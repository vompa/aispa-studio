using System.Xml.Linq;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Strenger Namens-Scan nach R01 Slice 11 (W64, W-Muster 7): kein Name stammt aus einem Platzhalter, Listenzeilen heißen nach ihrem Inhalt,
/// und ein Record-Text („FreeSlotRow { … }“) gilt als unlesbar.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Name_FreeSlotRow_mit_geschweiften_Klammern_gilt_als_unlesbar() => Session.Dispatch(() =>
    {
        // Given ein Element mit dem Namen eines Records, When HasUnreadableName prüft, Then gilt der Name als unlesbar; Text mit runden Klammern bleibt lesbar.
        var record = new Button();
        Avalonia.Automation.AutomationProperties.SetName(record, "FreeSlotRow { Start = 09:00 }");
        var plain = new Button();
        Avalonia.Automation.AutomationProperties.SetName(plain, "Vortag (←)");

        Assert.True(HasUnreadableName(record, out _));
        Assert.False(HasUnreadableName(plain, out _));
        return Task.FromResult(true);
    }, CancellationToken.None);

    [Fact]
    public Task Textfeld_und_Auswahl_nur_mit_Platzhalter_bekommen_keinen_Namen() => Session.Dispatch(async () =>
    {
        // Given AccessibleNames (in der App eingetragen), When ein Feld nur mit Platzhalter geladen wird, Then hat es keinen Namen (der Scan meldet es).
        var box = new TextBox { PlaceholderText = "z. B. 21.10.2026" };
        var combo = new ComboBox { PlaceholderText = "Freigabe durch …" };
        var window = new Window { Content = new StackPanel { Children = { box, combo } } };
        window.Show();
        try
        {
            await Pump();

            Assert.True(HasUnreadableName(box, out var boxName), boxName);
            Assert.True(HasUnreadableName(combo, out var comboName), comboName);
        }
        finally
        {
            window.Close();
        }

        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Kundendaten_mit_offenen_Weiteren_Angaben_jedes_Feld_hat_einen_lesbaren_Namen() => Render("hell", "kunden-bearbeiten-namen", async (window, main) =>
    {
        // Given die Kundendaten von Brigitte Müller (mit Titel, „Weitere Angaben“ öffnet von selbst)
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump(Settling);
        Assert.True(shell.Customers.Contact!.MoreOpen);
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);

        // When der Scan jedes bedienbare Element liest, Then hat jedes einen lesbaren Namen, auch das Textfeld in der Titel-Auswahl.
        var problems = form.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or ToggleButton or CheckBox or TextBox or ComboBox or ListBoxItem && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable)
            .Where(c => HasUnreadableName(c, out _))
            .Select(c => $"{c.GetType().Name}#{c.Name} in {(c.TemplatedParent as Control)?.Name}").ToList();
        Assert.Empty(problems);
        var titles = form.GetVisualDescendants().OfType<AutoCompleteBox>().First(a => a.Name == "TitlesBeforeBox");
        var inner = titles.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.Equal("Titel vorangestellt", ControlAutomationPeer.CreatePeerForElement(inner).GetName());
        return calendar;
    });

    [Fact]
    public Task Kundensuche_mit_drei_Treffern_jede_Zeile_heisst_nach_der_Person() => Render("hell", "kunden-zeilennamen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
        await Pump();

        // Given die Kundensuche mit drei Treffern
        shell.Customers.Query = "berg";
        await Pump(Settling);
        var rows = window.GetVisualDescendants().OfType<CustomersView>().Single().GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Equal(3, rows.Count);

        // When der Scan die Zeilen liest, Then heißt jede nach der Person (wie angezeigt: Nachname, Rufname) und kein Name enthält „{“.
        var names = rows.Select(r => ControlAutomationPeer.CreatePeerForElement(r).GetName()).ToList();
        Assert.Equal(["Berger, Martina", "Bergmann, Steffi", "Weinberg, Lukas"], names);
        Assert.All(rows, r => Assert.False(HasUnreadableName(r, out var name), name));
        // Die Telefonnummer unterscheidet gleichnamige Gäste; der Screenreader liest sie als Hilfetext nach dem Namen.
        Assert.Equal(["+49 89 1234567", "+43 664 7654321", ""], rows.Select(r => Avalonia.Automation.AutomationProperties.GetHelpText(r) ?? ""));
        return calendar;
    }, Api().On("GET /api/customers?search=berg", ThreeBergCustomers()));

    private static List<CustomerDto> ThreeBergCustomers() =>
    [
        new(Guid.NewGuid(), "Martina", "Berger", "m.berger@example.de", "+49 89 1234567", null, null),
        new(Guid.NewGuid(), "Stefanie", "Bergmann", null, "+43 664 7654321", null, null, PreferredName: "Steffi"),
        new(Guid.NewGuid(), "Lukas", "Weinberg", "lukas@example.at", null, null, null),
    ];

    public static TheoryData<string> DesktopViews() =>
        [.. Directory.GetFiles(Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop"), "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop"), f))
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Quelltextprüfung: jede <c>ListBox</c> der App benennt ihre Zeilen per Stil (<c>AutomationProperties.Name</c> am <c>ListBoxItem</c>), entweder in
    /// <c>ListBox.Styles</c> oder als <c>ListBox.&lt;Klasse&gt; ListBoxItem</c> in derselben Datei; sonst liest der Screenreader den Record-Typ.
    /// </summary>
    [Theory]
    [MemberData(nameof(DesktopViews))]
    public void Jede_ListBox_benennt_ihre_Zeilen(string view)
    {
        var document = XDocument.Load(Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop", view), LoadOptions.SetLineInfo);

        static bool SetsName(XElement style) =>
            style.Elements().Any(s => s.Name.LocalName == "Setter" && (string?)s.Attribute("Property") == "AutomationProperties.Name");
        var fileStyles = document.Descendants(AvaloniaXaml + "Style").Where(SetsName).Select(s => (string?)s.Attribute("Selector") ?? "").ToList();

        bool Named(XElement list)
        {
            var own = list.Element(AvaloniaXaml + "ListBox.Styles")?.Elements(AvaloniaXaml + "Style")
                .Any(s => (string?)s.Attribute("Selector") == "ListBoxItem" && SetsName(s)) ?? false;
            var classes = ((string?)list.Attribute("Classes") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return own || classes.Any(c => fileStyles.Contains($"ListBox.{c} ListBoxItem"));
        }

        var problems = document.Descendants(AvaloniaXaml + "ListBox").Where(l => !Named(l))
            .Select(l => $"ListBox in Zeile {((System.Xml.IXmlLineInfo)l).LineNumber}").ToList();
        Assert.Empty(problems);
    }
}
