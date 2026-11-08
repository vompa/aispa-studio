using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// KI-Anfrage im Kalender (Strg+K): Wunsch am Telefon frei eintippen, daraus werden Bausteine (Tag, Uhrzeit, Behandlung, Person, Raum, Zahl der Gäste,
/// Ausschlüsse) und mindestens drei konkrete Vorschläge (Tag, Uhrzeit, Person, Raum). Ein Baustein lässt sich entfernen, ein Nachsatz („lieber Donnerstag“)
/// ergänzt den Wunsch. Fehlt die Behandlung, zeigt die Leiste die passendsten als Karten und alle Behandlungen nach Gruppe; Rückfragen
/// („Für eine oder zwei Personen?“) kommen als Knöpfe. Ein gewählter Gast wird mit der Buchung übernommen.
/// Die KI liest nur den Text; Zeiten, Reihenfolge und Preise rechnet der Server. Gebucht wird erst auf Klick.
/// </summary>
[AiOutput(AiFunctions.Request)]
public partial class CalendarRequestViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly LocationResources _resources;
    private readonly DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly Func<CalendarProposal, CustomerDto?, Task> _book;
    private readonly Action _close;
    private readonly Action? _searchFreeTime;
    private readonly List<DateTimeOffset> _rejected = [];
    private CalendarWish? _wish;
    private int _version;

    public CalendarRequestViewModel(IWorkstationApi api, LocationResources resources, DateOnly date, TimeZoneInfo zone,
        Func<CalendarProposal, CustomerDto?, Task> book, Action close, Action? searchFreeTime = null)
    {
        _searchFreeTime = searchFreeTime;
        _api = api;
        _resources = resources;
        _date = date;
        _zone = zone;
        _book = book;
        _close = close;
        Guest = new CustomerSearchViewModel(api);
        Guest.Changed += () => GuestCustomer = Guest.Customer;
        TreatmentGroups = [.. resources.Treatments.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Weitere" : t.Category!)
            .OrderBy(g => g.Key == "Weitere" ? 1 : 0).ThenBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new TreatmentGroup(g.Key, [.. g.OrderBy(t => t.Name, StringComparer.CurrentCulture).Select(CardOf)]))];
    }

    /// <summary>Wunsch oder Nachsatz; nach dem Senden leer, die Bausteine tragen den Wunsch weiter.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Gast, für den gebucht wird (Kundensuche wie sonst); ohne Gast öffnet die Buchung „Neuer Termin“ ohne Gast.</summary>
    public CustomerSearchViewModel Guest { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookText))]
    public partial CustomerDto? GuestCustomer { get; private set; }

    /// <summary>Hauptknopf: „Buchen (Enter)“ bzw. mit Gast „Für Brigitte Müller buchen (Enter)“.</summary>
    public string BookText => GuestCustomer is { } guest ? $"Für {guest.InHouseName} buchen (Enter)" : "Buchen (Enter)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChips))]
    public partial IReadOnlyList<WishChip> Chips { get; private set; } = [];

    public bool HasChips => Chips.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProposals), nameof(Active))]
    public partial IReadOnlyList<ProposalRow> Proposals { get; private set; } = [];

    public bool HasProposals => Proposals.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Active))]
    public partial int ActiveIndex { get; private set; } = -1;

    public ProposalRow? Active => ActiveIndex >= 0 && ActiveIndex < Proposals.Count ? Proposals[ActiveIndex] : null;

    /// <summary>Zusammenfassung oder Rückfrage des Servers.</summary>
    [ObservableProperty]
    public partial string? Answer { get; private set; }

    /// <summary>Hinweis auf die KI (AI Act Art. 50), steht unter jeder Antwort.</summary>
    [ObservableProperty]
    public partial string? Disclosure { get; private set; }

    /// <summary>Die passendsten Behandlungen als Karten, solange der Wunsch keine nennt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsTreatment))]
    public partial IReadOnlyList<TreatmentCard> TreatmentCards { get; private set; } = [];

    /// <summary>Alle Behandlungen nach Gruppe, aufklappbar.</summary>
    public IReadOnlyList<TreatmentGroup> TreatmentGroups { get; }

    /// <summary>Der Wunsch nennt noch keine Behandlung: die Leiste bietet Karten und die Liste aller Behandlungen an.</summary>
    public bool NeedsTreatment => TreatmentCards.Count > 0 || _askedWithoutTreatment;

    private bool _askedWithoutTreatment;

    /// <summary>Rückfragen des Servers mit ihren Antworten als Knöpfe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuestions))]
    public partial IReadOnlyList<RequestQuestion> Questions { get; private set; } = [];

    public bool HasQuestions => Questions.Count > 0;

    /// <summary>„Bereitgestellt wird: Tee, Früchteteller“ zur gewählten Behandlung, nur als Hinweis.</summary>
    [ObservableProperty]
    public partial string? ExtrasText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial bool Busy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Wechselt zur Suche nach freier Zeit ohne KI.</summary>
    [RelayCommand]
    private void SearchFreeTime() => _searchFreeTime?.Invoke();

    public string Hint => Busy ? "Vorschläge werden gesucht …"
        : HasProposals ? "Enter bucht den ersten, Strg+2 und Strg+3 die anderen. Ein Nachsatz wie „lieber Donnerstag“ ergänzt den Wunsch."
        : "Zum Beispiel: „Massage morgen 17 Uhr, nicht bei Marco“. Enter sucht.";

    private TreatmentCard CardOf(TreatmentDto treatment)
    {
        var rooms = _resources.Rooms.Where(r => !r.IsArea && treatment.RoomIds.Contains(r.Id)).ToList();
        var kind = rooms.Any(r => r.Places >= 2) ? (rooms.Any(r => r.Places < 2) ? "Einzel- und Paarraum" : "Paarraum") : "Einzelraum";
        return new TreatmentCard(treatment.Id, treatment.Name, $"{treatment.DurationMinutes} Min. · {Money.Format(treatment.Price, _resources.Location.Currency)}", kind);
    }

    /// <summary>Enter im Eingabefeld: neuer Text wird gesendet, ohne Text wird der gewählte Vorschlag gebucht.</summary>
    public async Task SubmitAsync()
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            await AskAsync(Text.Trim(), _wish).ConfigureAwait(true);
        }
        else
        {
            await BookActiveAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Baustein entfernen; der Wunsch rechnet sich ohne ihn neu. Der Tag bleibt immer.</summary>
    public async Task RemoveAsync(WishChip chip)
    {
        if (_wish is not { } wish)
        {
            return;
        }

        var changed = chip.Chip.Part switch
        {
            CalendarWishPart.Time => wish with { From = null, Until = null },
            CalendarWishPart.Treatment => wish with { TreatmentId = null },
            CalendarWishPart.Employee => wish with { EmployeeId = null },
            CalendarWishPart.Excluded => wish with { ExcludedEmployeeIds = [.. wish.ExcludedEmployeeIds.Where(id => id != chip.Chip.Id)] },
            CalendarWishPart.Room => wish with { RoomId = null },
            CalendarWishPart.Guests => wish with { Guests = null },
            _ => null,
        };
        if (changed is not null)
        {
            await AskAsync(null, changed).ConfigureAwait(true);
        }
    }

    /// <summary>Eine Karte oder ein Eintrag der Liste „Alle Behandlungen“ wurde gewählt: der Wunsch bekommt die Behandlung, die Vorschläge kommen.</summary>
    [RelayCommand]
    private async Task ChooseTreatmentAsync(TreatmentCard card)
    {
        if (_wish is { } wish)
        {
            await AskAsync(null, wish with { TreatmentId = card.Id }).ConfigureAwait(true);
        }
    }

    /// <summary>Antwort auf eine Rückfrage: der Wunsch mit der Antwort darin geht zurück an den Server.</summary>
    [RelayCommand]
    private async Task AnswerAsync(RequestAnswer answer) => await AskAsync(null, answer.Wish).ConfigureAwait(true);

    /// <summary>„Andere Zeiten“: die gezeigten Vorschläge gelten als abgelehnt und kommen nicht wieder.</summary>
    [RelayCommand]
    private async Task OtherTimesAsync()
    {
        if (_wish is { } wish && HasProposals)
        {
            _rejected.AddRange(Proposals.Select(p => p.Proposal.Start));
            await AskAsync(null, wish).ConfigureAwait(true);
        }
    }

    private async Task AskAsync(string? text, CalendarWish? wish)
    {
        var version = ++_version;
        Busy = true;
        Error = null;
        var result = await _api.PostAsync<CalendarRequestResult>($"api/locations/{_resources.Location.Id}/calendar-request",
            new CalendarRequestInput(text, wish, _date, [.. _rejected])).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        Busy = false;
        if (result.Value is not { } answer)
        {
            Error = _api.IsOffline ? "Ohne Verbindung gibt es keine Vorschläge." : result.Error;
            return;
        }

        Text = "";
        _wish = answer.Wish;
        Answer = answer.Text;
        Disclosure = answer.Disclosure;
        Chips = [.. answer.Chips.Select(c => new WishChip(c, c.Part != CalendarWishPart.Day))];
        _askedWithoutTreatment = answer.Wish.TreatmentId is null;
        TreatmentCards = [.. (answer.TreatmentChoices ?? []).Select(id => _resources.Treatments.FirstOrDefault(t => t.Id == id)).OfType<TreatmentDto>().Select(CardOf)];
        OnPropertyChanged(nameof(NeedsTreatment));
        Questions = [.. (answer.Questions ?? []).Select(q => new RequestQuestion(q.Text, [.. q.Answers.Select(a => new RequestAnswer(a.Label, a.Wish))]))];
        var colors = _resources.Employees.ToDictionary(e => e.Id, e => e.Color);
        Proposals = [.. answer.Proposals.Select((p, i) => new ProposalRow(p, i + 1,
            DayLayout.TimeText(p.Start, p.End, _zone),
            DateOnly.FromDateTime(DayLayout.Local(p.Start, _zone)) is var day && day != _date ? DayLayout.DateText(day) : null,
            colors.GetValueOrDefault(p.EmployeeId) ?? "#2f6f5e",
            Money.Format(p.Price, _resources.Location.Currency)))];
        ActiveIndex = Proposals.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(Hint));
        await LoadExtrasAsync(answer.Wish.TreatmentId, version).ConfigureAwait(true);
    }

    /// <summary>Was zur gewählten Behandlung bereitgestellt wird (Tee, Früchteteller …), nur als Hinweis unter den Bausteinen.</summary>
    private async Task LoadExtrasAsync(Guid? treatmentId, int version)
    {
        if (treatmentId is not { } id)
        {
            ExtrasText = null;
            return;
        }

        var extras = await _api.GetAsync<List<TreatmentExtraDto>>($"api/treatments/{id}/extras").ConfigureAwait(true);
        if (version == _version)
        {
            ExtrasText = extras.Value is { Count: > 0 } list ? $"Bereitgestellt wird: {string.Join(", ", list.Select(e => e.Name))}" : null;
        }
    }

    public void Move(int direction)
    {
        if (Proposals.Count > 0)
        {
            ActiveIndex = Math.Clamp(ActiveIndex + direction, 0, Proposals.Count - 1);
        }
    }

    public void Choose(ProposalRow row) => ActiveIndex = Proposals.ToList().IndexOf(row);

    /// <summary>Vorschlag 1 bis 3 buchen (Enter bzw. Strg+2, Strg+3): mit Gast gleich für ihn, sonst öffnet „Neuer Termin“ mit Behandlung, Person und Uhrzeit.</summary>
    public async Task BookAsync(int number)
    {
        if (number >= 1 && number <= Proposals.Count)
        {
            ActiveIndex = number - 1;
            await _book(Proposals[number - 1].Proposal, GuestCustomer).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task BookActiveAsync() => Active is { } row ? BookAsync(row.Number) : Task.CompletedTask;

    [RelayCommand]
    private void Close() => _close();
}

/// <summary>Baustein des Wunsches; der Tag lässt sich nicht entfernen.</summary>
public sealed record WishChip(CalendarWishChip Chip, bool CanRemove)
{
    public string Text => Chip.Text;

    public bool IsExcluded => Chip.Part == CalendarWishPart.Excluded;
}

/// <summary>Behandlung als Karte: Name, Dauer und Preis, Raumart.</summary>
public sealed record TreatmentCard(Guid Id, string Name, string Facts, string RoomKind);

/// <summary>Eine Gruppe von Behandlungen („Massagen“), aufklappbar.</summary>
public sealed partial class TreatmentGroup(string name, IReadOnlyList<TreatmentCard> cards) : ViewModelBase
{
    public string Name { get; } = name;

    public IReadOnlyList<TreatmentCard> Cards { get; } = cards;

    public string Header => $"{Name} ({Cards.Count})";

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;
}

/// <summary>Rückfrage der KI-Anfrage mit Antworten als Knöpfe.</summary>
public sealed record RequestQuestion(string Text, IReadOnlyList<RequestAnswer> Answers);

/// <summary>Antwort auf eine Rückfrage: Beschriftung und der Wunsch mit der Antwort darin.</summary>
public sealed record RequestAnswer(string Label, CalendarWish Wish);

/// <summary>Vorschlag in der Liste: Nummer (Taste), Uhrzeit, anderer Tag, Person, Raum, Preis und warum.</summary>
public sealed record ProposalRow(CalendarProposal Proposal, int Number, string Time, string? OtherDay, string WhoColor, string Price)
{
    public string Who => Proposal.EmployeeName;

    public string Detail => string.Join(" · ", new[] { Proposal.RoomName, Proposal.TreatmentName }.Where(s => !string.IsNullOrEmpty(s)));

    public string Reason => Proposal.Reason;

    public bool HasReason => Reason.Length > 0;

    public bool HasOtherDay => OtherDay is not null;

    /// <summary>Name für Screenreader: Nummer (Taste), anderer Tag, Uhrzeit, Person, Raum und Behandlung, Grund, Preis.</summary>
    public string AccessibleName => AccessibleNames.Join(Number.ToString(CultureInfo.InvariantCulture), OtherDay, Time, Who, Detail, Reason, Price);
}
