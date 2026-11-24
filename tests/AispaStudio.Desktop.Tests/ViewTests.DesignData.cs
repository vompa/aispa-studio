using System.Reflection;
using System.Xml.Linq;
using AispaStudio.Desktop.Design;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beispieldaten für die Vorschau des Designers (Design/DesignData.cs, Aufgabe T-089).</summary>
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

    /// <summary>Der Compiler entfernt <c>Design.*</c> vor der Typauflösung; ein falscher Verweis fiele sonst erst in der Vorschau auf.</summary>
    [Fact]
    public void Der_Design_DataContext_der_Kundenliste_verweist_auf_eine_vorhandene_Eigenschaft()
    {
        var view = XDocument.Load(Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop", "Views", "CustomersView.axaml"));
        var root = view.Root!;
        var member = root.Elements().Single(e => e.Name.LocalName == "Design.DataContext")
            .Elements(Xaml + "Static").Single().Attribute("Member")!.Value;

        var property = ResolveStaticProperty(root, member);
        var dataType = ResolveType(root, root.Attribute(Xaml + "DataType")!.Value);

        Assert.True(dataType.IsAssignableFrom(property.PropertyType), $"{member} liefert {property.PropertyType.Name}, die Ansicht erwartet {dataType.Name}.");
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
