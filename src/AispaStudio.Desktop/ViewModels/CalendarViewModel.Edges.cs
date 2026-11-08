using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Verborgene Spalten an einem Rand des Rasters (R04 Slice 86, Ä154): Zahl, Kurztext „+3“ und die Namen für Hinweis und Screenreader.</summary>
public sealed record ColumnEdge(int Count, string Badge, string Tip);

/// <summary>
/// Weitere Spalten links und rechts (R04 Slice 86, Ä154, Variante 2): die Ansicht meldet den waagrechten Ausschnitt, das ViewModel nennt je Rand
/// die Spalten, die nicht ganz zu sehen sind. Ohne verborgene Spalte an einem Rand ist der Wert dort null, und die Ansicht zeigt weder Verlauf noch Knopf.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Spielraum, damit eine Spalte, die um Rundungsreste aus dem Ausschnitt ragt, nicht als verborgen zählt.</summary>
    private const double EdgeTolerance = 1;

    private double _scrollLeft;
    private double _scrollWidth;

    [ObservableProperty]
    public partial ColumnEdge? LeftEdge { get; private set; }

    [ObservableProperty]
    public partial ColumnEdge? RightEdge { get; private set; }

    /// <summary>Waagrechter Ausschnitt in Koordinaten der Spalten (0 = linker Rand der ersten Spalte) und seine Breite.</summary>
    public void SetHorizontalView(double left, double width)
    {
        (_scrollLeft, _scrollWidth) = (left, width);
        UpdateEdges();
    }

    private void UpdateEdges()
    {
        var before = new List<string>();
        var after = new List<string>();
        if (_scrollWidth > 0)
        {
            for (var i = 0; i < Columns.Count; i++)
            {
                var start = DayLayout.ColumnLeft(i, Scale);
                if (start < _scrollLeft - EdgeTolerance)
                {
                    before.Add(Columns[i].Title);
                }

                if (start + Scale.ColumnWidth > _scrollLeft + _scrollWidth + EdgeTolerance)
                {
                    after.Add(Columns[i].Title);
                }
            }
        }

        LeftEdge = Edge(before, "Zurück");
        RightEdge = Edge(after, "Weiter");
    }

    private static ColumnEdge? Edge(List<string> names, string verb) =>
        names.Count == 0
            ? null
            : new ColumnEdge(names.Count, $"+{names.Count}", $"{verb} zu {names.Count} {(names.Count == 1 ? "Spalte" : "Spalten")}: {string.Join(", ", names)}");
}
