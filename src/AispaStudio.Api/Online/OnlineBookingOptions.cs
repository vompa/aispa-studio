namespace AispaStudio.Api.Online;

/// <summary>
/// Regeln des Kundenbereichs (Abschnitt <c>OnlineBooking</c> in der Konfiguration). Vorlauf, Horizont, Stornofrist und Raster
/// sind die Vorgaben; jeder Standort kann in den Stammdaten eigene Werte setzen (<see cref="OnlineRules"/>).
/// </summary>
public sealed class OnlineBookingOptions
{
    public const string Section = "OnlineBooking";

    /// <summary>Öffentliche Adresse des Kundenbereichs für Links in Mails, z. B. „https://buchen.mein-spa.at“.</summary>
    public string PublicUrl { get; set; } = "http://localhost:5173";

    /// <summary>So viele Minuten vor Beginn ist der späteste Online-Termin buchbar.</summary>
    public int MinLeadMinutes { get; set; } = 120;

    /// <summary>So viele Tage im Voraus ist online buchbar.</summary>
    public int MaxDaysAhead { get; set; } = 90;

    /// <summary>Bis so viele Stunden vor Beginn können Kundinnen und Kunden online stornieren.</summary>
    public int CancellationDeadlineHours { get; set; } = 24;

    /// <summary>
    /// So oft darf ein Gast einen Termin selbst online verschieben, innerhalb der Stornofrist; danach nur noch über die Rezeption.
    /// 0 schaltet das Verschieben im Kundenbereich ab.
    /// </summary>
    public int MaxGuestMoves { get; set; } = 2;

    /// <summary>Ausfallgebühr in Prozent des Preises für Standorte ohne eigene Regel; 0 = keine Kartenhinterlegung.</summary>
    public int NoShowFeePercent { get; set; }

    /// <summary>
    /// So viele Tage nach dem Ende des Termins lässt sich die Ausfallgebühr noch belasten; danach wird die hinterlegte Karte beim
    /// Zahlungsanbieter gelöscht.
    /// </summary>
    public int CardRetentionDays { get; set; } = 14;

    /// <summary>Vorgabe für Standorte ohne eigene Regel: Ausfallgebühr nur von Gästen ohne abgeschlossene Behandlung im Spa verlangen.</summary>
    public bool NoShowFeeNewCustomersOnly { get; set; }

    /// <summary>
    /// Vorgabe für Standorte ohne eigene Regel: Termin freigeben, wenn so viele Stunden nach dem Buchen keine Karte hinterlegt oder
    /// keine Anzahlung bezahlt ist (0 = nie).
    /// </summary>
    public int CardHoldHours { get; set; }

    /// <summary>
    /// Vorgabe für Standorte ohne eigene Regel: Anzahlung beim Online-Buchen in Prozent des Preises (0 = keine, 100 = Vorauszahlung).
    /// Sie ersetzt die Kartenhinterlegung und braucht einen Zahlungsanbieter und eine aktive Kasse für den Anzahlungsbeleg.
    /// </summary>
    public int DepositPercent { get; set; }

    /// <summary>Wie oft nach Online-Terminen ohne Karte nach Ablauf der Frist gesucht wird, in Sekunden (0 = gar nicht).</summary>
    public int CardHoldPollSeconds { get; set; } = 300;

    /// <summary>Wie oft nach bezahlten Online-Zahlungen ohne Beleg, Gutschein oder Erstattung gesucht wird, in Sekunden (0 = gar nicht).</summary>
    public int PaymentRecoveryPollSeconds { get; set; } = 600;

    /// <summary>Wie oft nach Karten mit abgelaufener Frist gesucht wird, in Sekunden (0 = gar nicht).</summary>
    public int CardCleanupPollSeconds { get; set; } = 3600;

    /// <summary>Raster der angebotenen Startzeiten in Minuten.</summary>
    public int SlotStepMinutes { get; set; } = 15;

    /// <summary>Version des Datenschutzhinweises, der bei der Buchung bestätigt wird.</summary>
    public string PrivacyNoticeVersion { get; set; } = "2026-10";

    /// <summary>Ein Anamnesebogen gilt so viele Monate; danach wird vor dem nächsten Termin ein neuer erbeten.</summary>
    public int AnamnesisValidMonths { get; set; } = 12;

    /// <summary>Höchstzahl der Anfragen je IP-Adresse und Minute an Anmeldecode und Buchung (Schutz vor Missbrauch).</summary>
    public int SensitiveRequestsPerMinute { get; set; } = 10;

    /// <summary>Höchstzahl der lesenden Anfragen je IP-Adresse und Minute.</summary>
    public int PublicRequestsPerMinute { get; set; } = 300;

    /// <summary>Anmeldung mit Google; aktiv, wenn ClientId und ClientSecret gesetzt sind.</summary>
    public ExternalProviderOptions Google { get; set; } = new();

    /// <summary>Anmeldung mit Apple; aktiv, wenn ClientId, TeamId, KeyId und PrivateKey gesetzt sind.</summary>
    public AppleProviderOptions Apple { get; set; } = new();
}

public class ExternalProviderOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public virtual bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class AppleProviderOptions : ExternalProviderOptions
{
    public string? TeamId { get; set; }
    public string? KeyId { get; set; }

    /// <summary>Inhalt der .p8-Datei von Apple (PEM).</summary>
    public string? PrivateKey { get; set; }

    public override bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(TeamId) &&
        !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(PrivateKey);
}
