using AispaStudio.Contracts;

namespace AispaStudio.Api.Sync;

/// <summary>Wer oder was eine Änderung ausgelöst hat, als Satzteil des Verlaufs („von Lena“, „per SMS“, „online vom Gast“ …); gemeinsam für alle Masken (ADR 0082).</summary>
internal static class HistoryActor
{
    public const string DeletedPerson = "einer gelöschten Person";

    public static string Text(ChangeChannel channel, string? userId, IReadOnlyDictionary<string, string> people) => channel switch
    {
        ChangeChannel.Team when userId is not null => $"von {Person(userId, people)}",
        ChangeChannel.Team => "vom Team",
        ChangeChannel.StaffApp when userId is not null => $"in der Mitarbeiter-App von {Person(userId, people)}",
        ChangeChannel.StaffApp => "in der Mitarbeiter-App",
        ChangeChannel.Online => "online vom Gast",
        ChangeChannel.Sms => "per SMS",
        ChangeChannel.Mail => "per Mail",
        ChangeChannel.WhatsApp => "per WhatsApp",
        ChangeChannel.Phone => "per Telefon",
        ChangeChannel.Treatwell => "über Treatwell",
        ChangeChannel.Ai => "von der KI",
        ChangeChannel.Sync => "beim Abgleich",
        ChangeChannel.Import when userId is not null => $"beim Import von {Person(userId, people)}",
        ChangeChannel.Import => "beim Import",
        _ => "automatisch",
    };

    public static string Person(string userId, IReadOnlyDictionary<string, string> people) =>
        people.TryGetValue(userId, out var name) ? name : DeletedPerson;

    public static ChangeChannel Channel(string channel) => Enum.TryParse<ChangeChannel>(channel, out var parsed) ? parsed : ChangeChannel.Job;
}
