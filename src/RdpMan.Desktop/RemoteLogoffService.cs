using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RdpMan.Desktop;

[SupportedOSPlatform("windows")]
public static class RemoteLogoffService
{
    public static int LogOff(MachineEntry machine, CredentialProfile? credential)
    {
        var host = RdpConnectionProfile.ConnectionTargetHost(machine);
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Kein Zielhost für die Abmeldung angegeben.");
        }

        var (targetUser, targetDomain, targetUserAlias) = TargetIdentity(credential);

        if (string.IsNullOrWhiteSpace(targetUser))
        {
            throw new InvalidOperationException("Kein Benutzer für die Abmeldung ermittelbar.");
        }

        var server = WTSOpenServer(host);
        if (server == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Verbindung zum Terminaldienst auf \"{host}\" fehlgeschlagen.");
        }

        try
        {
            return LogOffMatchingSessions(server, host, targetUser, targetDomain, targetUserAlias);
        }
        finally
        {
            WTSCloseServer(server);
        }
    }

    private static (string User, string Domain, string Alias) TargetIdentity(CredentialProfile? credential)
    {
        var user = string.IsNullOrWhiteSpace(credential?.Username)
            ? Environment.UserName
            : credential.Username.Trim();
        var domain = string.IsNullOrWhiteSpace(credential?.Domain)
            ? (credential is null ? Environment.UserDomainName : "")
            : credential.Domain.Trim();
        var alias = "";

        var slashIndex = user.IndexOf('\\');
        if (slashIndex > 0 && slashIndex < user.Length - 1)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                domain = user[..slashIndex];
            }
            alias = user;
            user = user[(slashIndex + 1)..];
        }

        var atIndex = user.IndexOf('@');
        if (atIndex > 0)
        {
            alias = user;
            user = user[..atIndex];
        }

        return (user, domain, alias);
    }

    private static int LogOffMatchingSessions(IntPtr server, string host, string targetUser, string targetDomain, string targetUserAlias)
    {
        if (!WTSEnumerateSessions(server, 0, 1, out var sessionsPtr, out var sessionCount))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Sitzungen auf \"{host}\" konnten nicht gelesen werden.");
        }

        var loggedOff = 0;
        var errors = new List<string>();
        try
        {
            var itemSize = Marshal.SizeOf<WtsSessionInfo>();
            for (var index = 0; index < sessionCount; index++)
            {
                var itemPtr = IntPtr.Add(sessionsPtr, index * itemSize);
                var session = Marshal.PtrToStructure<WtsSessionInfo>(itemPtr);
                if (session.State == WtsConnectState.Listen)
                {
                    continue;
                }

                var user = QuerySessionString(server, session.SessionId, WtsInfoClass.UserName);
                if (string.IsNullOrWhiteSpace(user) || !UserMatches(user, targetUser, targetUserAlias))
                {
                    continue;
                }

                var domain = QuerySessionString(server, session.SessionId, WtsInfoClass.DomainName);
                if (!string.IsNullOrWhiteSpace(targetDomain) &&
                    !domain.Equals(targetDomain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (WTSLogoffSession(server, session.SessionId, wait: false))
                {
                    loggedOff++;
                    continue;
                }

                errors.Add($"{user} ({session.WinStationName}): {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            }
        }
        finally
        {
            WTSFreeMemory(sessionsPtr);
        }

        if (loggedOff == 0)
        {
            var user = string.IsNullOrWhiteSpace(targetDomain) ? targetUser : $@"{targetDomain}\{targetUser}";
            var suffix = errors.Count == 0 ? "" : $"{Environment.NewLine}{string.Join(Environment.NewLine, errors)}";
            throw new InvalidOperationException($"Keine passende Sitzung für \"{user}\" auf \"{host}\" gefunden.{suffix}");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return loggedOff;
    }

    private static bool UserMatches(string sessionUser, string targetUser, string targetUserAlias)
    {
        return sessionUser.Equals(targetUser, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(targetUserAlias) && sessionUser.Equals(targetUserAlias, StringComparison.OrdinalIgnoreCase));
    }

    private static string QuerySessionString(IntPtr server, int sessionId, WtsInfoClass infoClass)
    {
        if (!WTSQuerySessionInformation(server, sessionId, infoClass, out var buffer, out var bytesReturned) ||
            buffer == IntPtr.Zero ||
            bytesReturned == 0)
        {
            return "";
        }

        try
        {
            return Marshal.PtrToStringAuto(buffer)?.Trim() ?? "";
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr WTSOpenServer(string serverName);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern void WTSCloseServer(IntPtr serverHandle);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool WTSEnumerateSessions(
        IntPtr serverHandle,
        int reserved,
        int version,
        out IntPtr sessionInfo,
        out int count);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr serverHandle,
        int sessionId,
        WtsInfoClass infoClass,
        out IntPtr buffer,
        out int bytesReturned);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSLogoffSession(IntPtr serverHandle, int sessionId, bool wait);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WtsSessionInfo
    {
        public int SessionId;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string WinStationName;

        public WtsConnectState State;
    }

    private enum WtsInfoClass
    {
        UserName = 5,
        DomainName = 7,
    }

    private enum WtsConnectState
    {
        Active,
        Connected,
        ConnectQuery,
        Shadow,
        Disconnected,
        Idle,
        Listen,
        Reset,
        Down,
        Init,
    }
}
