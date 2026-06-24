# TerminalMCP 工具设计文档

> **目标：** 将 Python 终端文字捕获脚本（terminal_diff.py / terminal_list.py）转换为 .NET MCP Server 的正式工具。
>
> **架构：** 内存内基线（ConcurrentDictionary）、实例级别隔离、无状态请求（客户端传 hwnd）、纯 keybd_event + 剪贴板技法。
>
> **技术栈：** .NET 10 / C# / ModelContextProtocol 1.4.0 / P/Invoke user32.dll

---

## 1. 设计原则

| 原则 | 说明 |
|------|------|
| **内存基线** | 每个 MCP 实例进程独立 `ConcurrentDictionary`，不共享文件 |
| **客户端传参** | 服务端不保存选中状态，每次调用由客户端提供 `hwnd` |
| **增量发现** | `terminal_init` 不再全量覆盖，新窗口追加，旧窗口复用缓存 |
| **读写分离** | `terminal_read` 只读不写基线，`terminal_diff` 读+写基线 |
| **键盘优先** | 全选→复制→剪贴板，零鼠标依赖 |
| **剪贴板保护** | `terminal_input` 写入前后保存/恢复剪贴板 |

---

## 2. MCP 工具清单

### 2.1 terminal_init — 增量发现终端窗口

```
输入：无

行为：
  1. EnumWindows → 过滤 CASCADIA_HOSTING_WINDOW_CLASS
  2. 与 _baselines 对比：
     - 新 hwnd → 聚焦窗口 → Ctrl+Shift+A → Ctrl+C → 读剪贴板 → 存入 _baselines
     - 已缓存 hwnd → 跳过捕获，仅更新 title（GetWindowTextW）
  3. 清理 _baselines 中已不存在的 hwnd
  4. 返回全部已知窗口概览

返回：
  {
    terminals: [
      {
        hwnd: int,
        title: string,
        line_count: int,
        tail_preview: string    // 最后 15 行
      }
    ]
  }
```

### 2.2 terminal_read — 读取终端内容

```
输入：
  hwnd: int            // 目标窗口句柄
  offset: int = 1      // 起始位置（1=最后一行，即从末尾倒数第1行开始）
  limit: int = 20      // 最大返回行数

行为：
  1. 校验 hwnd 是否为 WT 窗口
  2. 聚焦窗口 → Ctrl+Shift+A → Ctrl+C → 读剪贴板
  3. 按 offset/limit 切片返回：
     - offset 以最后一行（最新行）为基点 1
     - 从倒数第 offset 行开始，向上取 limit 行
  4. 不更新 _baselines

返回：
  {
    hwnd: int,              // 窗口句柄
    title: string,          // 窗口标题
    total_lines: int,       // 终端当前总行数
    lines_read: int,        // 实际读取行数
    text: string            // 切片后的文本
  }

offset 语义（设终端共 100 行，最后一行编号为 1）：
  (1, 10)   → 第100行..第91行  （最近 10 行）
  (1, 20)   → 第100行..第81行  （最近 20 行，默认值）
  (10, 30)  → 第91行..第62行   （跳过最近 9 行，向上取 30 行）

示例：
  terminal_read(hwnd=10292576, offset=1, limit=10)
    → { hwnd: 10292576, title: "cmd.exe", total_lines: 100, lines_read: 10, text: "最近10行..." }
  terminal_read(hwnd=10292576, offset=10, limit=30)
    → { hwnd: 10292576, title: "cmd.exe", total_lines: 100, lines_read: 30, text: "第62-91行..." }
```

### 2.3 terminal_diff — 增量差异

```
输入：
  hwnd: int           // 目标窗口句柄

行为：
  1. 校验 hwnd 是否为 WT 窗口
  2. 聚焦窗口 → Ctrl+Shift+A → Ctrl+C → 读剪贴板
  3. 与 _baselines[hwnd] 逐行对比：
     - 无基线 → status="init"，返回全量文本，基线写入 _baselines
     - 内容相同 → status="no_change"，返回空
     - 内容不同 → 找到第一条差异行，返回差异行起全部内容
  4. 更新 _baselines[hwnd] 为当前内容

返回：
  {
    hwnd: int,              // 窗口句柄
    title: string,          // 窗口标题
    total_lines: int,       // 终端当前总行数
    status: "init" | "no_change" | "new",
    text: string,           // init=全量, new=增量, no_change=空
    new_line_count: int     // 新增行数
  }
```

### 2.4 terminal_input — 终端输入

```
输入：
  hwnd: int              // 目标窗口句柄
  text: string           // 要输入的文本
  press_enter: bool = true  // 是否在粘贴后发送回车

行为：
  1. 校验 hwnd 是否为 WT 窗口
  2. 保存当前剪贴板内容（ClipboardBackup）
  3. 将 text 写入剪贴板
  4. 聚焦窗口 → Ctrl+V（粘贴）
  5. if press_enter → keybd_event(VK_RETURN)
  6. 恢复剪贴板原始内容

返回：
  { success: bool }
```

---

## 3. 内存架构

```
TerminalCaptureService : IDisposable  (注册为 Singleton)
│
├── _baselines: ConcurrentDictionary<int, string>
│     hwnd → 最近一次 terminal_diff 后的完整终端文本
│     terminal_read 不修改此项
│
├── _windowCache: ConcurrentDictionary<int, WindowInfo>
│     hwnd → { title, last_capture_time }
│     terminal_init 更新此项
│
└── _clipboardBackup: string?
      terminal_input 前后保存/恢复
```

### 生命周期

