# TerminalMCP 工具实现计划

> **For Hermes:** 按任务顺序逐项实现，每个任务 2-5 分钟。所有代码遵循 CLAUDE.md 规范。

**Goal:** 将 terminal_diff.py 的终端文字捕获能力迁移为 .NET MCP Server 的 4 个正式工具。

**Architecture:** P/Invoke Win32 API（纯 keybd_event + 剪贴板）+ ConcurrentDictionary 内存基线 + ToolResponse 统一响应模式。

**参考项目:** D:\SourceCode\TS\wpf_mcp\src\WpfMcp.Server（DI 注入、ToolResponse 模式、ErrorCodes 模式、JSON 序列化返回）

---

## 参考项目模式总结

| 模式 | WpfMcp.Server 做法 | 本项目采用 |
|------|-------------------|-----------|
| 响应格式 | `ToolResponse<T>` (success + data + error + metadata) | 同 |
| 错误码 | `ErrorCodes` 静态类 + const string | 同 |
| 服务注册 | Singleton DI | 同 |
| 工具类注册 | Singleton DI | 同 |
| 工具返回 | `string` (JsonSerializer.Serialize) | 同 |
| 执行计时 | `Stopwatch` + `ExecutionTimeMs` | 同 |
| 异常处理 | 每个方法 try-catch → Fail 响应 | 同 |
| 参数描述 | `[Description("...")]` | 同 |
| using 排序 | 不严格 | **严格遵守 CLAUDE.md A-Z 排序** |
| 命名空间 | 文件作用域 `namespace X;` | **块作用域 `namespace X { }`** |
| sealed | 用 sealed | **不用（除非有继承副作用）** |

---

## Task 0: 对齐项目基础设施

### Task 0.1: 修复 ConsoleApp.cs 使其符合 CLAUDE.md

**目标:** 修正现有代码的规范违规，为正式开发铺路

**文件:**
- Modify: `TerminalMCP/ConsoleApp.cs`

**改动:**

1. 去掉 `IServiceProvider` 实现（无必要）
2. 成员顺序调整为：构造函数 → 私有字段 → 公共属性 → 公共方法 → 私有方法，实例在前
3. using 按 A-Z 排序
4. 异常消息英文化

### Task 0.2: 修复 Program.cs using 排序

**目标:** using 按 A-Z 排列

**文件:**
- Modify: `TerminalMCP/Program.cs`

### Task 0.3: 修复 LogBackupHelper.cs

**目标:** using 排序 + 异常消息英文化

**文件:**
- Modify: `TerminalMCP/Helpers/LogBackupHelper.cs`

---

## Task 1: 创建响应模型和错误码

### Task 1.1: 创建 ErrorCodes 类

**文件:**
- Create: `TerminalMCP/Models/ErrorCodes.cs`

```csharp
namespace TerminalMCP.Models
{
    public static class ErrorCodes
    {
        public const string WindowNotFound = "WINDOW_NOT_FOUND";
        public const string WindowNotTerminal = "WINDOW_NOT_TERMINAL";
        public const string ClipboardError = "CLIPBOARD_ERROR";
        public const string CaptureFailed = "CAPTURE_FAILED";
        public const string InputFailed = "INPUT_FAILED";
        public const string InvalidParameter = "INVALID_PARAMETER";
        public const string NoTerminalWindows = "NO_TERMINAL_WINDOWS";
    }
}
```

### Task 1.2: 创建 ToolResponse<T> 泛型响应包装

**文件:**
- Create: `TerminalMCP/Models/ToolResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace TerminalMCP.Models
{
    public sealed class ToolResponse<T>
    {
        [JsonPropertyName("success")]
        public required bool Success { get; init; }

        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("error")]
        public ErrorInfo? Error { get; init; }

        [JsonPropertyName("metadata")]
        public ResponseMetadata Metadata { get; init; } = new();

        public static ToolResponse<T> Ok(T data, ResponseMetadata? metadata = null) => new()
        {
            Success = true,
            Data = data,
            Metadata = metadata ?? new ResponseMetadata()
        };

        public static ToolResponse<T> Fail(string code, string message, string? suggestion = null, bool recoverable = true) => new()
        {
            Success = false,
            Error = new ErrorInfo
            {
                Code = code,
                Message = message,
                Suggestion = suggestion,
                Recoverable = recoverable
            }
        };
    }

    public sealed class ErrorInfo
    {
        [JsonPropertyName("code")]
        public required string Code { get; init; }

        [JsonPropertyName("message")]
        public required string Message { get; init; }

        [JsonPropertyName("suggestion")]
        public string? Suggestion { get; init; }

        [JsonPropertyName("recoverable")]
        public bool Recoverable { get; init; } = true;
    }

    public sealed class ResponseMetadata
    {
        [JsonPropertyName("execution_time_ms")]
        public long ExecutionTimeMs { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; init; } = [];
    }
}
```

### Task 1.3: 创建业务领域模型

**文件:**
- Create: `TerminalMCP/Models/TerminalModels.cs`

