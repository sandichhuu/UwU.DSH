# DSH Launcher — DeepSeek Harness + UwU X-Proxy

A WPF (WebView2) wrapper that shows DeepSeek Harness and UwU X-Proxy side by side in one window, with two tabs in the header.
On startup the app auto-installs / auto-updates everything it needs — no manual commands.

## How it works

- Tab `⚡ Deepseek Harness` loads `http://127.0.0.1:3080/` (default tab, auto-navigates to the `?token=` URL once DSH prints it).
- Tab `✖️ X-Proxy` lazy-loads `http://localhost:3081/` only when you open it.
- On launch the app checks `node -v`. If Node.js is missing, it shows a download dialog pointing to https://nodejs.org/ and exits.
- It auto-creates `settings.ini` next to the exe on first launch:
  - `[Startup]` commands run sequentially and wait for completion (update/install step).
  - `[Services]` commands run concurrently in the background for the whole session.
  - Default startup: `npm config set allow-scripts=better-sqlite3 --location=user`, `npm install -g uwu-x-proxy@latest`, `npx -y @deepseek-ai/dsh plugin --profile web add dshmarket`.
  - Default services: `call npx uwu-x-proxy`, `call npx -y @deepseek-ai/dsh web --no-open`.
  - You can add `command4`, `service3`, … — they are picked up automatically. Lines starting with `;`, `#` or `//` are ignored.
- Header buttons: `🔄 Reload`, `🛑 Stop Services`, `⚡ Restart Services`, `⚙️ Settings` (opens `settings.ini`), `📋 Log` (opens `lastsession.log`). The footer shows live status plus `DSH: 3080 | X-Proxy: 3081` and lets you collapse the header.
- All stdout/stderr is logged to `lastsession.log` next to the exe.

Using packages:
- [Deepseek Harness](https://github.com/deepseek-ai/deepseek-harness)
- [UwU X-Proxy](https://github.com/sandichhuu/uwu-x-proxy) (`uwu-x-proxy@latest` via npm)

> Note: older versions of this README mentioned `antigravity-claude-proxy` / `codex-claude-proxy` and ports `8080`/`8081`. The current code uses `uwu-x-proxy` with ports `3080` (DSH) and `3081` (X-Proxy).

---

## Requirements

- Windows x64
- Node.js (LTS from https://nodejs.org/)
- Ports **3080** and **3081** free
- WebView2 Runtime (preinstalled on most Windows 10/11; otherwise installed with Edge)

## Installation

> First, make sure ports 3080 and 3081 are free.
> Then just download the exe from Release and run it in an empty folder (`settings.ini` and `lastsession.log` are created beside the exe).

- `windows_x86_64\DshAntigravityLauncher-Standalone.exe` (~141 MB) — self-contained, no .NET install needed.
- `windows_x86_64\DshAntigravityLauncher-Lightweight.exe` (~1.4 MB) — requires .NET 10 Desktop Runtime.

## Build from source

Requires [.NET 10 SDK](https://aka.ms/dotnet/download). From the project root:

```bat
build.bat
```

This runs (see `build.bat`):
```bat
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "Release\_staging_standalone"
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "Release\_staging_lightweight"
```

For dev run without building: `dotnet run --project DshAntigravityLauncher.csproj` (or `run.bat`).

## Usage Guide

### Connect Google Account
<img width="1919" height="1019" alt="image" src="https://github.com/user-attachments/assets/93b8e4b8-ae1e-4a06-a1e7-602acd7dc994" />

### Fetch model list  
<img width="1919" height="1022" alt="image" src="https://github.com/user-attachments/assets/0f2bb7b3-276b-4b27-a2b5-ae1b1e8e6d0c" />  

### Turn to Anthropic protocol  
<img width="1919" height="1023" alt="image" src="https://github.com/user-attachments/assets/1126fca8-ba14-45c5-a2e0-563d547e234c" />  

### Select model and enjoy  
<img width="1919" height="1021" alt="image" src="https://github.com/user-attachments/assets/afd41adf-c132-47fc-a833-3d95c3e9b94f" />

---

