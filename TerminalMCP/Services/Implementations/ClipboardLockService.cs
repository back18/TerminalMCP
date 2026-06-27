using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP.Services.Implementations
{
    public class ClipboardLockService : IClipboardLockService
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public void Wait()
        {
            _semaphore.Wait();
        }

        public void Release()
        {
            _semaphore.Release();
        }
    }
}
