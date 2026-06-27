using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Models;

namespace TerminalMCP.Utilities
{
    internal static class DiffCalculator
    {
        private const int MinConsecutiveMatches = 3;

        public static DiffOutput Compute(string[] previousLines, string[] currentLines)
        {
            if (currentLines.Length == 0)
                return EmptyResult();

            // Phase 1: prefix comparison — handles normal append-only growth
            int prefixMatch = 0;
            int minLines = Math.Min(previousLines.Length, currentLines.Length);
            while (prefixMatch < minLines && previousLines[prefixMatch] == currentLines[prefixMatch])
                prefixMatch++;

            // Old content is a prefix of new content
            if (prefixMatch >= previousLines.Length)
                return AppendedResult(currentLines, previousLines.Length);

            // Phase 2: alignment-based — handles buffer rollover (head trimmed)
            int anchorIdx = FindBestAnchor(previousLines, currentLines);
            if (anchorIdx < 0)
                return DivergedResult(currentLines, prefixMatch);

            // Phase 3: from the best anchor, scan for the first divergence
            int overlap = 0;
            while (overlap < currentLines.Length)
            {
                int prevIdx = anchorIdx + overlap;
                if (prevIdx >= previousLines.Length)
                    break; // Past end of previous — current[overlap..] is new content

                if (previousLines[prevIdx] != currentLines[overlap])
                    break; // Divergence found

                overlap++;
            }

            if (overlap >= currentLines.Length)
                return NoChangeResult();

            return new DiffOutput
            {
                Status = DiffStatus.New,
                NewLines = currentLines[overlap..],
                StartLine = overlap + 1
            };
        }

        private static int FindBestAnchor(string[] previous, string[] current)
        {
            string target = current[0];
            int bestIdx = -1;
            int bestMatches = 0;
            int searchFrom = 0;

            while (searchFrom < previous.Length)
            {
                int idx = Array.IndexOf(previous, target, searchFrom);
                if (idx < 0)
                    break;

                int matches = CountConsecutiveMatches(previous, idx, current, 0);
                bool qualifies = matches >= MinConsecutiveMatches || idx + matches >= previous.Length;
                if (qualifies && matches > bestMatches)
                {
                    bestMatches = matches;
                    bestIdx = idx;
                }

                searchFrom = idx + 1;
            }

            return bestIdx;
        }

        private static int CountConsecutiveMatches(string[] previous, int prevStart, string[] current, int curStart)
        {
            int count = 0;
            while (prevStart + count < previous.Length
                   && curStart + count < current.Length
                   && previous[prevStart + count] == current[curStart + count])
            {
                count++;
            }

            return count;
        }

        private static DiffOutput AppendedResult(string[] currentLines, int previousLength)
        {
            string[] appendedLines = currentLines[previousLength..];

            return new DiffOutput
            {
                Status = appendedLines.Length > 0 ? DiffStatus.New : DiffStatus.NoChange,
                NewLines = appendedLines,
                StartLine = previousLength + 1
            };
        }

        private static DiffOutput DivergedResult(string[] currentLines, int divergeLine)
        {
            string[] newLines = currentLines[divergeLine..];

            return new DiffOutput
            {
                Status = newLines.Length > 0 ? DiffStatus.New : DiffStatus.NoChange,
                NewLines = newLines,
                StartLine = divergeLine + 1
            };
        }

        private static DiffOutput EmptyResult()
        {
            return new DiffOutput
            {
                Status = DiffStatus.New,
                NewLines = [],
                StartLine = 1
            };
        }

        private static DiffOutput NoChangeResult()
        {
            return new DiffOutput
            {
                Status = DiffStatus.NoChange,
                NewLines = [],
                StartLine = 1
            };
        }
    }
}
