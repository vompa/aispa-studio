using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Panel „Artikel und Gutscheine“ an der Kassa: gemerkter Zustand je Arbeitsplatz (Slice 129, Ä310), zu Beginn zugeklappt (Slice 137, Ä345).</summary>
public sealed class PosSalesColumnTests
{
    private static PosViewModel Pos(ISettingsStore store) => new(new StubApi().Client(), TimeProvider.System, store);

    [Fact]
    public void Neue_Kassa_ist_zugeklappt_ohne_gespeicherten_Zustand()
    {
        var pos = Pos(new MemorySettingsStore());

        Assert.False(pos.IsSalesColumnOpen);
    }

    [Fact]
    public void Aufklappen_wird_je_Arbeitsplatz_gespeichert_und_beim_naechsten_Start_gelesen()
    {
        var store = new MemorySettingsStore();
        var pos = Pos(store);

        pos.IsSalesColumnOpen = true;

        Assert.True(store.Load().PosSalesPanelOpen);
        Assert.True(Pos(store).IsSalesColumnOpen);

        pos.IsSalesColumnOpen = false;

        Assert.False(store.Load().PosSalesPanelOpen);
        Assert.False(Pos(store).IsSalesColumnOpen);
    }

    [Fact]
    public void Der_gemerkte_Zustand_ueberschreibt_keine_anderen_Einstellungen()
    {
        var store = new MemorySettingsStore();
        store.Save(new DesktopSettings(PrinterAddress: "192.168.1.60", CalendarAllStaff: true));

        Pos(store).IsSalesColumnOpen = true;

        var saved = store.Load();
        Assert.Equal("192.168.1.60", saved.PrinterAddress);
        Assert.True(saved.CalendarAllStaff);
        Assert.True(saved.PosSalesPanelOpen);
    }
}
