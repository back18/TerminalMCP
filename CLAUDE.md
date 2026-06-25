# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

TerminalMCP 是一个 MCP (Model Context Protocol) 服务器，允许 AI 代理通过 Win32 API 读写 Windows Terminal 窗口内容。基于 .NET 10 + `ModelContextProtocol` NuGet 包，使用 `keybd_event` + 剪贴板操作终端（不依赖 UIA）。

## 构建与运行

```bash
# 还原依赖（仓库根目录）
dotnet restore TerminalMCP.slnx

# 调试构建
dotnet build TerminalMCP.slnx -c Debug

# 发布构建
dotnet publish TerminalMCP/TerminalMCP.csproj -c Release -o TerminalMCP/bin/Release/net10.0-windows/publish
```

**使用 `.slnx` 解决方案文件**（`TerminalMCP.slnx`），与 `.sln` 等价。也可以直接操作 `TerminalMCP/TerminalMCP.csproj`。

**没有测试项目**。验证功能需要通过真实 MCP 客户端（如 Hermes Agent）运行并调用 MCP 工具。

## 架构

```
Program.cs (入口)
  └─ ConsoleApp.cs (Host 构建器，DI 注册，MCP 服务器配置)
       └─ Tools/TerminalTools.cs (5 个 MCP 工具方法)
            └─ Services/ITerminalCaptureService.cs (终端交互核心)
                 ├─ Interop/NativeMethods.cs (Win32 P/Invoke)
                 └─ Services/IClipboardService.cs (剪贴板读写)
```

### MCP 工具详解

所有工具注册在 `Tools/TerminalTools.cs`，通过 `[McpServerTool]` 特性暴露。每个工具方法返回 JSON 字符串，统一使用 `ToolResponse<T>.Ok()` / `ToolResponse<T>.Fail()` 包装，异常在工具层被 catch 后转为错误响应，绝不会向上抛出。

#### terminal_init — 终端发现与基线建立

**方法**: `TerminalInit()`，无参数。

**流程**:
1. 调用 `EnumerateWindows()` — 通过 `EnumWindows` 枚举所有顶层窗口，筛选窗口类名为 `CASCADIA_HOSTING_WINDOW_CLASS` 的可见窗口
2. 对于已在 `_windowCache` 中的已知窗口，只更新标题（从 `GetWindowTextW` 获取）
3. 对于新发现的窗口，执行完整捕获流程：`FocusWindow` → `CaptureContent`（Ctrl+Shift+A 全选 → Ctrl+C 复制 → 读剪贴板 → 恢复剪贴板）→ 按换行符分割为 `string[]` 存入 `_baselines` → 取尾部 20 行作为 `tail_preview`
4. 清理 `_windowCache` 中已不存在的过期窗口
5. 返回 `TerminalInitResult`，包含所有窗口的 `hwnd`、`title`、`line_count`、`tail_preview`

**副作用**: 对新窗口会切换窗口焦点并使用剪贴板；对已知窗口无副作用。

#### terminal_read — 读取缓存内容

**方法**: `TerminalRead(int hwnd, int offset = 1, int limit = 20)`

**流程**:
1. 验证 `hwnd` 是否为有效 WT 窗口（`IsValidTerminalWindow`）
2. 从 `_baselines` 读取缓存内容；若无基线则先执行一次捕获建立基线
3. 通过 `SliceLines` 切片：`offset` 为 1-based 倒序偏移（1=最后一行），从 `allLines.Length - offset - limit + 1` 位置开始取 `limit` 行
4. 通过 `BuildLinesByDescending` 为每行添加倒序行号前缀（`5|Line 48` 格式），数字从 `limit + offset - 1` 递减到 `offset`，等宽对齐
5. 返回 `ReadResult`：`hwnd`、`title`、`total_lines`、`lines_read`、`text`（每行带行号前缀）

**特点**: 纯内存操作，不触碰终端或剪贴板，速度最快。

#### terminal_diff — 增量差异捕获

**方法**: `TerminalDiff(int hwnd)`

**流程**:
1. 验证窗口 → 聚焦窗口 → 捕获当前完整内容（Ctrl+Shift+A → Ctrl+C → 读剪贴板）
2. 若无历史基线 → 建立基线，返回 `status: "init"` + 全部内容
3. 若有基线 → 行级前缀比对：从第 0 行开始逐行比较 `previousLines[i] == currentLines[i]`
   - 在旧内容范围内未找到分叉 → 旧内容是当前内容的前缀，返回 `previousLines.Length..` 的追加行，通过 `BuildLines(appendedLines, previousLines.Length + 1)` 添加升序绝对行号
   - 在旧内容范围内找到分叉 → 返回从分叉点到末尾的所有行，通过 `BuildLines(newLines, divergeLine + 1)` 添加升序绝对行号
4. 更新 `_baselines` 为最新内容
5. 返回 `DiffResult`：`status` 为 `"init"` / `"new"` / `"no_change"`，`text` 为新内容（每行带绝对行号前缀，如 `57|TEST LINE...`），`new_line_count` 为新行数

