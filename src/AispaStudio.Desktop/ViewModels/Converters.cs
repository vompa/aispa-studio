using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Data.Converters;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Kleine Umrechnungen für Bindungen in XAML.</summary>
public static class Converters
{
    /// <summary>„Brigitte Müller“ → „BM“ für den Kreis in Listen.</summary>
    public static readonly IValueConverter CustomerInitials =
        new FuncValueConverter<CustomerDto?, string>(c => c is null ? "" : DayLayout.Initials(c.InHouseName));

    /// <summary>Name einer Zeile der Kundenliste, wie sie angezeigt wird: „Müller, Brigitte“ (Nachname, Rufname).</summary>
    public static readonly IValueConverter CustomerListName =
        new FuncValueConverter<CustomerDto?, string>(c => c is null ? "" : $"{c.LastName}, {c.InHouseFirstName}");

    /// <summary>„Rezeption · Wien Innere Stadt“ unter dem Namen eines Testkontos.</summary>
    public static readonly IValueConverter TestAccountDetail =
        new FuncValueConverter<DevAccount?, string>(a => a is null ? "" : $"{ShellViewModel.RoleText(a.Role)} · {string.Join(", ", a.Locations)}");

    /// <summary>Breite eines Elements in seiner Bahn: <c>MultiBinding</c> aus Terminbreite der Spalte und <see cref="LanePlace"/>.</summary>
    public static readonly IMultiValueConverter LaneWidth =
        new FuncMultiValueConverter<object?, double>(values =>
        {
            var list = values.ToList();
            return list is [double width, LanePlace place] ? DayLayout.LaneWidth(width, place) : double.NaN;
        });

    /// <summary>Außenabstand eines Elements mit Versatz seiner Bahn: <c>MultiBinding</c> aus Terminbreite und <see cref="LanePlace"/>.</summary>
    public static readonly IMultiValueConverter LaneMargin =
        new FuncMultiValueConverter<object?, Thickness>(values =>
        {
            var list = values.ToList();
            return list is [double width, LanePlace place]
                ? new Thickness(3 + DayLayout.LaneLeft(width, place), 1, 3, 0)
                : new Thickness(3, 1, 3, 0);
        });

    /// <summary>Betrag und Währung, z. B. Preise in Listen: <c>MultiBinding</c> aus Betrag und Währungscode.</summary>
    public static readonly IMultiValueConverter MoneyText =
        new FuncMultiValueConverter<object?, string>(values =>
        {
            var list = values.ToList();
            return list.Count > 0 && list[0] is decimal amount ? Money.Format(amount, list.Count > 1 ? list[1] as string : null) : "";
        });
}
