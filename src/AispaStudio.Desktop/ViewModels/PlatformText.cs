using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Texte zu Buchungen über Treatwell (ADR 0062): wie der Gast bezahlt und was die Rezeption noch tun muss.</summary>
public static class PlatformText
{
    /// <summary>„Vorab über Treatwell bezahlt: 35,00 €“ oder „Zahlt vor Ort“; <c>null</c>, wenn der Termin keine Plattformbuchung ist.</summary>
    public static string? Payment(PlatformPayment payment, decimal? prepaid, string? currency = null) => payment switch
    {
        PlatformPayment.Prepaid => prepaid is { } amount ? $"Vorab über Treatwell bezahlt: {Money.Format(amount, currency)}" : "Vorab über Treatwell bezahlt",
        PlatformPayment.OnSite => "Zahlt vor Ort",
        _ => null,
    };

    public const string AcceptInPlatform = "In Treatwell noch annehmen";

    public const string AcceptInPlatformHint = "Die Buchung steht im Kalender, gilt bei Treatwell aber erst, wenn Sie sie dort annehmen.";

    public const string AssignmentOpen = "Zuordnung offen: Der Gast ist vorläufig angelegt. Unter „Anfragen“ klären, wer das ist.";
}
