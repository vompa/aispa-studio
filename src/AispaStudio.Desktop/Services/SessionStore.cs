using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Unter Windows im Benutzerprofil, verschlüsselt mit DPAPI: nur dasselbe Windows-Konto auf demselben PC kann es lesen.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSessionStore(string fileName = "anmeldung.bin") : ISessionStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", fileName);

    public string? Load()
    {
        try
        {
            return File.Exists(_filePath)
                ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_filePath), null, DataProtectionScope.CurrentUser))
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Save(string cookie)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllBytes(_filePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie), null, DataProtectionScope.CurrentUser));
    }

    public void Clear() => File.Delete(_filePath);
}
