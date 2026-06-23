using log4net.Util;
using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Helpers;

namespace TerminalMCP
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            await BackupLatestLogAsync();

            ConsoleApp consoleApp = ConsoleApp.Create(args);
            await consoleApp.RunAsync();
        }

        private static async Task BackupLatestLogAsync()
        {
            string logFilePath = Path.Combine(AppContext.BaseDirectory, "Logs", "Latest.log");

            try
            {
                if (File.Exists(logFilePath))
                    await LogBackupHelper.BackupAsync(logFilePath, "yyyy-MM-dd");
            }
            catch (Exception ex)
            {
                LogLog.Error(typeof(Program), $"Failed to backup log file: {logFilePath}", ex);
            }
        }
    }
}
