using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Formular für die Stammdaten eines Gastes (Name, Kontakt, Anschrift, dazu eingeklappt „Weitere Angaben“). In der Seitenleiste
/// (<see cref="Docked"/>) hat es Rand und eine feste Aktionsleiste mit Trennlinie; in der Kundenkarte steht die Leiste am Ende der Maske.
/// </summary>
public partial class CustomerContactForm : UserControl
{
    public static readonly StyledProperty<bool> DockedProperty = AvaloniaProperty.Register<CustomerContactForm, bool>(nameof(Docked));

    private CustomerContactFormViewModel? _form;

    static CustomerContactForm()
    {
        DockedProperty.Changed.AddClassHandler<CustomerContactForm>((form, _) => form.ApplyDocked());
    }

    public CustomerContactForm()
    {
        InitializeComponent();
        TitlesBeforeBox.FilterMode = AutoCompleteFilterMode.Custom;
        TitlesBeforeBox.TextFilter = (typed, title) => title is not null && CustomerContactFormViewModel.TitleMatches(typed, title);
        TitlesBeforeBox.TextSelector = (typed, title) => CustomerContactFormViewModel.CompleteTitle(typed, title ?? "", after: false);
        TitlesAfterBox.FilterMode = AutoCompleteFilterMode.Custom;
        TitlesAfterBox.TextFilter = (typed, title) => title is not null && CustomerContactFormViewModel.TitleMatches(typed, title);
        TitlesAfterBox.TextSelector = (typed, title) => CustomerContactFormViewModel.CompleteTitle(typed, title ?? "", after: true);
        DataContextChanged += (_, _) => Attach();
    }

    /// <summary>Seitenleiste: 18 Pixel Rand, Aktionsleiste mit Trennlinie fest im Fuß.</summary>
    public bool Docked
    {
        get => GetValue(DockedProperty);
        set => SetValue(DockedProperty, value);
    }

    private void ApplyDocked()
    {
        FieldsScroll.Padding = Docked ? new Thickness(16, 12, 16, 0) : new Thickness(0);
        Footer.Margin = Docked ? new Thickness(16, 0, 16, 12) : new Thickness(0);
        FooterDivider.IsVisible = Docked;
        FooterDivider.Margin = Docked ? new Thickness(-18, 0) : new Thickness(0);
    }

    private void Attach()
    {
        if (_form is not null)
        {
            _form.FocusRequested -= OnFocusRequested;
        }

        _form = DataContext as CustomerContactFormViewModel;
        if (_form is not null)
        {
            _form.FocusRequested += OnFocusRequested;
        }
    }

    /// <summary>Nach fehlgeschlagenem Speichern springt der Fokus ins erste ungültige Feld (masken.md, R8).</summary>
    private void OnFocusRequested(string field)
    {
        Control? box = field switch
        {
            CustomerContactFormViewModel.EmailField => EmailBox,
            CustomerContactFormViewModel.BirthDateField => BirthDateBox,
            CustomerContactFormViewModel.PostalCodeField => PostalCodeBox,
            CustomerContactFormViewModel.CountryField => CountryBox,
            _ => null,
        };
        if (box is not null)
        {
            Dispatcher.UIThread.Post(() => box.Focus());
        }
    }

    /// <summary>Erstes Feld der Maske, beim Öffnen einer Leiste zum Eingeben.</summary>
    public void FocusFirstField() => FirstNameBox.Focus();
}
