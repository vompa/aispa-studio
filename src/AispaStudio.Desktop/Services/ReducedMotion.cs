using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;


namespace AispaStudio.Desktop.Services;

/// <summary>
/// Respektiert die Windows-Einstellung „Animationen anzeigen“: ist sie aus, entfällt jede Bewegung und jeder Übergang (Design-Brief 4.6, Fluent 2 F22):
/// Knopfdruck, Einschweben der Rückmeldung und der Seitenleisten, Farbwechsel bei Hover und Fokus, Unterstrich der Felder, Schalter, auch das Ein- und Ausblenden.
/// Avalonia bietet dafür keine Abfrage, deshalb die Systemeinstellung direkt.
/// </summary>
public static class ReducedMotion
{
    private const uint GetClientAreaAnimation = 0x1042;
    private const string NeverSetClass = "motion-never-set";

    /// <summary>
    /// Die Abfrage der Systemeinstellung (true = Animationen aus); ein Haken, damit Tests unabhängig von Windows laufen:
    /// der Runner von GitHub meldet „Animationen aus“, ein Entwicklungs-PC meist „an“, und die Bewegungstests zählen Übergänge, die dann fehlen.
    /// </summary>
    internal static Func<bool> SystemSource { get; set; } = QuerySystem;

    /// <summary>Die Einstellung ist aus: entfällt jede Bewegung. Auf anderen Systemen als Windows immer false.</summary>
    public static bool IsRequested() => SystemSource();

    private static bool QuerySystem() =>
        OperatingSystem.IsWindows() && SystemParametersInfo(GetClientAreaAnimation, 0, out var enabled, 0) && !enabled;

    /// <summary>Ergänzt die Anwendungsstile, damit sie die Bewegungen aus <c>Styles.axaml</c> überschreiben.</summary>
    public static void Apply(Application application) => Apply(application.Styles);

    /// <summary>Wie <see cref="Apply(Application)"/> auf beliebigen Stilen, damit Tests den Start ohne zweite Anwendung nachstellen.</summary>
    internal static void Apply(Styles styles)
    {
        if (!IsRequested())
        {
            return;
        }

        foreach (var style in Overrides())
        {
            styles.Add(style);
        }
    }

    /// <summary>Die Stile, die bei „Animationen aus“ gelten; getrennt von <see cref="Apply(Styles)"/>, damit Tests sie ohne die Windows-Einstellung prüfen.</summary>
    internal static IEnumerable<Style> Overrides() =>
    [
        // Der Weg entfällt: Drücken skaliert nicht, Meldung und Seitenleiste kommen nicht von unten bzw. rechts.
        NoMovement(x => x.OfType<Button>().Class(":pressed")),
        NoMovement(x => x.OfType<ToggleButton>().Class(":pressed")),
        NoMovement(x => x.OfType<Border>().Class("toast")),
        NoMovement(x => x.OfType<Border>().Class("toast").Class("shown")),
        NoMovement(x => x.OfType<Border>().Class("sidepanel")),
        NoMovement(x => x.OfType<Border>().Class("sidepanel").Class("open")),
        // Alle Übergänge laufen mit 0 ms, auch Deckkraft und Farbwechsel (Fluent 2: 0,01 ms). Der Stil gilt für jedes Steuerelement, also auch für
        // Unterkante und Fokus-Unterstrich der Felder (4.5) und die Teile der Schalter-Vorlage (5.23). Der Ausschluss einer nie vergebenen Klasse macht ihn
        // zu einem Stil mit Aktivator: nur solche schlagen die Übergänge, die Stile mit Klasse oder Zustand (".toast", ":pressed") gesetzt haben.
        NoTransitions(x => x.Is<Control>().Not(y => y.Class(NeverSetClass))),
    ];

    private static Style NoTransitions(Func<Selector?, Selector> selector) =>
        new(selector) { Setters = { new Setter(Animatable.TransitionsProperty, new Transitions()) } };

    private static Style NoMovement(Func<Selector?, Selector> selector) =>
        new(selector) { Setters = { new Setter(Visual.RenderTransformProperty, null) } };

    // DllImport statt LibraryImport: das bräuchte unsicheren Code im ganzen Projekt, für einen einzigen Aufruf.
#pragma warning disable SYSLIB1054
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
#pragma warning restore SYSLIB1054
}
