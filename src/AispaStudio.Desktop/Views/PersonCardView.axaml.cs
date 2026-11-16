using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Personenkarte (R05 Slice 98, ADR 0098): Kopf, Bereiche und ihre Abschnitte; das Verhalten steht im Modell. Die Ansicht reicht den Speicherdialog für die Kalenderdatei herein
/// und rollt einen angesprungenen Abschnitt (<see cref="PersonCardViewModel.SectionRequested"/>) ins Bild; der Fokus geht auf sein erstes Bedienelement, außer er steht schon darin.
/// </summary>
public partial class PersonCardView : UserControl
{
    /// <summary>Der Block der Ansicht je Abschnitts-Id.</summary>
    private static readonly Dictionary<string, string> SectionBlocks = new()
    {
        [PersonCardViewModel.DetailsTab] = "DetailsBlock",
        [PersonCardViewModel.AccessTab] = "AccessHost",
        [PersonCardViewModel.QualificationsTab] = "QualificationsHost",
        [PersonCardViewModel.DutiesTab] = "DutiesHost",
        [PersonCardViewModel.RosterTab] = "RosterSection",
        [PersonCardViewModel.TimesTab] = "TimesSection",
        [PersonCardViewModel.PayrollTab] = "PayrollSection",
        [PersonCardViewModel.HrFileTab] = "HrFileHost",
        [PersonCardViewModel.RegulationsTab] = "RegulationsHost",
    };

    private PersonCardViewModel? _card;

    public PersonCardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_card is not null)
            {
                _card.SectionRequested -= ShowSection;
            }

            _card = DataContext as PersonCardViewModel;
            if (_card is null)
            {
                return;
            }

            if (_card.CalendarFile is { } calendarFile)
            {
                calendarFile.Files ??= new StorageFileSaver(this);
            }

            _card.SectionRequested += ShowSection;
            // Ein Sprung, der vor dem Anzeigen der Karte kam (Personenwechsel mit Ziel).
            if (_card.FocusedSection is { } section)
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

        // Erst nach dem Layout: der Bereich wird mit der Wahl sichtbar.
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<Control>(name) is not { IsEffectivelyVisible: true } block || TopLevel.GetTopLevel(this) is not { } top)
            {
                return;
            }

            block.BringIntoView(new Rect(0, 0, block.Bounds.Width, Math.Min(block.Bounds.Height, 40)));
            if (top.FocusManager?.GetFocusedElement() is Visual focused && block.IsVisualAncestorOf(focused))
            {
                return;
            }

            var candidates = block.GetVisualDescendants().OfType<InputElement>()
                .Where(c => c.Focusable && c.IsTabStop && c.IsEffectivelyVisible && c.IsEffectivelyEnabled).ToList();
            (candidates.FirstOrDefault(c => c is not HelpButton) ?? candidates.FirstOrDefault())?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Background);
    }
}
