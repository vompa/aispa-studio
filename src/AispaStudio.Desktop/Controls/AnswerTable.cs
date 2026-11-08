using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Tabelle in der Antwort des Assistenten (höchstens zehn Zeilen, ADR 0072). In der schmalen Leiste „KI und Hilfe“ stehen mehrspaltige Tabellen als Liste:
/// je Zeile die erste Zelle fett, darunter die übrigen Zellen mit ihrer Spalte (<c>Rabatt: −10 %</c>); beginnt die Tabelle mit „Platz“, ist die Überschrift
/// der Zeile „1. Name“. Tabellen mit zwei Spalten stehen als Beschriftung links und Wert rechts, wie die Kennzahlen einer Auswertung; die Beschriftung behält ihre
/// Breite, ein langer Wert („Zuständig für: Wasserschaden, Heizung, Warmwasser“) bricht in der Wertspalte um.
/// </summary>
public sealed class AnswerTable : ContentControl
{
    public static readonly StyledProperty<AssistantTable?> TableProperty = AvaloniaProperty.Register<AnswerTable, AssistantTable?>(nameof(Table));

    static AnswerTable()
    {
        TableProperty.Changed.AddClassHandler<AnswerTable>((table, _) => table.Rebuild());
    }

    public AnswerTable()
    {
        AutomationProperties.SetName(this, "Tabelle zur Antwort");
    }

    public AssistantTable? Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    private void Rebuild()
    {
        if (Table is not { } table || table.Rows.Count == 0)
        {
            Content = null;
            return;
        }

        Content = table.Columns.Count == 2 ? Pairs(table) : Stacked(table);
    }

    private static Grid Pairs(AssistantTable table)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        for (var i = 0; i < table.Rows.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Children.Add(Cell(table.Rows[i][0], i, 0, secondary: true));
            var value = Cell(table.Rows[i][1], i, 1, secondary: false);
            value.FontWeight = FontWeight.SemiBold;
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.TextAlignment = TextAlignment.Right;
            grid.Children.Add(value);
        }

        return grid;
    }

    private static StackPanel Stacked(AssistantTable table)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var row in table.Rows)
        {
            var ranked = table.Columns[0] == "Platz" && row.Count > 1;
            var block = new StackPanel { Spacing = 1 };
            block.Children.Add(new TextBlock { Text = ranked ? $"{row[0]}. {row[1]}" : row[0], FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            for (var c = ranked ? 2 : 1; c < row.Count; c++)
            {
                var line = new TextBlock { Text = $"{table.Columns[c]}: {row[c]}", TextWrapping = TextWrapping.Wrap };
                line.Classes.Add("secondary");
                line.Classes.Add("small");
                block.Children.Add(line);
            }

            stack.Children.Add(block);
        }

        return stack;
    }

    private static TextBlock Cell(string text, int row, int column, bool secondary)
    {
        var cell = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2) };
        if (secondary)
        {
            cell.Classes.Add("secondary");
        }

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        return cell;
    }
}
