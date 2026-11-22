#Requires -RunAsAdministrator
param([Parameter(Mandatory)][string]$ServiceSid)
$ErrorActionPreference='Stop'
# Add only this SID/right pair. Importing a full security-policy template can overwrite other local users' rights.
if (-not ('Echodia.RedisBatchLogon' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace Echodia
{
    public static class RedisBatchLogon
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Attributes
        {
            public int Length;
            public IntPtr Root, Name;
            public uint Flags;
            public IntPtr Descriptor, Quality;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Unicode
        {
            public ushort Length, MaximumLength;
            public IntPtr Buffer;
        }
        [DllImport("advapi32.dll")]
        private static extern uint LsaOpenPolicy(IntPtr system, ref Attributes attributes, uint access, out IntPtr handle);
        [DllImport("advapi32.dll")]
        private static extern uint LsaAddAccountRights(IntPtr handle, byte[] sid, ref Unicode rights, uint count);
        [DllImport("advapi32.dll")]
        private static extern uint LsaNtStatusToWinError(uint status);
        [DllImport("advapi32.dll")]
        private static extern uint LsaClose(IntPtr handle);
        private static void Check(uint status)
        {
            if (status != 0) throw new Win32Exception((int)LsaNtStatusToWinError(status));
        }
        public static void Grant(string value)
        {
            var account = new SecurityIdentifier(value);
            var sid = new byte[account.BinaryLength];
            account.GetBinaryForm(sid, 0);
            var attributes = new Attributes { Length = Marshal.SizeOf(typeof(Attributes)) };
            IntPtr handle;
            Check(LsaOpenPolicy(IntPtr.Zero, ref attributes, 0x810, out handle));
            try {
                const string right = "SeBatchLogonRight";
                var name = new Unicode { Length = (ushort)(right.Length * 2), MaximumLength = (ushort)((right.Length + 1) * 2), Buffer = Marshal.StringToHGlobalUni(right) };
                try { Check(LsaAddAccountRights(handle, sid, ref name, 1)); }
                finally { Marshal.FreeHGlobal(name.Buffer); }
            } finally { LsaClose(handle); }
        }
    }
}
'@
}
$expected=(Get-LocalUser -Name 'EchodiaRedisSvc').SID.Value
if($ServiceSid -ne $expected){throw 'Grant batch logon only to the dedicated local Redis account.'}
[Echodia.RedisBatchLogon]::Grant($ServiceSid)
