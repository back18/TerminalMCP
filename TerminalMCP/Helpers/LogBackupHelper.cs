using log4net.Util;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace TerminalMCP.Helpers
{
    public static class LogBackupHelper
    {
        public static async Task BackupAsync(string filePath, string dateTimeFormat, bool enableCompression = true, bool deleteSourceFile = true)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentException.ThrowIfNullOrEmpty(dateTimeFormat, nameof(dateTimeFormat));

            if (!File.Exists(filePath))
            {
                LogLog.Warn(typeof(LogBackupHelper), $"需要备份的文件“{filePath}”不存在");
                return;
            }

            string savePath = QueryFileName(filePath, dateTimeFormat, enableCompression);

            if (enableCompression)
            {
                using FileStream sourceStream = new(filePath, FileMode.Open, FileAccess.Read);
                using FileStream targetStream = new(savePath, FileMode.CreateNew);
                using GZipStream gZipStream = new(targetStream, CompressionMode.Compress);
                await sourceStream.CopyToAsync(gZipStream);
                await gZipStream.FlushAsync();
                await targetStream.FlushAsync();
            }
            else
            {
                File.Copy(filePath, savePath);
            }

            if (deleteSourceFile)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    LogLog.Warn(typeof(LogBackupHelper), $"备份已完成，源文件“{filePath}”删除失败", ex);
                }
            }
        }

        private static string QueryFileName(string filePath, string dateTimeFormat, bool enableCompression)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            string format = File.GetLastWriteTime(filePath).ToString(dateTimeFormat) + "-{0}.log";
            if (enableCompression)
                format += ".gz";

            if (format.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                throw new InvalidOperationException("路径包含无效字符：" + format);

            string path = string.Empty;
            for (int i = 1; i <= 65536; i++)
            {
                path = Path.Combine(directory, string.Format(format, i));
                if (!File.Exists(path))
                    break;
            }

            return path;
        }
    }
}
