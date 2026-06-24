using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using log4net.Util;
using TerminalMCP.Helpers;

namespace TerminalMCP
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                await BackupLatestLogAsync();

                ConsoleApp consoleApp = ConsoleApp.Create(args);
                await consoleApp.RunAsync();
            }
            finally
            {
                _mutex?.Dispose();
            }
        }

        private static Mutex? _mutex;

        private static async Task BackupLatestLogAsync()
        {
            if (!IsFirstInstance())
            {
                LogLog.Warn(typeof(Program), "Another TerminalMCP server instance is already running in this directory. Skipping log backup.");
                return;
            }

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

        private static bool IsFirstInstance()
        {
            string mutexName = ComputeMutexName();

            try
            {
                _mutex = new Mutex(true, mutexName, out bool createdNew);

                if (!createdNew)
                {
                    _mutex.Dispose();
                    _mutex = null;
                }

                return createdNew;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogLog.Warn(typeof(Program), $"Failed to create mutex '{mutexName}': {ex.Message}. Assuming first instance.");
                return true;
            }
        }

        private static string ComputeMutexName()
        {
            string directory = AppContext.BaseDirectory;
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(directory));
            string hash = Convert.ToHexString(hashBytes)[..8];

            return $@"Local\TerminalMCP_{hash}";
        }
    }
}
