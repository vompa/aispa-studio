using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Die Sprache einer Nachricht in der Rückfrage: das Kürzel für die Liste („EN“) und der Name für Screenreader und Hinweis („Englisch“); Deutsch ist die Grundsprache.</summary>
public static class ClosureLanguage
{
    public static string Code(string? language) => (string.IsNullOrWhiteSpace(language) ? "de" : language).ToUpperInvariant();

    public static string Name(string? language) => language?.ToLowerInvariant() switch
    {
        null or "" or "de" => "Deutsch",
        { } code => GuestLanguages.Name(code) ?? code.ToUpperInvariant(),
    };
}

/// <summary>Ein Termin in der Rückfrage „Termine absagen?“: mit Häkchen, Gast, Zeit und Behandlung, Weg und Sprache der Nachricht und Hinweis auf Geld, das zurückgeht oder zu erstatten ist.</summary>
public sealed partial class ClosureAppointmentRow : ObservableObject
{
    public ClosureAppointmentRow(ClosureAppointmentDto appointment, TimeZoneInfo zone, Action changed, string? currency = null)
    {
        Appointment = appointment;
        _changed = changed;
        var start = TimeZoneInfo.ConvertTime(appointment.Start, zone);
        Guest = appointment.CustomerName;
        Detail = $"{DayLayout.ShortDay(DateOnly.FromDateTime(start.DateTime))} {start.ToString("HH:mm", CultureInfo.InvariantCulture)} · {appointment.TreatmentName}";
        ChannelText = ChannelName(appointment.Channel);
        LanguageCode = ClosureLanguage.Code(appointment.Language);
        LanguageName = ClosureLanguage.Name(appointment.Language);
        var deposit = appointment.DepositAmount is { } amount ? $"Anzahlung {Money.Format(amount, currency)} wird retourniert" : "Anzahlung zu erstatten";
        Hint = (appointment.HasDeposit, appointment.PrepaidOnPlatform) switch
        {
            (true, true) => $"{deposit} · bei Treatwell bezahlt",
            (true, false) => deposit,
            (false, true) => "bei Treatwell bezahlt",
            _ => null,
        };
    }

    private readonly Action _changed;

    public ClosureAppointmentDto Appointment { get; }

    public string Guest { get; }

    /// <summary>„Mo 8.3. 09:30 · Massage“.</summary>
    public string Detail { get; }

    /// <summary>„Mail“, „SMS“ oder „kein Kontakt“.</summary>
    public string ChannelText { get; }

    /// <summary>Kürzel der Sprache der Nachricht, „DE“ oder „EN“ (Ä46).</summary>
    public string LanguageCode { get; }

    public string LanguageName { get; }

    public string? Hint { get; }

    public bool HasHint => Hint is not null;

    /// <summary>Alles in einer Zeile für Screenreader: Gast, Zeit, Weg der Nachricht, Hinweis. Die Sprache nennt es nur, wenn sie nicht Deutsch ist.</summary>
    public string Name => $"{Guest}, {Detail}, {ChannelText}{(LanguageCode == "DE" ? "" : $", {LanguageName}")}{(Hint is null ? "" : $", {Hint}")}";

    /// <summary>Alle Termine sind zu Beginn gewählt (die Rückfrage sagt nichts ab, bevor jemand den roten Knopf drückt).</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;

    partial void OnIsCheckedChanged(bool value) => _changed();

    public static string ChannelName(ClosureChannel channel) => channel switch
    {
        ClosureChannel.Email => "Mail",
        ClosureChannel.Sms => "SMS",
        _ => "kein Kontakt",
    };
}

/// <summary>Ein Wartelisten-Eintrag in der Rückfrage (Ä48): sein Wunschzeitraum fällt in die Schließzeit; er bekommt eine kurze Nachricht in seiner Sprache und bleibt auf der Liste.</summary>
public sealed partial class ClosureWaitlistRow : ObservableObject
{
    public ClosureWaitlistRow(ClosureWaitlistDto entry, Action changed)
    {
        Entry = entry;
        _changed = changed;
        Guest = entry.CustomerName;
        var from = DayLayout.ShortDay(entry.From);
        Detail = $"Wunsch {(entry.From == entry.Until ? from : $"{from} bis {DayLayout.ShortDay(entry.Until)}")} · {entry.TreatmentName}";
        ChannelText = ClosureAppointmentRow.ChannelName(entry.Channel);
        LanguageCode = ClosureLanguage.Code(entry.Language);
        LanguageName = ClosureLanguage.Name(entry.Language);
    }

