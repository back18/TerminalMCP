using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP.Services
{
    public interface IClipboardLockService
    {
        public void Wait();

        public void Release();
    }
}
