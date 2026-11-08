using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Betroffene Person, betroffener Raum oder betroffenes Material als Link: ein Klick bringt den Kalender dorthin.</summary>
public sealed record SubjectLink(MessageSubject Subject, ICommand Open)
{
    public string Name => Subject.Name;

    public string Icon => Subject.Icon;
}

/// <summary>Vorschlag des Assistenten als Knopf in „Was tun?“; <c>Action</c> sagt, was ein Klick tut.</summary>
public sealed record DetailSuggestion(AssistantSuggestion Suggestion, string Action, Func<Task> Run)
{
    public string Label => Suggestion.Label;
}

/// <summary>
/// Detailansicht einer Meldung als eigener Reiter „Meldung“ (Schritt 16): voller Wortlaut, Betroffene mit Links, Auslöser und „seit“, die Aktion als primärer Knopf
/// und die Karte „Was tun?“. Sie fragt den Assistenten (<c>api/assistant</c>) und zeigt Antwort, Vorschläge als Knöpfe und Verweise ins Handbuch; ohne Antwort
/// steht der Abschnitt des Handbuchs zur Art der Meldung da. Bei einer Tagesstart-Meldung gehört die Checkliste des Raums mit der Abnahme dazu.
/// </summary>
[AiOutput(AiFunctions.WhatToDo)]
public sealed partial class MessageDetailViewModel : ViewModelBase, ISidePanel
{
    private readonly IWorkstationApi _api;
    private readonly Guid? _locationId;
    private readonly bool _canAsk;
    private readonly Func<AssistantSuggestion, DetailSuggestion?> _resolve;
    private readonly Func<RoomStartRow?>? _startRoom;
    private readonly Action _close;

    public MessageDetailViewModel(MessageItem item, IWorkstationApi api, Guid? locationId, bool canAsk, Action<MessageSubject> openSubject,
        Func<AssistantSuggestion, DetailSuggestion?> resolve, DayOperationsViewModel? operations, Action close)
    {
        _api = api;
        _locationId = locationId;
        _canAsk = canAsk;
        _resolve = resolve;
        _close = close;
        Operations = operations;
        Item = item;
        if (item.Kind == MessageKind.DayStart && item.Subjects is [var room, ..])
        {
            _startRoom = () => operations?.Rooms.FirstOrDefault(r => r.Room.RoomId == room.Id);
        }

        Links = [.. item.Subjects.Select(s => new SubjectLink(s, new RelayCommand(() => openSubject(s))))];
    }

    public string PanelTitle => "Meldung";

    public string? PanelSubtitle => Item.Title;

    public PanelKind PanelKind => PanelKind.MessageDetail;

    public string PanelIcon => Icons.Bell;

    public string PanelTabText => "Meldung";

    /// <summary>Die Meldung; wechselt, wenn der Kalender sie neu aufbaut (z. B. nach dem Laden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelSubtitle), nameof(HasLines), nameof(IsResolved), nameof(CanAct))]
    public partial MessageItem Item { get; private set; }

    public IReadOnlyList<SubjectLink> Links { get; }

    public bool HasLinks => Links.Count > 0;

    public bool HasLines => Item.Lines.Count > 0;

    /// <summary>Die Meldung gibt es nicht mehr: sie ist erledigt oder weggeklickt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResolved), nameof(CanAct))]
    public partial bool Gone { get; private set; }

    public bool IsResolved => Gone || Item.IsDone;

    /// <summary>Die Aktion als Hauptknopf; bei „Tagesstart offen“ steht die Checkliste selbst darunter, ein Knopf dafür wäre doppelt.</summary>
    public bool CanAct => !IsResolved && Item.HasAction && Item.Kind != MessageKind.DayStart;

    /// <summary>Tagesstart-Kontrolle des Raums mit Haken und Abnahme; nur bei einer Tagesstart-Meldung.</summary>
    public RoomStartRow? StartRoom => _startRoom?.Invoke();

    public DayOperationsViewModel? Operations { get; }

    public bool HasStartRoom => StartRoom is not null;

    /// <summary>Ersetzt die Meldung durch ihren neuen Stand; <c>null</c>, wenn sie nicht mehr da ist.</summary>
    public void Update(MessageItem? current)
    {
        if (current is not null)
        {
            Item = current;
        }

        Gone = current is null && !Item.IsDone;
        OnPropertyChanged(nameof(StartRoom));
        OnPropertyChanged(nameof(HasStartRoom));
    }

    // ---------- Was tun? ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Suggestions), nameof(HelpTopic), nameof(HelpSection), nameof(HasHelp), nameof(HasAnswer), nameof(Disclosure))]
    public partial AssistantAnswer? Answer { get; private set; }

    [ObservableProperty]
    public partial bool IsAsking { get; private set; }

    public bool HasAnswer => Answer is not null;

    /// <summary>Der Assistent hat nicht geantwortet (Fehler oder nicht erreichbar); das Handbuch zur Meldung bleibt der Weg ohne KI.</summary>
    [ObservableProperty]
    public partial bool AskFailed { get; private set; }


    public string? Disclosure => Answer?.Disclosure;

    /// <summary>Vorschläge der Antwort; bei Listen-Antworten (Ä316) stehen die Ziele in den Zeilen und kommen hier dazu.</summary>
    public IReadOnlyList<DetailSuggestion> Suggestions =>
        [.. (Answer?.Suggestions ?? []).Concat(Answer?.Block?.Items?.Select(i => i.Suggestion).OfType<AssistantSuggestion>() ?? []).Select(_resolve).OfType<DetailSuggestion>()];

    /// <summary>Handbuch zur Meldung: der beste Treffer des Assistenten, sonst der Abschnitt zur Art der Meldung.</summary>
    public string? HelpTopic => Answer?.Help is [var first, ..] ? first.Topic : Item.Help?.Topic;

    public string? HelpSection => Answer?.Help is [var first, ..] ? first.Section : Item.Help?.Section;

    public bool HasHelp => HelpTopic is not null;

    /// <summary>Weitere Treffer des Assistenten als Verweis ins Handbuch.</summary>
    public IReadOnlyList<AssistantHelpLink> MoreHelp => Answer?.Help.Skip(1).ToList() ?? [];

    [RelayCommand]
    private static void OpenHelpLink(AssistantHelpLink link) => HelpContent.Open(link.Topic, link.Section);

    [RelayCommand]
    private static Task RunSuggestionAsync(DetailSuggestion suggestion) => suggestion.Run();

    /// <summary>Fragt den Assistenten „Was tun bei: …“; ohne Zugang oder bei einem Fehler bleibt der Handbuch-Abschnitt.</summary>
    public async Task LoadHelpAsync()
    {
        if (!_canAsk)
        {
            return;
        }

        IsAsking = true;
        AskFailed = false;
        try
        {
            var result = await _api.PostAsync<AssistantAnswer>("api/assistant",
                new AssistantQuestion($"Was tun bei: {Item.Title}. {Item.Text}", _locationId, "kalender", HelpContent.Surface)).ConfigureAwait(true);
            if (result is { Ok: true, Value: { } answer })
            {
                Answer = answer;
                OnPropertyChanged(nameof(MoreHelp));
            }
            else
            {
                AskFailed = true;
            }
        }
        finally
        {
            IsAsking = false;
        }
    }

    [RelayCommand]
    private void Close() => _close();
}
