using System.Collections.ObjectModel;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Hinweis der Startprüfung als schließbare Warnung oben im Fenster.</summary>
public sealed class StartupHint
{
    public StartupHint(StartupFinding finding, Action<StartupHint> close)
    {
        Title = finding.Title;
        Text = finding.Text;
        CloseCommand = new RelayCommand(() => close(this));
    }

    public string Title { get; }

    public string Text { get; }

    public IRelayCommand CloseCommand { get; }
}

/// <summary>
/// Startprüfung der Voraussetzungen (<see cref="StartupRequirements"/>): Funde, die den Start verhindern, ersetzen den Start durch eine Karte mit
/// „Beenden“; Hinweise stehen als Warnung oben, solange die Person nicht angemeldet ist, und blockieren nichts.
/// </summary>
public partial class MainViewModel
{
    private bool _requirementsChecked;

    private readonly Lazy<string?> _offlineAdvice = new(() => facts is null || local is not null
        ? null
        : StartupRequirements.ServerAdvice(facts, api.BaseAddress));

    /// <summary>Ein Fund verhindert den Start: die Karte „AISPA Studio kann auf diesem PC nicht starten“ steht statt der Verbindung.</summary>
    [ObservableProperty]
    public partial bool IsBlocked { get; private set; }

    /// <summary>Alle Funde der Karte: zuerst die, die den Start verhindern, dann die Hinweise.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StartupFinding> BlockedFindings { get; private set; } = [];

    /// <summary>Hinweise der Startprüfung; jeder lässt sich schließen.</summary>
    public ObservableCollection<StartupHint> Hints { get; } = [];

    /// <summary>Die Warnungen stehen auf Startbildschirm und Anmeldung, nicht in der Arbeitsfläche.</summary>
    public bool AreHintsShown => Hints.Count > 0 && Content is not ShellViewModel;

    /// <summary>Der Weg zu einem Server, wenn die Adresse auf diesem PC liegt und der Server nicht antwortet; sonst <c>null</c>.</summary>
    public string? OfflineAdvice => _offlineAdvice.Value;

    /// <summary>Prüft einmal je Programmstart; <c>false</c>, wenn der Start nicht weitergehen darf.</summary>
    private bool CheckRequirements()
    {
        if (facts is null)
        {
            return true;
        }

        if (!_requirementsChecked)
        {
            _requirementsChecked = true;
            var findings = StartupRequirements.Check(facts, standalone: local is not null);
            if (findings.Any(f => f.Severity == StartupSeverity.Blocking))
            {
                BlockedFindings = [.. findings.OrderBy(f => f.Severity)];
                IsBlocked = true;
                StatusText = "AISPA Studio kann auf diesem PC nicht starten.";
                return false;
            }

            foreach (var finding in findings)
            {
                Hints.Add(new StartupHint(finding, CloseHint));
            }

            OnPropertyChanged(nameof(AreHintsShown));
        }

        return !IsBlocked;
    }

    private void CloseHint(StartupHint hint)
    {
        Hints.Remove(hint);
        OnPropertyChanged(nameof(AreHintsShown));
    }
}
