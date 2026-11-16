using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Seite „Schließzeiten“ der Verwaltung (ADR 0098, masken.md R15): Schließzeiten des Standorts und Raumsperren stehen als zwei Abschnitte untereinander, damit die Gruppe „Betrieb“
/// mit fünf Masken auskommt. Jeder Abschnitt bleibt die bisherige Maske mit eigener Liste, eigenem Formular und eigenem Speichern; die Seite reicht Laden, Rückfrage und Sprungziele
/// weiter. Das frühere Sprungziel <c>masterData/roomClosures</c> öffnet diese Seite und rollt zum Abschnitt „Raumsperren“.
/// </summary>
public sealed partial class ClosuresPageViewModel : ViewModelBase, ITargetedArea, IFocusTarget
{
    /// <summary>Abschnitt „Schließzeiten“; zugleich der Reiter der Seite in den Sprungzielen.</summary>
    public const string ClosuresSection = "closures";

    /// <summary>Abschnitt „Raumsperren“; bis ADR 0098 eine eigene Maske, als Sprungziel weiter gültig.</summary>
    public const string RoomClosuresSection = "roomClosures";

    private Task? _loading;
    private bool _scrollPending;

    public ClosuresPageViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time)
    {
        Closures = new ClosuresViewModel(api, user, time);
        RoomClosures = new RoomClosuresViewModel(api, user, time);
        Closures.PropertyChanged += OnSectionChanged;
        RoomClosures.PropertyChanged += OnSectionChanged;
    }

    public ClosuresViewModel Closures { get; }

    public RoomClosuresViewModel RoomClosures { get; }

    /// <summary>Name in der Rückfrage: der Abschnitt mit den ungespeicherten Änderungen, bei beiden beide.</summary>
    public string Title => (Closures.HasUnsavedChanges, RoomClosures.HasUnsavedChanges) switch
    {
        (true, true) => $"{Closures.Title} und {RoomClosures.Title}",
        (false, true) => RoomClosures.Title,
        _ => Closures.Title,
    };

    public bool HasUnsavedChanges => Closures.HasUnsavedChanges || RoomClosures.HasUnsavedChanges;

    /// <summary>Der zuletzt verlangte Abschnitt (<see cref="ClosuresSection"/> oder <see cref="RoomClosuresSection"/>); die Ansicht rollt über <see cref="FocusTarget"/> dorthin.</summary>
    public string? FocusedSection { get; private set; }

    /// <summary>Der Name des Abschnitts, zu dessen Anfang die Ansicht rollt; gesetzt, sobald beide Abschnitte geladen sind.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Beide Abschnitte fragen über die Dialoge des Fensters.</summary>
    public override IConfirmDialogs? Dialogs
    {
        get => base.Dialogs;
        set
        {
            base.Dialogs = value;
            Closures.Dialogs = value;
            RoomClosures.Dialogs = value;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        var load = _loading ??= Task.WhenAll(Closures.EnsureLoadedAsync(), RoomClosures.EnsureLoadedAsync());
        await load.ConfigureAwait(true);
        if (ReferenceEquals(_loading, load))
        {
            _loading = null;
        }

        ScrollToSection();
    }

    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, Field: field, Section: section));

    /// <summary>Ein Sprung aus Kalender oder Assistent: <c>roomClosures</c> gilt dem Abschnitt „Raumsperren“ (mit Raum für eine neue Sperre), alles andere den Schließzeiten.</summary>
    public void Open(AdminTarget target)
    {
        FocusedSection = target.Section == RoomClosuresSection ? RoomClosuresSection : ClosuresSection;
        _scrollPending = true;
        var section = target with { Section = null };
        if (FocusedSection == RoomClosuresSection)
        {
            RoomClosures.Open(section);
        }
        else
        {
            Closures.Open(section);
        }

        ScrollToSection();
    }

    public async Task<bool> SaveChangesAsync()
    {
        var saved = true;
        if (Closures.HasUnsavedChanges)
        {
            saved &= await Closures.SaveChangesAsync().ConfigureAwait(true);
        }

        if (RoomClosures.HasUnsavedChanges)
        {
            saved &= await RoomClosures.SaveChangesAsync().ConfigureAwait(true);
        }

        return saved;
    }

    public void Discard()
    {
        if (Closures.HasUnsavedChanges)
        {
            Closures.Discard();
        }

        if (RoomClosures.HasUnsavedChanges)
        {
            RoomClosures.Discard();
        }
    }

    /// <summary>Meldet der Ansicht den Abschnitt eines Sprungs, sobald beide geladen sind; vorher verschöbe der Inhalt darüber die Stelle noch.</summary>
    private void ScrollToSection()
    {
        if (_scrollPending && Closures.IsLoaded && RoomClosures.IsLoaded)
        {
            _scrollPending = false;
            FocusTarget = FocusedSection == RoomClosuresSection ? "RoomClosuresSection" : "ClosuresSection";
        }
    }

    private void OnSectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Title));
        }
    }
}
