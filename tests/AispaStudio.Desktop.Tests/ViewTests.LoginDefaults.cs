using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Standardknöpfe der Anmeldeseite (Runde R02, Slice 34: W4): mehrere sichtbare Standardknöpfe feuern bei Enter alle, ein Standardknopf an einem
/// verborgenen Formular hängt bei jedem Zustandswechsel mit im Fenster. Darum trägt in jedem Zustand genau ein Knopf <c>IsDefault</c>, und er ist sichtbar.
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("Anmeldung mit Code")]
    [InlineData("Empfangsgerät")]
    [InlineData("Empfangsgerät mit Code")]
    [InlineData("Einzelplatz")]
    [InlineData("Wiederherstellung")]
    [InlineData("Einrichtung")]
    [InlineData("Passwort wählen")]
    public Task Anmeldeseite_hat_in_jedem_Zustand_genau_einen_Standardknopf(string state) => Session.Dispatch(async () =>
    {
        var login = await LoginIn(state);
        var window = await Show(new LoginView { DataContext = login });
        var defaults = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsDefault).ToList();
        var described = string.Join(", ", defaults.Select(b => $"\"{b.Content}\" sichtbar={b.IsEffectivelyVisible}"));
        window.Close();

        Assert.True(defaults.Count == 1, $"{state}: {defaults.Count} Standardknöpfe ({described})");
        Assert.True(defaults[0].IsEffectivelyVisible, $"{state}: der Standardknopf {described} ist nicht sichtbar");
        return true;
    }, CancellationToken.None);
}
