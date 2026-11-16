using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Bereich „Team“: Aufbau in der AXAML, Verhalten im Modell. Die Ansicht rollt einen angesprungenen Abschnitt von „Rechte und Zuständigkeiten“
/// (<see cref="TeamViewModel.SectionRequested"/>, frühere Ansichten „Wer darf was?“ und „Zuständigkeiten“) an den Anfang des sichtbaren Bereichs.
/// </summary>
public partial class TeamView : UserControl
{
    /// <summary>Der Block der Ansicht je früherer Ansicht.</summary>
    private static readonly Dictionary<string, string> SectionBlocks = new()
    {
        [TeamViewModel.PowersMode] = "PowersSection",
        [TeamViewModel.DutiesMode] = "DutiesSection",
    };

    private TeamViewModel? _team;

    public TeamView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_team is not null)
            {
                _team.SectionRequested -= ShowSection;
            }

            _team = DataContext as TeamViewModel;
            if (_team is null)
            {
                return;
            }

            _team.SectionRequested += ShowSection;
            // Ein Sprung, der vor dem Anzeigen kam (Bereich lädt noch).
            if (_team.FocusedSection is { } section)
            {
                ShowSection(section);
            }
        };
    }

    private void ShowSection(string section)
    {
        if (!SectionBlocks.TryGetValue(section, out var name))
        {
            return;
        }

        // Erst nach dem Layout: die Ansicht wird mit dem Wechsel sichtbar. BringIntoView richtete einen hohen Abschnitt am unteren Rand aus (Avalonia 12), darum der Versatz direkt.
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<Control>(name) is not { IsEffectivelyVisible: true } block || this.FindControl<ScrollViewer>("RightsScroll") is not { } scroll
                || scroll.Content is not Visual content || block.TranslatePoint(default, content) is not { } top)
            {
                return;
            }

            scroll.Offset = new Vector(scroll.Offset.X, top.Y);
        }, DispatcherPriority.Background);
    }
}
