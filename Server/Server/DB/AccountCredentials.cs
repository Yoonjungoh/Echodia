using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Google.Protobuf.Protocol;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Server.DB
{
    internal static class AccountCredentials
    {
        // V1 stores a 16-byte random salt followed by a 32-byte verifier.
        // Change the storage version when changing these parameters.
        const int SaltSize = 16;
        const int KeySize = 32;
        const int Iterations = 600_000;

        static byte[] Hash(string password)
        {
            byte[] record = new byte[SaltSize + KeySize];
            RandomNumberGenerator.Fill(record.AsSpan(0, SaltSize));
            Rfc2898DeriveBytes.Pbkdf2(password, record.AsSpan(0, SaltSize),
                record.AsSpan(SaltSize), Iterations, HashAlgorithmName.SHA256);
            return record;
        }

        static bool Verify(byte[] record, string password)
        {
            if (record == null || record.Length != SaltSize + KeySize)
                return false;
            Span<byte> actual = stackalloc byte[KeySize];
            Rfc2898DeriveBytes.Pbkdf2(password, record.AsSpan(0, SaltSize),
                actual, Iterations, HashAlgorithmName.SHA256);
            bool matches = CryptographicOperations.FixedTimeEquals(actual, record.AsSpan(SaltSize));
            CryptographicOperations.ZeroMemory(actual);
            return matches;
        }

        static bool ConvertLegacy(GameDbContext db, AccountDb account)
        {
            string legacy = account.AccountPassword;
            byte[] hash = Hash(legacy);
            // SQL string equality pads trailing spaces. Check byte length as well.
            using var tx = db.Database.BeginTransaction();
            bool updated = db.Accounts.Where(a => a.AccountDbId == account.AccountDbId &&
                    a.PasswordHashV1 == null &&
                    EF.Functions.DataLength(a.AccountPassword) == legacy.Length * sizeof(char) &&
                    EF.Functions.Collate(a.AccountPassword, "Latin1_General_100_BIN2") == legacy)
                .ExecuteUpdate(update => update.SetProperty(a => a.PasswordHashV1, hash)
                    .SetProperty(a => a.AccountPassword, (string)null)) == 1;

            if (updated)
            {
                byte[] saved = db.Accounts.Where(a => a.AccountDbId == account.AccountDbId)
                    .Select(a => a.PasswordHashV1).Single();
                if (!Verify(saved, legacy))
                    throw new InvalidOperationException("Stored verifier failed verification.");
            }
            tx.Commit();
            return updated;
        }

        public static (LoginStatus Status, int AccountId) Authenticate(GameDbContext db, string id, string password)
        {
            var byName = db.Accounts.AsNoTracking().Where(a => a.AccountId == id);
            AccountDb account = byName.SingleOrDefault();
            if (account == null)
            {
                var created = new AccountDb { AccountId = id, PasswordHashV1 = Hash(password) };
                db.Accounts.Add(created);
                try
                {
                    db.SaveChanges();
                    return (LoginStatus.SignUpSuccess, created.AccountDbId);
                }
                catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
                {
                    db.Entry(created).State = EntityState.Detached;
                    account = byName.Single();
                }
            }

            byte[] stored = account.PasswordHashV1;
            if (stored == null && account.AccountPassword != null &&
                CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(account.AccountPassword.AsSpan()),
                    MemoryMarshal.AsBytes(password.AsSpan())))
            {
                if (ConvertLegacy(db, account))
                    return (LoginStatus.Success, account.AccountDbId);
                stored = db.Accounts.Where(a => a.AccountDbId == account.AccountDbId)
                    .Select(a => a.PasswordHashV1).SingleOrDefault();
            }
            return Verify(stored, password)
                ? (LoginStatus.Success, account.AccountDbId)
                : (LoginStatus.PasswordWrong, 0);
        }

        public static int ConvertRemaining(GameDbContext db)
        {
            int cursor = 0;
            int converted = 0;
            while (true)
            {
                var batch = db.Accounts.AsNoTracking().Where(a => a.AccountDbId > cursor &&
                        a.PasswordHashV1 == null && a.AccountPassword != null)
                    .OrderBy(a => a.AccountDbId).Take(32).ToList();
                if (batch.Count == 0)
                    return converted;
                foreach (AccountDb account in batch)
                    if (ConvertLegacy(db, account))
                        converted++;
                cursor = batch[^1].AccountDbId;
            }
        }
    }
}
