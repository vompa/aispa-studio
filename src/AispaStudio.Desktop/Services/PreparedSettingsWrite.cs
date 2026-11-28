namespace AispaStudio.Desktop.Services;

internal interface IPreparedSettingsStore
{
    PreparedSettingsWrite Prepare(Func<DesktopSettings, DesktopSettings> update);
}

internal sealed class PreparedSettingsWrite(Action publish, Action confirm, Action? discard = null) : IDisposable
{
    public void CommitAndVerify()
    {
        publish();
        confirm();
    }

    internal static void Verify(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new IOException("Der gespeicherte Inhalt der Einstellungen stimmt nicht mit dem vorbereiteten Stand überein.");
        }
    }

    internal static void Discard(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die aktive Datei bleibt unabhängig von diesem temporären Rest lesbar.
        }
    }

    public void Dispose() => discard?.Invoke();
}