**特点**: 必须与终端交互（聚焦 + 捕获），会短暂占用剪贴板。`"no_change"` 状态时 `text` 为空字符串。

#### terminal_input — 文本输入

**方法**: `TerminalInput(int hwnd, string text, bool pressEnter = true)`

**流程**:
1. 验证窗口 + 参数非空
2. `_clipboardLock.Wait()` 获取剪贴板锁 → 备份当前剪贴板内容 → `ClipboardService.TrySetText(text)` 设置新内容 → `FocusWindow(hwnd)` → 发送 `Ctrl+V` 粘贴 → 若 `pressEnter=true` 则发送 `VK_RETURN` → 恢复剪贴板原内容 → 释放锁
3. 返回 `InputResult`：`success: true/false`

**实现位置**: `TerminalCaptureService.TypeText()`。关键常量：`VkCtrl = 0x11`、`VkV = 0x56`、`VkReturn = 0x0D`。

#### terminal_key — 按键发送

**方法**: `TerminalKey(int hwnd, string key)`

**流程**:
1. 验证窗口 + 参数非空
2. `KeyNameToVk(key)` 将键名映射为虚拟键码 — 支持 `enter`/`return`、`escape`/`esc`、`tab`、`space`、`backspace`、`delete`/`del`、`up`/`down`/`left`/`right`、`home`、`end`、`y`、`n`、`a`、`c`、`v`、`d`
3. `FocusWindow(hwnd)` → `keybd_event` 按下 → 30ms 延迟 → `keybd_event`（带 `KEYEVENTF_KEYUP`）释放
4. 返回 `KeyResult`：`success: true/false` + 实际发送的 `key`

**实现位置**: `TerminalCaptureService.SendKey()`。组合键（如 `Ctrl+V`）通过 `SendKeyCombo` 实现：先按顺序按下所有键，再按逆序释放。

### 关键设计决策

- **MCP 传输**：stdio JSON-RPC，由 `ModelContextProtocol` 库处理协议层
- **终端发现**：通过 `EnumWindows` + 窗口类名 `CASCADIA_HOSTING_WINDOW_CLASS` 识别 WT 窗口
- **内容捕获**：`Ctrl+Shift+A`（全选）→ `Ctrl+C`（复制）→ 读剪贴板，完成后恢复剪贴板原内容
- **文本输入**：设置剪贴板 → `Ctrl+V` 粘贴
- **按键发送**：`keybd_event` 发送虚拟键码
- **剪贴板线程模型**：`ClipboardService` 在 STA 线程上执行剪贴板操作（Windows 剪贴板 API 要求）
- **diff 算法**：行级前缀比对 — 从第一行开始逐行比较新旧内容，找到第一个分叉点
- **单实例**：通过 named mutex (`Local\TerminalMCP_{hash}`) 确保每目录只运行一个实例
- **日志**：使用 log4net，stdout 被 MCP JSON-RPC 占用，console appender 已注释，日志写入 `Logs/Latest.log` 和 `Logs/Debug.log`

### 依赖注入层次

| 服务 | 生命周期 | 职责 |
|:---|:---|:---|
| `IClipboardService` → `ClipboardService` | Singleton | STA 线程剪贴板读写 |
| `ITerminalCaptureService` → `TerminalCaptureService` | Singleton | 窗口枚举、内容捕获、diff、输入 |
| `TerminalTools` | Singleton | MCP 工具注册，JSON 序列化响应 |

### 线程安全

- `TerminalCaptureService` 使用 `ConcurrentDictionary<nint, string[]>` 存储 baselines，`ConcurrentDictionary<nint, TerminalInfo>` 缓存窗口信息
- 剪贴板访问受 `SemaphoreSlim(1, 1)` 保护，防止并发读写冲突
- `ClipboardService` 使用 5 秒超时的 STA 线程

## 代码规范

- 文件编码：`.cs` / `.xaml` 必须 UTF-8 with BOM，换行符 CRLF（见 `.editorconfig`）
- 缩进：4 空格
- 类成员声明顺序：常量 → 构造函数 → 私有字段 → 事件 → 公共字段 → 公共属性 → 公共方法 → 私有方法
- 私有字段使用 `_camelCase` 前缀
- null 检查优先使用 `is null` / `is not null`
- 参数验证优先使用 `ArgumentNullException.ThrowIfNull()` 等静态方法
- 集合初始化优先使用集合表达式 `[]`
- 每个文件只包含一个类/结构体或接口
- 方法参数中的枚举值无效时抛出 `InvalidEnumArgumentException`
- 异常消息使用英文
- 记录类型（record）用于简单数据模型，公共属性使用 `required` 修饰符
- 为 MCP 公共 API 参数添加 `[Description]` 特性
- 工具方法返回 JSON 字符串（通过 `JsonSerializer.Serialize` + `ToolResponse<T>`），不抛出异常到 MCP 层
