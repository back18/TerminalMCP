# TerminalMCP

一个 MCP (Model Context Protocol) 服务器，让 AI 代理能够读写 Windows Terminal 窗口中的文本内容 —— 支持 cmd.exe、PowerShell、WSL 以及任何运行在 Windows Terminal 中的 CLI。

纯 Win32 API 实现（`keybd_event` + 剪贴板），不依赖 UIA，无需 SendKeys。基于 .NET 10 构建，使用官方 [`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) NuGet 包。

## 功能

- **发现**所有已打开的 Windows Terminal 窗口（`terminal_init`）
- **读取**缓冲内容，按行偏移定位，带倒序行号前缀（`terminal_read`）
- **差分**输出，基于基线比对返回变更或新增内容，带绝对行号前缀（`terminal_diff`）
- **输入**命令，通过剪贴板粘贴（`terminal_input`）
- **发送按键**，响应交互式提示（`terminal_key`）

## 工具

| 工具 | 说明 |
|------|------|
| `terminal_init` | 发现所有 Windows Terminal 窗口并建立内容基线 |
| `terminal_read` | 从内存缓存读取终端内容，每行带倒序行号前缀（`offset`，1=最后一行）。有缓存时不触碰终端窗口；无缓存时会自动捕获一次 |
| `terminal_diff` | 捕获当前内容并与基线比对，返回变更部分，每行带绝对行号前缀（`57`，1-based 从顶部起算）。首次调用返回全部内容（`init`），无变化时返回空（`no_change`） |
| `terminal_input` | 通过剪贴板粘贴将文本输入终端窗口 |
| `terminal_key` | 发送单个按键（Enter、Esc、Tab、Backspace、Delete、方向键、Home/End、Y/N、A/C/V/D 等） |

## 安装

### 前置条件

- Windows 10+，已安装 [Windows Terminal](https://github.com/microsoft/terminal)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 构建与发布

```bash
# 还原依赖（仓库根目录）
dotnet restore TerminalMCP.slnx

# 调试构建
dotnet build TerminalMCP.slnx -c Debug

# 发布构建
dotnet publish TerminalMCP/TerminalMCP.csproj -c Release -o TerminalMCP/bin/Release/net10.0-windows/publish
```

### 配置 MCP 客户端

#### 使用 `dotnet run`（从源码运行）：

添加到 Claude Desktop (`claude_desktop_config.json`) 或 Claude Code (`.mcp.json`)：

```json
{
  "mcpServers": {
    "terminal-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "<repo-path>/TerminalMCP/TerminalMCP.csproj", "-c", "Release"]
    }
  }
}
```

#### 使用已发布的二进制文件：

```json
{
  "mcpServers": {
    "terminal-mcp": {
      "command": "<repo-path>/TerminalMCP/bin/Release/net10.0-windows/publish/TerminalMCP.exe",
      "args": []
    }
  }
}
```

## 使用

### 典型工作流

```
terminal_init()                         → 发现所有 WT 窗口
terminal_read(hwnd, offset=1, limit=20) → 读取最后 20 行，每行带倒序行号（20|...1|）
terminal_input(hwnd, "echo hello")      → 输入命令
terminal_diff(hwnd)                     → 增量输出，每行带绝对行号（57|TEST LINE...）
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
