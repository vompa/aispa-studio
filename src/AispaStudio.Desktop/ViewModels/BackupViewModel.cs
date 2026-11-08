using System.Globalization;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Tägliche Sicherung im Einzelplatz (ADR 0041, Schritt 5): Die lokale Datenbank ist die einzige Kopie, solange keine zentrale
/// dazukommt. Die App sichert einmal am Tag in einen Ordner auf einem zweiten Laufwerk oder im Netz und erinnert, wenn die letzte
/// Sicherung älter als sieben Tage ist. Den Ordner wählt, wer den Standort verwalten darf; dabei entsteht der Wiederherstellungsschlüssel.
/// Mit <paramref name="scheduler"/> sichert Windows auch abends, wenn die App geschlossen ist (<see cref="ScheduledBackup"/>).
/// </summary>
public partial class BackupViewModel(ILocalServer local, ISettingsStore settings, TimeProvider time, IBackupScheduler? scheduler = null)
    : ViewModelBase
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    public static readonly TimeSpan RemindAfter = TimeSpan.FromDays(7);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(30);

    private DateTimeOffset? _lastAttempt;
    private bool _scheduleTried;

    /// <summary>Ordner wählen und Wiederherstellungsschlüssel erzeugen (Recht „Standort verwalten“).</summary>
    public bool CanConfigure { get; init; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFolderCommand))]
    public partial string FolderInput { get; set; } = settings.Load().BackupFolder ?? "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastBackupText), nameof(ReminderText), nameof(HasReminder), nameof(FolderText))]
    public partial DateTimeOffset? LastBackupAt { get; private set; } = settings.Load().LastBackupAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderText), nameof(ReminderText), nameof(HasReminder))]
    [NotifyCanExecuteChangedFor(nameof(BackupNowCommand))]
    public partial string? Folder { get; private set; } = settings.Load().BackupFolder;

    /// <summary>Gerade erzeugter Wiederherstellungsschlüssel; nur bis „Verstanden“ sichtbar, danach nirgends mehr gespeichert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewCode))]
    public partial string? NewCode { get; private set; }

    public bool HasNewCode => NewCode is not null;

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Ob Windows auch ohne laufende App sichert; leer, solange es keine Aufgabe gibt (oder kein Windows).</summary>
    [ObservableProperty]
    public partial string? ScheduleText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupNowCommand), nameof(SaveFolderCommand))]
    public partial bool IsBusy { get; private set; }

    public string FolderText => Folder is { } folder ? $"Sicherung täglich nach {folder}." : "Noch kein Ordner für die Sicherung gewählt.";

    public string LastBackupText => LastBackupAt is { } at ? $"Letzte Sicherung: {Local(at)}." : "Noch keine Sicherung.";

    /// <summary>Hinweis über allen Seiten, solange die letzte Sicherung fehlt oder älter als sieben Tage ist.</summary>
    public string? ReminderText => Folder is null
        ? "Noch keine tägliche Sicherung eingerichtet. Die Daten liegen nur auf diesem PC; die Leitung wählt unter „Sicherung“ einen Ordner auf einem zweiten Laufwerk oder im Netz."
        : LastBackupAt is not { } at ? "Noch keine Sicherung erstellt. Bitte unter „Sicherung“ prüfen, ob der Ordner erreichbar ist."
        : time.GetUtcNow() - at > RemindAfter ? $"Die letzte Sicherung ist vom {Local(at)} und damit älter als sieben Tage. Bitte unter „Sicherung“ prüfen."
        : null;

    public bool HasReminder => ReminderText is not null;

    private bool CanSaveFolder => CanConfigure && !IsBusy && FolderInput.Trim().Length > 0;

    /// <summary>Ordner merken, beim ersten Mal den Wiederherstellungsschlüssel erzeugen und gleich sichern.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveFolder))]
    private async Task SaveFolderAsync()
    {
        var folder = FolderInput.Trim();
        if (!Path.IsPathFullyQualified(folder))
        {
            ErrorText = "Bitte den vollständigen Pfad angeben, z. B. D:\\Sicherung oder \\\\server\\freigabe\\aispa.";
            return;
        }

        settings.Save(settings.Load() with { BackupFolder = folder });
        Folder = folder;
        if (!local.HasRecoveryKey)
        {
            NewCode = local.CreateRecoveryKey();
        }

        await ScheduleAsync().ConfigureAwait(true);
        await BackupNowAsync().ConfigureAwait(true);
    }

    /// <summary>Windows-Aufgabe anlegen oder erneuern (auch für Ordner, die vor dieser Version gewählt wurden).</summary>
    private async Task ScheduleAsync()
    {
        _scheduleTried = true;
        if (scheduler is null || Folder is null)
        {
            return;
        }

        var error = await scheduler.RegisterAsync().ConfigureAwait(true);
        ScheduleText = error is null
            ? $"Auch wenn die App geschlossen ist, sichert Windows jeden Abend um {WindowsBackupScheduler.At:HH:mm} Uhr; war der PC aus, beim nächsten Start."
            : $"Die Windows-Aufgabe für die Sicherung ließ sich nicht anlegen ({error}). Die App sichert weiter, solange sie läuft.";
    }

    /// <summary>Neuen Wiederherstellungsschlüssel erzeugen, z. B. wenn der alte verloren ist; ältere Sicherungen brauchen weiter den alten.</summary>
    [RelayCommand]
    private void NewRecoveryKey()
    {
        if (CanConfigure)
        {
            NewCode = local.CreateRecoveryKey();
            Message = "Neuer Wiederherstellungsschlüssel. Er gilt ab der nächsten Sicherung; ältere Sicherungen brauchen weiter den alten.";
        }
    }

    [RelayCommand]
    private void CodeNoted() => NewCode = null;

    private bool CanBackupNow => Folder is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanBackupNow))]
    public async Task BackupNowAsync()
    {
        if (Folder is not { } folder)
        {
            return;
        }

        IsBusy = true;
        Message = "Sichert …";
        _lastAttempt = time.GetUtcNow();
        try
        {
            var file = await local.BackupAsync(folder).ConfigureAwait(true);
            var at = time.GetUtcNow();
            settings.Save(settings.Load() with { LastBackupAt = at });
            LastBackupAt = at;
            ErrorText = null;
            Message = $"Gesichert: {Path.GetFileName(file)}";
        }
        catch (LocalServerException ex)
        {
            ErrorText = ex.Message;
            Message = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Aus dem Hintergrund: sichern, wenn die letzte Sicherung einen Tag alt ist; nach einem Fehler frühestens nach 30 Minuten wieder.</summary>
    public async Task RunIfDueAsync()
    {
        if (!_scheduleTried && local.HasRecoveryKey)
        {
            await ScheduleAsync().ConfigureAwait(true);
        }

        var now = time.GetUtcNow();
        if (Folder is null || IsBusy || !local.HasRecoveryKey
            || (LastBackupAt is { } last && now - last < Interval)
            || (_lastAttempt is { } attempt && now - attempt < RetryAfter))
        {
            OnPropertyChanged(nameof(ReminderText));
            OnPropertyChanged(nameof(HasReminder));
            return;
        }

        await BackupNowAsync().ConfigureAwait(true);
    }

    private string Local(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, time.LocalTimeZone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
