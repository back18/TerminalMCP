using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TerminalMCP.Services.Implementations
{
    public class ClipboardService : IClipboardService
    {
        private const int TimeoutMs = 5000;

        public ClipboardService(ILogger<ClipboardService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _logger = logger;
        }

        private readonly ILogger<ClipboardService> _logger;

        public bool TryReadText([MaybeNullWhen(false)] out string? result)
        {
            string? text = null;
            Exception? capturedException = null;

            Thread thread = new(() =>
            {
                try
                {
                    text = Clipboard.GetText();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!thread.Join(TimeSpan.FromMilliseconds(TimeoutMs)))
            {
                _logger.LogWarning("ClipboardService.TryReadText: STA thread timed out after {timeout}ms", TimeoutMs);
                result = null;
                return false;
            }

            if (capturedException is not null)
            {
                _logger.LogWarning(capturedException, "ClipboardService.TryReadText: clipboard read failed");
                result = null;
                return false;
            }

            result = text;
            return true;
        }

        public bool TrySetText(string text)
        {
            ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));

            Exception? capturedException = null;

            Thread thread = new(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!thread.Join(TimeSpan.FromMilliseconds(TimeoutMs)))
            {
                _logger.LogWarning("ClipboardService.TrySetText: STA thread timed out after {timeout}ms", TimeoutMs);
                return false;
            }

            if (capturedException is not null)
            {
                _logger.LogWarning(capturedException, "ClipboardService.TrySetText: clipboard write failed");
                return false;
            }

            return true;
        }
    }
}