    private readonly Action _changed;

    public ClosureWaitlistDto Entry { get; }

    public string Guest { get; }

    /// <summary>„Wunsch Mo 8.3. bis Do 11.3. · Massage“.</summary>
    public string Detail { get; }

    public string ChannelText { get; }

    public string LanguageCode { get; }

    public string LanguageName { get; }

    public string Name => $"{Guest}, {Detail}, {ChannelText}{(LanguageCode == "DE" ? "" : $", {LanguageName}")}";

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;

    partial void OnIsCheckedChanged(bool value) => _changed();
}

/// <summary>
/// Die Rückfrage „Termine absagen?“ zu einer Schließzeit (ADR 0073): die offenen Termine mit Häkchen (alle oder einzelne), die Einträge der Warteliste im Zeitraum als eigener Abschnitt,
/// Betreff und Text der Nachricht (deutsche Vorlage je Art, bearbeitbar; Gäste mit einer anderen Sprache bekommen die Standardvorlage ihrer Sprache), auf Wunsch der Link zum Neubuchen und
/// vor dem Senden die Zahl der Nachrichten, je Sprache und die Anzahlungen, die über die Kassa zurückgehen. Ein Rückfragedialog nach Design-Brief 5.27 (zerstörende Aktion: „Abbrechen“
/// hat den Fokus), nur breiter und mit Liste. Nichts wird abgesagt oder gesendet, bevor der rote Knopf gedrückt ist; die KI formuliert höchstens auf Knopfdruck um und der
/// Text trägt dann beim Senden den Hinweis darauf. Wer fragt, liest nach der Antwort die Auswahl und die Texte aus diesem Objekt.
/// </summary>
public sealed partial class CancelClosureDialogViewModel : ConfirmDialogViewModel
{
    /// <summary>Höchstlängen wie am Server (<c>ClosureModel</c>).</summary>
    public const int MaxText = 2000;

    public const int MaxSubject = 200;

    private readonly Func<ReplyRevisionKind, string, Task<(string? Text, string? Problem)>> _revise;
    private readonly string? _currency;
    private bool _updating;

    public CancelClosureDialogViewModel(ClosureCancellationPreview preview, TimeZoneInfo zone, Action<ConfirmResult> answer,
        Func<ReplyRevisionKind, string, Task<(string? Text, string? Problem)>> revise, string? currency = null)
        : base(new ConfirmRequest("Termine absagen?",
            "Die gewählten Termine sind danach abgesagt („storniert durch Betrieb“), ohne Ausfallgebühr; eine geleistete Anzahlung geht über die Kassa zurück. Die Gäste bekommen die Nachricht unten in ihrer Sprache; vorher geht nichts hinaus.",
            "Termine absagen"), answer)
    {
        _revise = revise;
        _currency = currency;
        Rows = [.. preview.Appointments.Select(a => new ClosureAppointmentRow(a, zone, OnSelectionChanged, currency))];
        WaitlistRows = [.. (preview.Waitlist ?? []).Select(w => new ClosureWaitlistRow(w, OnSelectionChanged))];
        Subject = preview.SuggestedSubject;
        Text = preview.SuggestedText;
        AiAvailable = preview.AiAvailable;
        Refresh();
    }

    public IReadOnlyList<ClosureAppointmentRow> Rows { get; }

    /// <summary>Die Einträge der Warteliste, deren Wunschzeitraum in die Schließzeit fällt (Ä48).</summary>
    public IReadOnlyList<ClosureWaitlistRow> WaitlistRows { get; }

    public bool HasRows => Rows.Count > 0;

    public bool HasWaitlist => WaitlistRows.Count > 0;

    public bool IsEmpty => Rows.Count == 0 && WaitlistRows.Count == 0;

    public string EmptyText { get; } = "In der Schließzeit gibt es keinen offenen Termin und keinen Eintrag der Warteliste mehr.";

    public string ListLabel => $"Termine in der Schließzeit ({(Rows.Count == 1 ? "1 Termin" : $"{Rows.Count} Termine")})";

    public string WaitlistLabel => $"Warteliste ({WaitlistRows.Count})";

    /// <summary>Was die Nachricht an die Warteliste tut, in einem Satz unter der Überschrift.</summary>
    public string WaitlistHint { get; } = "Der Wunsch fällt in die Schließzeit. Die Gäste bekommen eine kurze Nachricht in ihrer Sprache und bleiben auf der Liste, sofern sie nicht abspringen.";

