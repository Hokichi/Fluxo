using System.Security.Cryptography;
using System.Text;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Ui;

public sealed class DpapiUiLockPasswordProtector : IUiLockPasswordProtector
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Fluxo.UI.LockingPassword.v1");

    public string Protect(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
            return string.Empty;

        if (!protectedPassword.StartsWith(Prefix, StringComparison.Ordinal))
            return string.Empty;

        try
        {
            var payload = protectedPassword[Prefix.Length..];
            var protectedBytes = Convert.FromBase64String(payload);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
