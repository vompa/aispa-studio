using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Wiederherstellungsschlüssel für die Sicherung im Einzelplatz (ADR 0041, Schritt 5). <c>zugang.bin</c> ist mit DPAPI an das
/// Windows-Konto gebunden; geht der PC verloren, ist der Schlüssel für Gesundheitsdaten nur noch über diesen Code lesbar. Die App
/// zeigt den Code einmal, merkt sich nur den damit verschlüsselten Schlüssel und legt ihn neben jede Sicherung.
/// </summary>
public static class RecoveryKey
{
    /// <summary>Neuer Code aus 24 Zeichen in sechs Vierergruppen (rund 119 Bit), z. B. „K7QM-2XRT-…“.</summary>
    public static string NewCode() => KeySeal.NewCode();

    public static string Seal(string keyId, string healthKey, string code) => KeySeal.Seal(keyId, healthKey, code);

    /// <summary>Kennung und Schlüssel für Gesundheitsdaten; wirft, wenn der Code nicht passt oder die Datei beschädigt ist.</summary>
    public static (string KeyId, string Key) Open(string sealedJson, string code) =>
        KeySeal.TryOpen(sealedJson, code, out var keyId, out var key)
            ? (keyId, key)
            : throw new LocalServerException("Der Wiederherstellungsschlüssel passt nicht zu dieser Sicherung.");
}