    /// <summary>Die KI kann umformulieren (Schlüssel eingerichtet).</summary>
    public bool AiAvailable { get; }

    [ObservableProperty]
    public partial string Subject { get; set; } = "";

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Umbuchung anbieten: die Nachricht endet mit dem Link in den Kundenbereich.</summary>
    [ObservableProperty]
    public partial bool OfferRebooking { get; set; }

    /// <summary>Die KI hat den Text umformuliert; die Nachricht trägt beim Senden den Hinweis darauf (KI-Pille am Feld).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextHint))]
    public partial bool AiWritten { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextHint), nameof(HasTextError))]
    public partial string? TextError { get; private set; }

    public bool HasTextError => TextError is not null;

    [ObservableProperty]
    public partial bool IsRevising { get; private set; }

    /// <summary>Alle Termine ausgewählt (Kontrollkästchen „Alle auswählen“).</summary>
    public bool AllChecked
    {
        get => Rows.Count > 0 && Rows.All(r => r.IsChecked);
        set
        {
            _updating = true;
            foreach (var row in Rows)
            {
                row.IsChecked = value;
            }

            _updating = false;
            Refresh();
        }
    }

    /// <summary>Alle Einträge der Warteliste ausgewählt.</summary>
    public bool AllWaitlistChecked
    {
        get => WaitlistRows.Count > 0 && WaitlistRows.All(r => r.IsChecked);
        set
        {
            _updating = true;
            foreach (var row in WaitlistRows)
            {
                row.IsChecked = value;
            }

            _updating = false;
            Refresh();
        }
    }

    /// <summary>Die gewählten Termine.</summary>
    public IReadOnlyList<ClosureAppointmentRow> Selected => [.. Rows.Where(r => r.IsChecked)];

    /// <summary>Die gewählten Einträge der Warteliste.</summary>
    public IReadOnlyList<ClosureWaitlistRow> SelectedWaitlist => [.. WaitlistRows.Where(r => r.IsChecked)];

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>„Nachrichten nach Sprache: 2 Deutsch, 1 Englisch.“; leer ohne Nachricht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLanguageSummary))]
    public partial string LanguageSummary { get; private set; } = "";

    public bool HasLanguageSummary => LanguageSummary.Length > 0;

    /// <summary>„Eine Anzahlung (30,00 €) geht über die Kassa zurück (Storno-Beleg, auf dem Zahlungsweg der Einzahlung).“; leer ohne Anzahlung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRefundSummary))]
    public partial string RefundSummary { get; private set; } = "";

    public bool HasRefundSummary => RefundSummary.Length > 0;

    [ObservableProperty]
    public partial string SendText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool CanSend { get; private set; }

    /// <summary>Hinweis unter dem Textfeld: wie die Platzhalter wirken, welche Sprache der Text hat oder dass die KI umformuliert hat; ein Fehler ersetzt ihn.</summary>
    public string TextHint => TextError
        ?? (AiWritten
            ? AiLabels.RevisedNotice
            : "Je Gast ersetzt der Server {Anrede}, {Behandlung} und {Zeit}. Der Text ist die deutsche Vorlage zur Art der Schließzeit und frei bearbeitbar; Gäste mit einer anderen Sprache "
                + "(Englisch, Französisch, Italienisch) bekommen die Standardvorlage ihrer Sprache. Eine Rückzahlung der Anzahlung ergänzt der Server dort, wo es eine gibt.");

    partial void OnTextChanged(string value) => Refresh();

    partial void OnSubjectChanged(string value) => Refresh();

    private void OnSelectionChanged()
    {
        if (!_updating)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var chosen = Selected;
        var waiting = SelectedWaitlist;
        var channels = chosen.Select(r => r.Appointment.Channel).Concat(waiting.Select(r => r.Entry.Channel)).ToList();
        Summary = chosen.Count == 0 && waiting.Count == 0 && !IsEmpty ? "Kein Termin gewählt." : Summarize(channels);
        LanguageSummary = SummarizeLanguages(chosen.Where(r => r.Appointment.Channel != ClosureChannel.None).Select(r => r.Appointment.Language)
            .Concat(waiting.Where(r => r.Entry.Channel != ClosureChannel.None).Select(r => r.Entry.Language)));
        RefundSummary = SummarizeRefunds(chosen.Select(r => r.Appointment.DepositAmount), _currency);
        SendText = (chosen.Count, waiting.Count) switch
        {
            (0, > 0) => waiting.Count == 1 ? "1 Eintrag benachrichtigen" : $"{waiting.Count} Einträge benachrichtigen",
            ( > 0, > 0) => $"{(chosen.Count == 1 ? "1 Termin absagen" : $"{chosen.Count} Termine absagen")}, {waiting.Count} benachrichtigen",
            (1, _) => "1 Termin absagen",
            _ => $"{chosen.Count} Termine absagen",
        };
        // Der Text gehört zu den Terminen: nur Einträge der Warteliste brauchen keinen.
        CanSend = chosen.Count > 0 ? Text.Trim().Length is > 0 and <= MaxText && Subject.Trim().Length is > 0 and <= MaxSubject : waiting.Count > 0;
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(AllWaitlistChecked));
    }

    /// <summary>„2 Nachrichten werden gesendet (1 Mail, 1 SMS). 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.“</summary>
    public static string Summarize(IEnumerable<ClosureChannel> channels)
    {
        var list = channels.ToList();
        var mail = list.Count(c => c == ClosureChannel.Email);
        var sms = list.Count(c => c == ClosureChannel.Sms);
        var none = list.Count - mail - sms;
        var total = mail + sms;
        var ways = string.Join(", ", new[] { mail > 0 ? $"{mail} Mail" : null, sms > 0 ? $"{sms} SMS" : null }.OfType<string>());
        var text = total == 0 ? "Es wird keine Nachricht gesendet." : $"{(total == 1 ? "1 Nachricht wird" : $"{total} Nachrichten werden")} gesendet ({ways}).";
        return none == 0 ? text : $"{text} {(none == 1 ? "1 Gast hat" : $"{none} Gäste haben")} keinen Kontakt und {(none == 1 ? "wird" : "werden")} nicht benachrichtigt.";
    }

    /// <summary>„Nachrichten nach Sprache: 2 Deutsch, 1 Englisch.“ in der Reihenfolge Deutsch, Englisch, Französisch, Italienisch; leer, wenn es keine Nachricht gibt.</summary>
    public static string SummarizeLanguages(IEnumerable<string?> languages)
    {
        var counts = languages.GroupBy(l => ClosureLanguage.Code(l)).ToDictionary(g => g.Key, g => g.Count());
        string[] order = ["DE", "EN", "FR", "IT"];
        var parts = order.Concat(counts.Keys.Except(order).Order(StringComparer.Ordinal)).Where(counts.ContainsKey)
            .Select(code => $"{counts[code]} {ClosureLanguage.Name(code)}");
        return counts.Count == 0 ? "" : $"Nachrichten nach Sprache: {string.Join(", ", parts)}.";
    }

    /// <summary>„Eine Anzahlung (30,00 €) geht über die Kassa zurück (Storno-Beleg, auf dem Zahlungsweg der Einzahlung).“ Leer ohne Anzahlung.</summary>
    public static string SummarizeRefunds(IEnumerable<decimal?> amounts, string? currency)
    {
        var refunds = amounts.OfType<decimal>().ToList();
        return refunds.Count switch
        {
            0 => "",
            1 => $"Eine Anzahlung ({Money.Format(refunds[0], currency)}) geht über die Kassa zurück (Storno-Beleg, auf dem Zahlungsweg der Einzahlung).",
            _ => $"{refunds.Count} Anzahlungen ({Money.Format(refunds.Sum(), currency)}) gehen über die Kassa zurück (je ein Storno-Beleg, auf dem Zahlungsweg der Einzahlung).",
        };
    }

    /// <summary>„Absagen“: antwortet mit „bestätigt“; die Auswahl und die Texte liest der Fragende aus diesem Objekt.</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send() => Answer(new ConfirmResult(ConfirmChoice.Confirmed));

    /// <summary>Lässt die KI den Text auf Knopfdruck umformulieren; verwirft der Server die Fassung, bleibt der Text, wie er war, und der Grund steht am Feld.</summary>
    [RelayCommand]
    private async Task ReviseAsync(string kind)
    {
        if (IsRevising || !Enum.TryParse<ReplyRevisionKind>(kind, out var revision))
        {
            return;
        }

        IsRevising = true;
        TextError = null;
        var (text, problem) = await _revise(revision, Text).ConfigureAwait(true);
        IsRevising = false;
        if (text is null)
        {
            TextError = problem ?? "Die KI konnte den Text nicht umformulieren.";
            return;
        }

        Text = text;
        AiWritten = true;
    }
}
