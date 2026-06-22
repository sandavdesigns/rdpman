using System.Security.Cryptography;
using System.Text;

namespace RdpMan.Desktop;

public static class CredentialVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RDP Man credential profile v1");

    public static string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedText)
    {
        var bytes = Convert.FromBase64String(protectedText);
        var plainBytes = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}

