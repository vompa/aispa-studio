using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Stornierung zu einer neuen Zeit zurücknehmen (Aufgabe E): die Freie-Zeit-Vorschläge unter der Verschieben-Karte.</summary>
public partial class AppointmentViewModel
{
    /// <summary>Freie Zeiten des Gastes bei derselben Behandlung, Person und demselben Raum; die Wahl einer Zeile füllt Tag und Beginn der Verschieben-Karte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRestoreSlots))]
    public partial IReadOnlyList<FreeSlotRow> RestoreSlots { get; private set; } = [];

    /// <summary>Die Vorschläge stehen nur bei offener Verschieben-Karte.</summary>
    public bool ShowRestoreSlots => IsMovingToDay && RestoreSlots.Count > 0;

    /// <summary>
    /// Öffnet die Verschieben-Karte eines stornierten Termins mit den Vorschlägen; der erste ist schon eingetragen. Die Belegungen, die den Weg hierher machten,
    /// stehen in der Karte, und die Wahl zur Korrektur der Absage gilt für das Bestätigen.
    /// </summary>
    internal void ProposeRestoreTime(NewTimeRequest request, IReadOnlyList<FreeSlotRow> slots)
    {
        if (!ShowRestore)
        {
            return;
        }

        _restoreCorrection = request.SendCorrection;
        Conflict = ConflictOf(request.Conflict);
        RestoreSlots = slots;
        if (slots.Count > 0)
        {
            ChooseRestoreSlot(slots[0]);
        }

        IsMovingToDay = true;
    }

    [RelayCommand]
    private void ChooseRestoreSlot(FreeSlotRow? row)
    {
        if (row is null)
        {
            return;
        }

        var local = DayLayout.Local(row.Slot.Start, Zone);
        MoveDateText = $"{local:dd.MM.yyyy}";
        MoveTimeText = $"{local:HH:mm}";
    }
}
