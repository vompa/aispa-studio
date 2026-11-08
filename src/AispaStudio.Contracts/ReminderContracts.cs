namespace AispaStudio.Contracts;

/// <summary>Kanal für Terminerinnerungen. Terminerinnerungen sind keine Werbung und brauchen keine Einwilligung (§ 7 UWG, § 174 TKG).</summary>
public enum ReminderChannel
{
    Email,
    Sms,
    WhatsApp,
}

/// <param name="FirstHoursBefore">Erste Erinnerung, Stunden vor Beginn (0 = aus).</param>
/// <param name="SecondHoursBefore">Zweite Erinnerung, Stunden vor Beginn (0 = aus).</param>
/// <param name="WhatsAppTemplate">Freigegebene Vorlage (Meta: Name, Twilio: Content-SID). Platzhalter: {{1}} Vorname, {{2}} Behandlung, {{3}} Datum und Uhrzeit, {{4}} Standort, {{5}} Link zum Termin.</param>
public sealed record ReminderSettingsDto(
    bool Enabled,
    int FirstHoursBefore,
    int SecondHoursBefore,
    ReminderChannel Channel,
    string? WhatsAppTemplate,
    string WhatsAppTemplateLanguage);

/// <summary>Stand der Erinnerungen eines Termins (im Termin sichtbar).</summary>
public sealed record AppointmentReminderDto(int Stage, ReminderChannel Channel, string State, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, string? Error);
