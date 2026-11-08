using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Betrieb im Haus im Kalender (ADR 0065): Raumpflege abhaken, die Betriebsdaten des Tages (Tagesstart-Kontrolle, Bereitstellen, Handbuch-Hinweise, Lager) als
/// Quelle der Meldungen sowie die Hinweise nach Buchen und Einchecken. Änderungen anderer Arbeitsplätze kommen über <see cref="OperationsChangeKind"/> live.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Betriebsdaten des Tages; leer in der 7-Tage-Ansicht und ohne Verbindung für einen noch nicht geladenen Tag.</summary>
    [ObservableProperty]
    public partial DayOperationsViewModel? Operations { get; private set; }

    /// <summary>Raumcheck nach der Behandlung (ADR 0068): die offene Seitenleiste „Raumpflege &lt;Raum&gt;“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial RoomCarePanelViewModel? RoomCarePanel { get; private set; }

    partial void OnOperationsChanged(DayOperationsViewModel? value) => RefreshMessages();

    /// <summary>Raumpflege und Tagesstart abhaken darf, wer Termine bearbeiten darf.</summary>
    public bool CanCheckCare => _user.Rights.Contains(AccessRight.ChangeStatus);

    private async Task LoadOperationsAsync(LocationDto location, DateOnly date)
    {
        if (IsFiltered)
        {
            Operations = null;
            return;
        }

        if (_api.IsOffline || OfflineNote is not null)
        {
            // Ohne Verbindung bleibt der letzte Stand dieses Tages stehen (Meldungen: „letzter Stand“); ein anderer Tag hat keinen.
            if (Operations?.Scope != (location.Id, date))
            {
                Operations = null;
            }

            return;
        }

        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        DayOperationsViewModel? operations = null;
        operations = new DayOperationsViewModel(_api, location.Id, date, _zone, date > today, CanCheckCare, _user.Rights.Contains(AccessRight.ManageLocation),
            _user.Rights.Contains(AccessRight.Book))
        {
            Changed = () =>
            {
                if (ReferenceEquals(Operations, operations))
                {
                    RefreshMessages();
                }
            },
        };
        await operations.LoadAsync().ConfigureAwait(true);
        if (date != Date || location.Id != Location?.Id || IsFiltered)
        {
            return;
        }

        Operations = operations;
        RefreshServings();
        Rebuild();
    }

    /// <summary>„Bereitstellen“ des geöffneten Termins aus den Einträgen des Tages (nach dem Laden und nach dem Abhaken neu).</summary>
    private void RefreshServings()
    {
        if (Selected is { } selected)
        {
            selected.Servings = Operations?.ServingsFor(selected.Appointment.Id, CanCheckCare, ReloadOperationsAsync);
        }
    }

    private async Task ReloadOperationsAsync()
    {
        if (Operations is { } operations)
        {
            await operations.LoadAsync().ConfigureAwait(true);
            RefreshServings();
            Rebuild();
        }
    }

    /// <summary>
    /// Raumpflege-Streifen angeklickt: abhaken oder den Haken zurücknehmen; der Kalender zeigt den neuen Stand. Hat die Aufgabe offene Zeilen des Raumchecks
    /// (Material unter der Mindestmenge), öffnet stattdessen die Seitenleiste „Raumpflege &lt;Raum&gt;“.
    /// </summary>
    public Task ToggleCareAsync(CalendarStripe stripe) => stripe.Care is { } task ? CheckCareAsync(task) : Task.CompletedTask;

    /// <summary>Raumpflege einer Aufgabe abhaken oder den Haken zurücknehmen (Streifen im Kalender oder Meldung); offene Zeilen des Raumchecks öffnen die Seitenleiste.</summary>
    private async Task CheckCareAsync(RoomCareTaskDto task)
    {
        if (!CanCheckCare)
        {
            return;
        }

        if (task.State != RoomCareState.Done && task.Restock?.Any(l => l.State == RestockState.Open) == true)
        {
            OpenRoomCarePanel(task);
            return;
        }

        var result = await _api.PutAsync<object>($"api/appointments/{task.AppointmentId}/room-care", new RoomCareCheckInput(task.State != RoomCareState.Done))
            .ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Raumpflege nicht abgehakt", result.Error, action: () => _ = CheckCareAsync(task));
            return;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    private void OpenRoomCarePanel(RoomCareTaskDto task)
    {
        ClosePanels();
        RoomCarePanel = new RoomCarePanelViewModel(_api, task, CanCheckCare, LoadAsync,
            PostWarning, () =>
            {
                RoomCarePanel = null;
                Rebuild();
            });
        Rebuild();
    }

    /// <summary>Hinweise der Antwort auf das Buchen oder Einchecken (Lagergrenze, Tagesstart, Raumpflege): bleiben stehen, sperren aber nichts.</summary>
    private void ShowWarnings(AppointmentDto appointment) => ShowWarnings([appointment]);

    private void ShowWarnings(IEnumerable<AppointmentDto> appointments)
    {
        var warnings = appointments.SelectMany(a => a.Warnings ?? []).Distinct().ToList();
        if (warnings.Count > 0)
        {
            PostWarning(string.Join(" ", warnings));
        }
    }

    // ---------- Live: Betrieb ändert sich an einem anderen Arbeitsplatz ----------

    private void OnOperationsLive(Guid locationId, OperationsChangeKind kind)
    {
        if (locationId == Location?.Id)
        {
            _ = ReloadSourceAsync(kind);
        }
    }

    /// <summary>Lädt nur die Quelle nach, die sich geändert hat, und baut die Meldungen neu: Zähler, Kopf-Symbole und Seitenleiste ziehen sofort mit.</summary>
    private async Task ReloadSourceAsync(OperationsChangeKind kind)
    {
        if (Location is not { } location || _api.IsOffline || IsFiltered)
        {
            return;
        }

        var date = Date;
        if (kind == OperationsChangeKind.RoomCare)
        {
            var care = await _api.GetAsync<RoomCareDay>($"api/locations/{location.Id}/room-care?date={date:yyyy-MM-dd}").ConfigureAwait(true);
            if (care.Value is { } value && date == Date && location.Id == Location?.Id && _day is not null && !IsFiltered)
            {
                _day = _day with { RoomCare = value };
                Rebuild();
            }

            return;
        }

        if (OperationsOfDay() is { } operations)
        {
            await operations.LoadAsync(kind).ConfigureAwait(true);
            if (kind == OperationsChangeKind.RoomStart)
            {
                Rebuild();
            }
        }
    }
}
