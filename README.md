# CommMate

[English](#english) | [中文](#chinese)

---

<h2 id="english">English</h2>

CommMate is a WPF-based serial port and network debugging tool for Windows, combining serial communication, TCP/UDP networking, a VT100 terminal emulator, and offline cheat sheets into a single application.

## Features

- **Serial Port** — COM port communication with configurable baud rate, data/stop bits, parity, flow control, and framing mode (Timeout / Streaming). Supports hex and text send/receive, timed auto-send, quick command buttons, and TX/RX byte counters.
- **Network** — TCP Client, TCP Server (multi-client), and UDP modes. Supports local IP binding, broadcast, client management, and full data logging.
- **Terminal** — VT100-compatible terminal emulator over serial. Handles escape sequences (cursor movement, screen clearing, title changes), local echo, and keyboard input forwarding.
- **Reference** — Offline lookup tables for ASCII, Git, Linux, Android ADB, and Wi-Fi debugging commands with bilingual descriptions.
- **Internationalization** — Chinese and English UI, switchable at any time.
- **Themes** — Light and dark themes with a VS Code-inspired dark palette.
- **Config Persistence** — Settings auto-saved to and loaded from a JSON file.

---

<h2 id="chinese">中文</h2>

CommMate 是一款基于 WPF 的 Windows 串口与网络调试工具，集串口通信、TCP/UDP 网络通信、VT100 终端仿真和离线速查表于一体。

## 功能特性

- **串口助手** — COM 口通信，支持波特率、数据位、停止位、校验位、流控、分帧模式（超时/流式）配置。支持 Hex / 文本收发、定时自动发送、快捷命令按钮、TX/RX 字节计数。
- **网络助手** — TCP 客户端、TCP 服务器（多客户端）、UDP 三种模式。支持本地 IP 绑定、广播、客户端管理及完整数据日志。
- **终端仿真** — 基于串口的 VT100 兼容终端。支持转义序列（光标移动、清屏、标题变更）、本地回显和键盘输入转发。
- **速查手册** — 离线速查表，涵盖 ASCII 码表、Git、Linux、Android ADB、Wi-Fi 调试命令，中英双语描述。
- **国际化** — 完整中英文界面，可随时切换。
- **主题切换** — 亮色 / 暗色主题，暗色采用 VS Code 风格配色。
- **配置持久化** — 所有设置自动保存到 JSON 文件并在启动时加载。
