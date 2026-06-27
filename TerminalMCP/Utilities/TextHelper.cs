using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP.Utilities
{
    internal static class TextHelper
    {
        public static string[] SliceLines(string[] allLines, int offset, int limit)
        {
            // offset is 1-based from the end: offset=1 = last line
            int startIdx = allLines.Length - offset - limit + 1;
            if (startIdx < 0)
            {
                startIdx = 0;
                limit = allLines.Length - offset + 1;
                if (limit < 0)
                    limit = 0;
            }

            int actualLimit = Math.Min(limit, allLines.Length - startIdx);
            if (actualLimit <= 0)
                return [];

            string[] result = new string[actualLimit];
            Array.Copy(allLines, startIdx, result, 0, actualLimit);
            return result;
        }

        public static string[] SplitLines(string text)
        {
            string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

            int lastNonEmpty = lines.Length - 1;
            while (lastNonEmpty >= 0 && string.IsNullOrEmpty(lines[lastNonEmpty]))
                lastNonEmpty--;

            if (lastNonEmpty == lines.Length - 1)
                return lines;

            return lines[..(lastNonEmpty + 1)];
        }

        public static string BuildLines(string[] lines, int start)
        {
            int line = start;
            int width = (start + lines.Length - 1).ToString().Length;
            StringBuilder stringBuilder = new();

            for (int i = 0; i < lines.Length; i++)
            {
                stringBuilder.Append((line++).ToString().PadLeft(width));
                stringBuilder.Append('|');
                stringBuilder.AppendLine(lines[i]);
            }

            if (stringBuilder.Length > 0)
                stringBuilder.Length -= Environment.NewLine.Length;

            return stringBuilder.ToString();
        }

        public static string BuildLinesByDescending(string[] lines, int offset)
        {
            int line = lines.Length + offset - 1;
            int width = line.ToString().Length;
            StringBuilder stringBuilder = new();

            for (int i = 0; i < lines.Length; i++)
            {
                stringBuilder.Append((line--).ToString().PadLeft(width));
                stringBuilder.Append('|');
                stringBuilder.AppendLine(lines[i]);
            }

            if (stringBuilder.Length > 0)
                stringBuilder.Length -= Environment.NewLine.Length;

            return stringBuilder.ToString();
        }
    }
}
