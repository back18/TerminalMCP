# TerminalMCP

一个 MCP (Model Context Protocol) 服务器，让 AI 代理能够读写 Windows Terminal 窗口中的文本内容 —— 支持 cmd.exe、PowerShell、WSL 以及任何运行在 Windows Terminal 中的 CLI。

纯 Win32 API 实现（`keybd_event` + 剪贴板），不依赖 UIA，无需 SendKeys。基于 .NET 10 构建，使用官方 [`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) NuGet 包。

## 功能

- **发现**所有已打开的 Windows Terminal 窗口（`terminal_init`）
- **读取**缓冲内容，按行偏移定位（`terminal_read`）
- **追踪**增量输出，基于 baseline 差分（`terminal_diff`）
- **输入**命令，通过剪贴板粘贴（`terminal_input`）
- **发送按键**，响应交互式提示（`terminal_key`）

## 工具

| 工具 | 说明 |
|------|------|
| `terminal_init` | 发现所有 Windows Terminal 窗口并建立内容基线 |
| `terminal_read` | 从内存缓存读取终端内容（不触碰终端窗口） |
| `terminal_diff` | 捕获当前内容，仅返回上次基线之后的新增行 |
| `terminal_input` | 通过剪贴板粘贴将文本输入终端窗口 |
| `terminal_key` | 发送单个按键（Enter、Esc、方向键、Y/N 等） |

## 安装

### 前置条件

- Windows 10+，已安装 [Windows Terminal](https://github.com/microsoft/terminal)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 构建与发布

```bash
cd TerminalMCP
dotnet publish -c Release -o bin/Release/net10.0-windows/publish
```

### 在 Hermes Agent 中配置

添加到 `config.yaml`：

```yaml
mcp_servers:
  terminal-mcp:
    command: dotnet
    args:
      - run
      - --project
      - D:/SourceCode/TS/TerminalMCP/TerminalMCP/TerminalMCP.csproj
      - -c
      - Release
    enabled: true
```

或直接使用已发布的二进制文件：

```yaml
mcp_servers:
  terminal-mcp:
    command: D:/SourceCode/TS/TerminalMCP/TerminalMCP/bin/Release/net10.0-windows/publish/TerminalMCP.exe
    args: []
    enabled: true
```

## 使用

### 典型工作流

```
terminal_init()                         → 发现所有 WT 窗口
terminal_read(hwnd, offset=1, limit=20) → 读取目标窗口最后 20 行
terminal_input(hwnd, "echo hello")      → 输入命令
terminal_diff(hwnd)                     → 查看上次捕获以来的新增输出
terminal_key(hwnd, "enter")             → 确认交互式提示
terminal_key(hwnd, "up")                → 菜单导航
```

### 多窗口定位

`terminal_init` 返回所有 Windows Terminal 窗口的列表，包含 `hwnd`、`title`、`line_count`、`tail_preview`。后续所有调用都通过 `hwnd` 指定目标窗口。

### 交互式提示

对于使用确认对话框的 AI CLI 工具（Claude Code、Codex 等）：

```
terminal_key(hwnd, "down")   → 下移选择
terminal_key(hwnd, "up")     → 上移选择
terminal_key(hwnd, "enter")  → 确认选择
terminal_key(hwnd, "y")      → Yes 确认
terminal_key(hwnd, "n")      → No / 取消
terminal_key(hwnd, "escape") → 关闭提示
```

## 架构

```
┌──────────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│ TerminalMCP (.NET)   │     │ Win32 P/Invoke    │     │ 剪贴板          │
│ (stdio MCP 服务器)   │ ──► │ (keybd_event)     │ ──► │ (STA 线程)      │
│ init / read / diff / │     │ (SetForeground)   │     │                 │
│ input / key          │     └──────────────────┘     └─────────────────┘
└──────────────────────┘
       ▲
       │ MCP 协议 (stdio)
       │
┌──────┴───────────┐
│ AI 代理           │
│ (Hermes、Claude、  │
│  Cursor 等)       │
└──────────────────┘
```

## 已知限制

- **剪贴板依赖**：读写操作使用系统剪贴板，操作期间避免其他程序修改剪贴板。
- **无持久化状态**：所有基线仅存于进程内存中，重启 MCP 服务器后会丢失，需重新运行 `terminal_init` 建立基线。不同 MCP 客户端各自启动的进程完全隔离，互不影响。

## 许可证

MIT License — 详见 [LICENSE.txt](LICENSE.txt)
