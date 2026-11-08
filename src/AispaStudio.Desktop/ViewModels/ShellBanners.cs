namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Welche Banner die Shell zeigt: höchstens zwei gleichzeitig, der Abgleichfehler vor den Warnungen „Offline“ und „Sicherung“;
/// was nicht passt, nennt eine Zeile „n weitere“ (Design-Brief 5.15).
/// </summary>
public readonly record struct ShellBanners(bool Failed, bool Offline, bool Backup, string? HiddenText)
{
    public const int MaxVisible = 2;

    public static ShellBanners For(bool failed, bool offline, bool backup, bool showAll)
    {
        var pending = new[] { failed, offline, backup };
        var limit = showAll ? pending.Length : MaxVisible;
        var visible = new bool[pending.Length];
        var shown = 0;
        for (var i = 0; i < pending.Length && shown < limit; i++)
        {
            if (pending[i])
            {
                visible[i] = true;
                shown++;
            }
        }

        var hidden = pending.Count(p => p) - shown;
        return new ShellBanners(visible[0], visible[1], visible[2], hidden switch
        {
            0 => null,
            1 => "1 weitere Meldung",
            _ => $"{hidden} weitere Meldungen",
        });
    }
}