```
MCP Client 连接
  → Hermes 启动 TerminalMCP.exe (stdio)
    → Host 构建 → TerminalCaptureService 构造
      → _baselines = 空字典
      → _windowCache = 空字典
  → AI 调用 terminal_init → 捕获窗口 → 存入内存
  → AI 调用 terminal_diff → 更新基线
  → AI 调用 terminal_read → 只读不写
  ...
MCP Client 断开
  → 进程退出 → 内存释放 → 完全隔离
```

---

## 4. Win32 P/Invoke 清单

| API | 用途 | 调用位置 |
|-----|------|---------|
| `EnumWindows` | 枚举所有顶层窗口 | EnumerateWindows |
| `IsWindowVisible` | 过滤可见窗口 | EnumerateWindows |
| `GetClassNameW` | 获取窗口类名 | 过滤 WT_CLASS |
| `GetWindowTextW` | 获取窗口标题 | EnumerateWindows, ValidateWindow |
| `IsWindow` | 校验句柄有效性 | ValidateWindow |
| `IsIconic` | 判断是否最小化 | FocusWindow |
| `ShowWindow` | 恢复最小化窗口 | FocusWindow |
| `SetForegroundWindow` | 聚焦窗口 | FocusWindow |
| `keybd_event` | 模拟键盘输入 | CaptureContent, TypeText |
| `OpenClipboard` / `EmptyClipboard` / `SetClipboardData` / `CloseClipboard` | 剪贴板操作 | TypeText |
| `GetClipboardData` | 读取剪贴板 | BackupClipboard |

常量：
- `CASCADIA_HOSTING_WINDOW_CLASS` — Windows Terminal 窗口类名
- `VK_CTRL (0x11)`, `VK_SHIFT (0x10)`, `VK_A (0x41)`, `VK_C (0x43)`, `VK_V (0x56)`, `VK_RETURN (0x0D)`
- `KEYEVENTF_KEYUP (0x0002)`

---

## 5. 项目文件结构

```
TerminalMCP/
├── Program.cs                      // 入口（不动）
├── ConsoleApp.cs                   // Host 配置（需修改：注册新服务）
├── TerminalMCP.csproj              // 项目文件（可能加 NuGet 引用）
│
├── Models/
│   └── TerminalInfo.cs             // record: hwnd, title, line_count, tail_preview
│
├── Services/
│   ├── ITerminalCaptureService.cs  // 接口
│   └── TerminalCaptureService.cs   // 实现：P/Invoke + 键鼠模拟 + 内存缓存
│
├── Interop/
│   └── NativeMethods.cs            // 所有 P/Invoke 声明（internal static）
│
├── Tools/
│   └── TerminalTools.cs            // [McpToolType] — 4 个工具方法
│
├── Helpers/
│   └── LogBackupHelper.cs          // 已有，不动
│
└── Config/
    └── log4net.config              // 已有，不动
```

---

## 6. 关键接口

```csharp
public interface ITerminalCaptureService : IDisposable
{
    // 窗口枚举
    IReadOnlyList<TerminalInfo> EnumerateWindows();

    // terminal_init 核心：增量发现，返回全部已知窗口概览
    IReadOnlyList<TerminalInfo> Init();

    // terminal_read 核心：捕获终端内容（不更新基线）
    ReadResult ReadContent(int hwnd, int offset, int limit);

    // terminal_diff 核心：捕获终端内容 + diff + 更新基线
    DiffResult ReadDiff(int hwnd);

    // terminal_input 核心：剪贴板中转 → 粘贴 → 恢复剪贴板
    InputResult TypeText(int hwnd, string text, bool pressEnter);

    // 校验窗口
    bool IsValidTerminalWindow(int hwnd);
}

// 返回模型
public record TerminalInfo(int Hwnd, string Title, int LineCount, string TailPreview);

public record ReadResult(int Hwnd, string Title, int TotalLines, int LinesRead, string Text);

public record DiffResult(int Hwnd, string Title, int TotalLines, string Status, string Text, int NewLineCount);

public record InputResult(bool Success);
```

---

## 7. 异常处理约定

| 场景 | 行为 |
|------|------|
| hwnd 不存在 | 返回错误信息，不抛异常 |
| hwnd 不是 WT 窗口 | 返回错误信息 |
| 剪贴板读取超时 | 返回错误，不抛异常 |
| 剪贴板恢复失败 | 记录日志，不阻断主流程 |
| 窗口最小化 | 自动恢复后再操作 |
| 无 WT 窗口 | terminal_init 返回空列表 |

所有 MCP 工具方法返回结构化结果，不抛未捕获异常。

---

## 8. 典型使用流程

```
1. terminal_init()
   → { terminals: [
       { hwnd: 10292576, title: "cmd.exe", line_count: 6, tail_preview: "..." },
       { hwnd: 2757322,  title: "bash",    line_count: 42, tail_preview: "..." }
     ]}

2. terminal_read(hwnd=10292576, offset=1, limit=10)
   → { hwnd: 10292576, title: "cmd.exe", total_lines: 6, lines_read: 6, text: "back18@DESKTOP:~$ ..." }

3. terminal_diff(hwnd=10292576)
   → { hwnd: 10292576, title: "cmd.exe", total_lines: 6, status: "init", text: "...(全量)...", new_line_count: 6 }

4. terminal_input(hwnd=10292576, text="ls -la", press_enter=true)
   → { success: true }

5. terminal_diff(hwnd=10292576)
   → { hwnd: 10292576, title: "cmd.exe", total_lines: 21, status: "new", text: "total 128\ndrwxr-xr-x ...", new_line_count: 15 }
```