```csharp
namespace TerminalMCP.Models
{
    public record TerminalInfo(int Hwnd, string Title, int LineCount, string TailPreview);

    public record ReadResult(int Hwnd, string Title, int TotalLines, int LinesRead, string Text);

    public record DiffResult(int Hwnd, string Title, int TotalLines, string Status, string Text, int NewLineCount);

    public record InputResult(bool Success);
}
```

---

## Task 2: 创建 Win32 P/Invoke 互操作层

### Task 2.1: 创建 NativeMethods 类

**文件:**
- Create: `TerminalMCP/Interop/NativeMethods.cs`

```csharp
using System.Runtime.InteropServices;

namespace TerminalMCP.Interop
{
    internal static class NativeMethods
    {
        public const string WtClassName = "CASCADIA_HOSTING_WINDOW_CLASS";

        public const int VkCtrl = 0x11;
        public const int VkShift = 0x10;
        public const int VkA = 0x41;
        public const int VkC = 0x43;
        public const int VkV = 0x56;
        public const int VkReturn = 0x0D;
        public const int KeyeventfKeyup = 0x0002;

        public const int SwRestore = 9;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }
}
```

---

## Task 3: 创建 ITerminalCaptureService 接口和实现

### Task 3.1: 创建接口

**文件:**
- Create: `TerminalMCP/Services/ITerminalCaptureService.cs`

```csharp
using TerminalMCP.Models;

namespace TerminalMCP.Services
{
    public interface ITerminalCaptureService : IDisposable
    {
        IReadOnlyList<TerminalInfo> EnumerateWindows();

        IReadOnlyList<TerminalInfo> Init();

        ReadResult ReadContent(int hwnd, int offset, int limit);

        DiffResult ReadDiff(int hwnd);

        InputResult TypeText(int hwnd, string text, bool pressEnter);

        bool IsValidTerminalWindow(int hwnd);
    }
}
```

### Task 3.2: 创建 TerminalCaptureService 实现

**文件:**
- Create: `TerminalMCP/Services/TerminalCaptureService.cs`

**关键实现点:**
- `ConcurrentDictionary<int, string> _baselines` — diff 基线
- `ConcurrentDictionary<int, TerminalInfo> _windowCache` — 窗口元数据缓存
- `EnumerateWindows()` — EnumWindows 回调 + 过滤 WT_CLASS
- `Init()` — 增量发现：新窗口捕获，已有窗口跳过
- `ReadContent(hwnd, offset, limit)` — 捕获 + 切片，不更新 _baselines
- `ReadDiff(hwnd)` — 捕获 + 逐行 diff + 更新 _baselines
- `TypeText(hwnd, text, pressEnter)` — 保存剪贴板 → 写入 → 粘贴 → 恢复
- `FocusWindow(hwnd)` — 恢复最小化 + SetForegroundWindow
- `CaptureContent(hwnd)` — Ctrl+Shift+A → Ctrl+C → 读剪贴板
- `BackupClipboard()` / `RestoreClipboard()` — 剪贴板保护
- `SliceLines(text, offset, limit)` — 从末尾倒数第 offset 行开始取 limit 行

---

## Task 4: 创建 MCP 工具类

### Task 4.1: 创建 TerminalTools 类

**文件:**
- Create: `TerminalMCP/Tools/TerminalTools.cs`

**4 个工具方法:**
- `TerminalInit()` → 返回 `ToolResponse<object>` (terminals 列表)
- `TerminalRead(int hwnd, int offset, int limit)` → 返回 `ToolResponse<ReadResult>`
- `TerminalDiff(int hwnd)` → 返回 `ToolResponse<DiffResult>`
- `TerminalInput(int hwnd, string text, bool pressEnter)` → 返回 `ToolResponse<InputResult>`

每个方法: try-catch + Stopwatch + JsonSerializer.Serialize + 错误码

---

## Task 5: 注册服务和工具到 DI 容器

### Task 5.1: 修改 ConsoleApp.cs

**目标:** 在 ConfigureServices 中注册 ITerminalCaptureService 和 TerminalTools

**文件:**
- Modify: `TerminalMCP/ConsoleApp.cs`

**改动:**
```csharp
builder.Services.AddSingleton<ITerminalCaptureService, TerminalCaptureService>();
builder.Services.AddSingleton<TerminalTools>();
```

---

## Task 6: 编译验证

### Task 6.1: 编译项目

```bash
cd TerminalMCP && dotnet build
```

修复所有编译错误（属性名差异、命名空间引用等）。

### Task 6.2: 验证 MCP Server 启动

```bash
cd TerminalMCP && dotnet run
# 应该正常启动 stdio MCP Server，无异常退出
```

---

## 文件变更总览

| 操作 | 文件 |
|------|------|
| Create | `Models/ErrorCodes.cs` |
| Create | `Models/ToolResponse.cs` |
| Create | `Models/TerminalModels.cs` |
| Create | `Interop/NativeMethods.cs` |
| Create | `Services/ITerminalCaptureService.cs` |
| Create | `Services/TerminalCaptureService.cs` |
| Create | `Tools/TerminalTools.cs` |
| Modify | `ConsoleApp.cs`（注册 DI 服务） |
| Modify | `Program.cs`（using 排序） |
| Modify | `Helpers/LogBackupHelper.cs`（using 排序 + 英文化异常消息） |

**新增 7 个文件，修改 3 个文件。**
