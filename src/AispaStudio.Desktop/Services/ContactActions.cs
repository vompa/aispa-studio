namespace AispaStudio.Desktop.Services;

/// <summary>
/// Was die Kontaktzeilen eines Gastes am Rechner auslösen: einen Link öffnen (<c>tel:</c>, <c>mailto:</c>, <c>https://wa.me/…</c>; der Rechner nimmt die
/// App dafür) und Text in die Zwischenablage legen. Das Fenster stellt beides bereit; ohne Fenster (Tests) ersetzt es ein Doppel.
/// </summary>
public interface IContactActions
{
    Task LaunchAsync(Uri uri);

    Task CopyAsync(string text);
}
