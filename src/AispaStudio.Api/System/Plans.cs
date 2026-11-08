using AispaStudio.Contracts;

namespace AispaStudio.Api.System;

/// <summary>
/// Welche zuschaltbaren Module eine Abo-Stufe freischaltet (ADR 0015, Paketübersicht im Lizenzkonzept). Ein Modul läuft für ein
/// Spa nur, wenn es in der Installation eingeschaltet ist (<see cref="ModuleSwitches"/>) und die Stufe des Spas es enthält.
/// Höhere Stufen enthalten alles der niedrigeren. Der Kern ist in jeder Stufe da.
/// </summary>
public static class Plans
{
    private static readonly Dictionary<AppModule, SubscriptionPlan> From = new()
    {
        // Start: Online-Buchung und Erinnerungen.
        [AppModule.OnlineBooking] = SubscriptionPlan.Start,
        [AppModule.Reminders] = SubscriptionPlan.Start,
        // Das Dashboard im Pausenraum ist keine Preisstufe wert (Slice 105, Annahme 6): es liegt in jeder Stufe.
        [AppModule.Dashboard] = SubscriptionPlan.Start,
        // Spa: dazu KI-Assistent im Programm und Erinnerung an die nächste Behandlung.
        [AppModule.Assistant] = SubscriptionPlan.Spa,
        [AppModule.Rebooking] = SubscriptionPlan.Spa,
        // Spa Plus: dazu KI-Terminannahme über alle Kanäle und Benchmark.
        [AppModule.BookingIntake] = SubscriptionPlan.SpaPlus,
        [AppModule.Benchmark] = SubscriptionPlan.SpaPlus,
    };

    public static bool Includes(SubscriptionPlan plan, AppModule module) => plan >= From[module];

    /// <summary>Die Stufen mit diesem Modul, z. B. für Abfragen in Hintergrundläufen über alle Spas.</summary>
    public static SubscriptionPlan[] With(AppModule module) => [.. Enum.GetValues<SubscriptionPlan>().Where(p => Includes(p, module))];

    /// <summary>Kleinste Stufe mit diesem Modul, für Meldungen.</summary>
    public static SubscriptionPlan Minimum(AppModule module) => From[module];

    public static string Name(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Start => "Start",
        SubscriptionPlan.Spa => "Spa",
        _ => "Spa Plus",
    };
}
