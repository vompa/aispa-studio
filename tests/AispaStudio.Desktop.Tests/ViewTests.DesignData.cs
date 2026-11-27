using System.Reflection;
using System.Xml.Linq;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Design;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beispieldaten für die Vorschau des Designers (Design/DesignData.cs, Aufgaben T-089 und T-090).</summary>
public sealed partial class ViewTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public Task Vorschau_der_Kundenliste_zeigt_Beispielkunden_und_die_Kundenkarte() =>
        InWindow("hell", () => new CustomersView { DataContext = DesignData.Customers }, 1280, 800, async window =>
        {
            var customers = (CustomersViewModel)((Control)window.Content!).DataContext!;

            // When die Vorschau die Termine der geöffneten Kundenkarte geladen hat
            await customers.LoadVisitsAsync(customers.Selected);
            await Pump();

            // Then stehen die Beispielkunden in der Liste, die Karte der ersten zeigt ihre Termine, und kein Aufruf ist gescheitert.
            Assert.Equal(5, customers.Results.Count);
            Assert.Equal(4, customers.Visits.Count);
            Assert.Null(customers.Error);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == customers.SelectedName && t.IsEffectivelyVisible);
        });

    [Fact]
    public Task Vorschau_des_Kalenders_zeigt_den_Beispieltag_mit_Terminen() =>
        InWindow("hell", () => new CalendarView { DataContext = DesignData.Calendar }, 1440, 900, async window =>
        {
            var calendar = (CalendarViewModel)((Control)window.Content!).DataContext!;

            // When die Vorschau den Standort gewählt und den Tag geladen hat (der Getter startet das Laden ohne Warten)
            for (var round = 0; round < 50 && calendar.Location is null; round++)
            {
                await Pump();
            }

            await calendar.LoadAsync();
            await Pump();

            // Then zeigt der Kalender die Termine des Beispieltags, und kein Abruf ist gescheitert.
            Assert.Null(calendar.Error);
            Assert.Contains(window.GetVisualDescendants().OfType<AppointmentCard>(),
                c => c.IsEffectivelyVisible && c.DataContext is CalendarItem { Appointment.CustomerName: "Brigitte Müller" });
        });

    /// <summary>Der Compiler entfernt <c>Design.*</c> vor der Typauflösung; ein falscher Verweis fiele sonst erst in der Vorschau auf.</summary>
    [Fact]
    public void Jeder_Design_DataContext_verweist_auf_eine_vorhandene_Eigenschaft()
    {
        var desktop = Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop");
        var previews = Directory.GetFiles(desktop, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (File: Path.GetRelativePath(desktop, f), Root: XDocument.Load(f).Root!))
            .Select(v => (v.File, v.Root, Static: v.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "Design.DataContext")?.Element(Xaml + "Static")))
            .Where(v => v.Static is not null)
            .ToList();
        var problems = new List<string>();
        foreach (var (file, root, reference) in previews)
        {
            try
            {
                var member = reference!.Attribute("Member")!.Value;
                var property = ResolveStaticProperty(root, member);
                var dataType = ResolveType(root, root.Attribute(Xaml + "DataType")!.Value);
                if (!dataType.IsAssignableFrom(property.PropertyType))
                {
                    problems.Add($"{file}: {member} liefert {property.PropertyType.Name}, die Ansicht erwartet {dataType.Name}.");
                }
            }
            catch (Xunit.Sdk.XunitException e)
            {
                problems.Add($"{file}: {e.Message}");
            }
        }

        Assert.NotEmpty(previews);
        Assert.Empty(problems);
    }

    private static PropertyInfo ResolveStaticProperty(XElement root, string member)
    {
        var dot = member.LastIndexOf('.');
        var type = ResolveType(root, member[..dot]);
        return type.GetProperty(member[(dot + 1)..], BindingFlags.Public | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException($"{member}: keine öffentliche statische Eigenschaft.");
    }

    /// <summary>„präfix:Typ“ über <c>xmlns:präfix="using:Namensraum"</c> in der Assembly der App.</summary>
    private static Type ResolveType(XElement root, string qualifiedName)
    {
        var parts = qualifiedName.Split(':');
        var clrNamespace = root.GetNamespaceOfPrefix(parts[0])!.NamespaceName["using:".Length..];
        return typeof(CustomersView).Assembly.GetType($"{clrNamespace}.{parts[1]}")
            ?? throw new Xunit.Sdk.XunitException($"{qualifiedName}: Typ nicht gefunden.");
    }
}
