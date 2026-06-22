# RDP Man

RDP Man is a native Windows desktop application for managing and switching between RDP sessions. It is intentionally not a browser app: the right side of the window hosts real Microsoft RDP ActiveX sessions so the application can switch between open machines like a classic RDP manager.

## Planned Scope

- Left sidebar with managed machines
- Embedded RDP view on the right
- Switch between already connected sessions by selecting machines
- Connect, reconnect, disconnect, and ping actions
- Local credential profiles with DPAPI-protected passwords
- Per-machine default credential assignment
- Active Directory import by LDAP path / OU
- Local JSON data store under `%APPDATA%\RDP Man`

## Build Requirements

- Windows 10/11
- .NET 8 SDK
- Microsoft Remote Desktop Client components, included on normal Windows installations

## Download Ready Build

GitHub Actions builds a Windows ZIP package on every push to `main`.

1. Open the GitHub repository.
2. Go to **Actions**.
3. Open the latest **Build Windows App** run.
4. Download the `RdpMan-win-x64` artifact.
5. Extract the ZIP and start `RdpMan.exe`.

Tagged versions like `v0.1.0` also create a GitHub Release with the ZIP attached.

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
