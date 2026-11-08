using AispaStudio.Api.Demo.Memory;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;

namespace AispaStudio.Api.Demo;

/// <summary>Die Schritte des Verlaufs im Speicher; die Teile für Buchen und Kassieren stehen in den Dateien daneben.</summary>
internal sealed partial class MemoryDemoOps(DemoSnapshot snapshot, MemoryWorld world, DemoClock clock, FiscalizationResolver fiscal, PaymentOptions payments,
    Guid? siteId) : IDemoOps
{
    private readonly DemoSnapshot _snapshot = snapshot;
    private readonly MemoryWorld _world = world;
    private readonly DemoClock _clock = clock;
    private readonly PaymentOptions _payments = payments;
    private readonly Guid? _siteId = siteId;
    private readonly Dictionary<string, TimeZoneInfo> _zones = [];

    private IFiscalization Fiscal(CashRegister register) => fiscal.For(register.Country);

    /// <summary>Die Abschnitte teilen sich den Speicher; es gibt nichts freizugeben.</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
