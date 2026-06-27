using System.Security.Cryptography;
using System.Text;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Platform.Windows.Security;

/// <summary>
/// <see cref="ISecretStore"/> backed by Windows DPAPI (Data Protection API). Each secret is
/// encrypted with the current user's credentials and written as an opaque blob — API keys are
/// never stored in plain text and can only be read back by the same Windows user.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _dir;

    public DpapiSecretStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslator", "secrets");
        Directory.CreateDirectory(_dir);
    }

    public void Save(string key, string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        var blob = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(key), blob);
    }

    public string? Load(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        try
        {
            var blob = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(blob, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null; // corrupt or written by a different user
        }
    }

    public void Delete(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
    }

    public bool Has(string key) => File.Exists(PathFor(key));

    private string PathFor(string key)
    {
        // Hash the key into a filesystem-safe name.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(_dir, Convert.ToHexString(hash, 0, 16) + ".bin");
    }
}
