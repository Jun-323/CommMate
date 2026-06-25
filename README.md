# CommMate

[English](#english) | [中文](#chinese)

---

<h2 id="english">English</h2>

**CommMate** is an all-in-one serial & network debugging toolkit for Windows, built on WPF (.NET 10). It replaces a handful of separate tools with one app.

## Why CommMate?

- **Four tools in one** — Serial debugging, TCP/UDP networking, VT100 terminal emulation, and 500+ offline cheat sheets (ASCII, Git, Linux, ADB, Wi-Fi). No more juggling between Putty, SSCOM, a separate terminal, and a browser.
- **Real VT100 terminal** — Not a plain-text console. Full escape sequence support with 256-color XTerm extensions, alternate screen buffer, and keyboard forwarding. Test embedded Linux shells the way they actually render.
- **Smart framing** — *Timeout mode* accumulates bytes and flushes after silence (perfect for AT commands); *Streaming mode* forwards instantly (ideal for raw passthrough).
- **Flexible hex input** — Paste hex in any format: `41 54`, `4154`, `0x41, 0x54`, `41-54` — it just works.
- **Built-in cheat sheets** — 500+ commands always at your fingertips, offline. Select and Ctrl+C to copy.
- **Bilingual & themeable** — Chinese/English UI switchable on the fly. VS Code-inspired dark theme for late-night sessions.
- **Set-and-forget** — All configs auto-saved. Port auto-monitoring detects device removal and closes gracefully. Timed auto-send for repetitive tasks.

## Features at a Glance

| Module | Highlights |
|---|---|
| **Serial** | Baud 110–921600, 5/6/7/8 data bits, parity, flow control, hex/text TX/RX, quick commands (editable presets), timed auto-send, TX/RX counters, save-to-file |
| **Network** | TCP Client (optional local bind), TCP Server (multi-client, per-client disconnect), UDP (broadcast, auto reply routing) |
| **Terminal** | VT100/256-color XTerm over serial, 80×24 buffer, full escape sequences, keyboard forwarding (arrows, F1–F12, Home/End, etc.), local echo |
| **Reference** | ASCII (128 entries), Git (54 commands / 8 categories), Linux (106 commands / 9 categories), ADB (82 commands / 7 categories), Wi-Fi (78 commands / 4 categories) — all offline, bilingual |
| **UI** | Bilingual (EN/ZH), light & dark themes, status bar with live clock, config auto-persistence |

## Shortcuts

| Key | Where | Does |
|---|---|---|
| `Enter` | Send box | Send |
| `Ctrl+Enter` | Send box | Newline (no send) |
| `Ctrl+C` | Reference panel | Copy selection |
| Arrow keys, F1–F12, Home/End, Del, Tab, Esc, Backspace | Terminal | VT100 sequence |

## Requirements

This application depends on [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Please install it before running CommMate.

## Privacy

This app does not collect or upload any personal data. See the [Privacy Policy](https://jun-323.github.io/CommMate/privacy) for details.

---

<h2 id="chinese">中文</h2>

**CommMate** 是一款 Windows 平台 All-in-One 串口 & 网络调试工具，基于 WPF (.NET 10)，一个应用替代多个工具。

## 为什么选 CommMate？

- **四合一** — 集串口调试、TCP/UDP 网络通信、VT100 终端仿真、500+ 离线速查命令（ASCII / Git / Linux / ADB / Wi-Fi）于一体。告别在 SSCOM、Putty、终端模拟器和浏览器之间来回切换。
- **真正的 VT100 终端** — 不是纯文本控制台。完整转义序列 + 256 色 XTerm 扩展 + 备用屏幕缓冲区 + 键盘转发。以实际渲染方式测试嵌入式 Linux Shell。
- **智能分帧** — *超时模式*静默期后整包输出（AT 指令利器）；*流式模式*即时转发（透传场景首选）。
- **Hex 输入百搭** — `41 54`、`4154`、`0x41, 0x54`、`41-54` 随意粘贴，自动识别。
- **离线速查手册** — 500+ 条常用命令随时查阅，选中即 Ctrl+C 复制，无需联网。
- **双语 & 可换肤** — 中英文界面随时切换，VS Code 风格暗色主题适合深夜调试。
- **一次配置，长久省心** — 所有配置自动保存，设备拔出自动检测并关闭端口，定时发送帮你处理重复任务。

## 功能速览

| 模块 | 亮点 |
|---|---|
| **串口** | 波特率 110–921600，5/6/7/8 数据位，校验，流控，Hex/文本收发，快捷命令（可编辑预设），定时发送，收发计数，日志保存 |
| **网络** | TCP 客户端（可选本地绑定），TCP 服务器（多客户端管理，逐客户端断开），UDP（广播，自动回复路由） |
| **终端** | 基于串口的 VT100 / 256 色 XTerm，80×24 缓冲区，完整转义序列，键盘转发（方向键、F1–F12 等），本地回显 |
| **速查** | ASCII（128 条）、Git（54 条 / 8 类）、Linux（106 条 / 9 类）、ADB（82 条 / 7 类）、Wi-Fi（78 条 / 4 类）— 全离线、双语 |
| **界面** | 中英双语，亮/暗主题，状态栏实时时钟，配置自动持久化 |

## 快捷键

| 快捷键 | 场景 | 功能 |
|---|---|---|
| `Enter` | 发送框 | 发送 |
| `Ctrl+Enter` | 发送框 | 换行（不发送） |
| `Ctrl+C` | 速查面板 | 复制选中 |
| 方向键、F1–F12、Home/End、Del、Tab、Esc、Backspace | 终端面板 | VT100 序列 |

## 运行要求

本应用依赖 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)，运行前请先安装。

## 隐私策略

本应用不收集、不上传任何个人数据。详见 [隐私策略](https://jun-323.github.io/CommMate/privacy)。
