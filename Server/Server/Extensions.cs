using Server.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Extensions
{
    public static bool SaveChangesEx(this GameDbContext db)
    {
        try
        {
            db.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogManager.Instance.Log($"[Error] DB Update Exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                ConsoleLogManager.Instance.Log($"[Error] Inner Exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    public static async Task<bool> SaveChangesExAsync(this GameDbContext db)
    {
        try
        {
            await db.SaveChangesAsync(); // 비동기 호출
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogManager.Instance.Log($"[Error] DB Update Exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                ConsoleLogManager.Instance.Log($"[Error] Inner Exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }
}