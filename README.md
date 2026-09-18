# RDPMan

RDPMan is a native Windows desktop application for managing and switching between RDP and SSH sessions. It is intentionally not a browser app: the right side hosts real Microsoft RDP ActiveX sessions or an embedded SSH terminal so the application can switch between open machines like a classic remote connection manager.

## Features

- Modern WinForms desktop UI with a machine sidebar and embedded RDP workspace
- Multiple open RDP sessions with sidebar switching
- Embedded SSH terminal for Linux and Unix hosts with persistent session switching
- Password-based SSH authentication and configurable SSH ports
- Configurable SSH terminal text color with a Matrix-green default
- SSH copy and paste toolbar, context menu, and keyboard shortcuts
- ANSI-aware SSH terminal cursor, history, arrow-key, and backspace handling
- Connect, connect as, reconnect, disconnect, remove entry, and continuous ping actions
- Ad hoc RDP and SSH sessions that exist only while connected
- Instant machine search across name, host, group, and notes
- Per-machine colors, favorites, groups, notes, and hover tooltips
- Central machine groups with inherited colors and credentials
- Active session badges and connected-state color indicators
- Local credential profiles with DPAPI-protected passwords
- SSH host-key fingerprint verification with change warnings
- Global, per-group, and per-machine credential assignment
- Per-machine RDP redirection controls for clipboard, printers, smartcards, and WebAuthn
- Active Directory import by LDAP path / OU
- CSV import for exported AD computer lists
- Backup and restore for machines, credentials, and reconnect state
- Automatic restore of sessions that were open before closing the app
- Local JSON data store under `%APPDATA%\RDP Man` for compatibility with earlier builds

## Build Requirements

- Windows 10/11
- .NET 8 SDK
- Microsoft Remote Desktop Client components, included on normal Windows installations

## Download Ready Build

GitHub Actions builds a Windows ZIP package on every push to `main`.

1. Open the GitHub repository.
2. Open the `builds/` folder.
3. Download `RDPMan-latest-win-x64.zip` or the newest versioned ZIP.
4. Extract the ZIP and start `RDPMan.exe`.

You can also download the build from **Actions**:

1. Go to **Actions**.
2. Open the latest **Build Windows App** run.
3. Download the `RDPMan-<version>-win-x64` artifact.

Tagged versions like `v0.2.5` also create a GitHub Release with the ZIP attached.

The build intentionally loads the Microsoft RDP ActiveX control at runtime from
the Windows registry. This avoids generated COM wrapper files in CI while still
using the real Windows RDP client component on the target machine.

Build:

```powershell
dotnet build .\RdpMan.Desktop.sln
```

Run:

```powershell
dotnet run --project .\src\RdpMan.Desktop\RdpMan.Desktop.csproj
```

## Security Notes

Passwords are protected with Windows DPAPI for the current Windows user. The local data file can move with the machine list, but encrypted passwords are only decryptable by the same Windows user profile unless the credential is re-entered.

## Current Architecture

The app is local-first. There is no web backend and no Portainer deployment. AD import talks directly to Active Directory from the Windows desktop app.
