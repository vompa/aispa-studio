using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Startprüfung der Windows-App (docs/installation/README.md, „Systemvoraussetzungen“): was den Start verhindert, was nur ein Hinweis ist und welchen Weg die
/// Karte „Keine Verbindung zum Server“ nennt. Die Werte stehen als Grenzwerte im Test: je der erlaubte und der abgelehnte Fall.
/// </summary>
public sealed class StartupRequirementsTests
{
    private const long Gb = 1024L * 1024 * 1024;

    /// <summary>Ein ausreichender PC; jeder Test ändert nur den Wert, um den es geht.</summary>
    internal sealed record Facts(
        int Build = 26100,
        bool Is64Bit = true,
        long? Memory = 8 * Gb,
        long? Disk = 50 * Gb,
        bool WebView2 = true,
        bool Docker = true) : ISystemFacts
    {
        public int WindowsBuild => Build;

        public bool Is64BitOperatingSystem => Is64Bit;

        public long? TotalMemoryBytes => Memory;

        public long? FreeDiskBytes => Disk;

        public string DataDrive => "C:";

        public bool HasWebView2 => WebView2;

        public bool HasDocker => Docker;
    }

    private static IReadOnlyList<StartupFinding> Blocking(Facts facts, bool standalone = false) =>
        [.. StartupRequirements.Check(facts, standalone).Where(f => f.Severity == StartupSeverity.Blocking)];

    private static IReadOnlyList<StartupFinding> Hints(Facts facts, bool standalone = false) =>
        [.. StartupRequirements.Check(facts, standalone).Where(f => f.Severity == StartupSeverity.Hint)];

    [Fact]
    public void Ein_ausreichender_PC_ergibt_keinen_Fund()
    {
        Assert.Empty(StartupRequirements.Check(new Facts(), standalone: false));
        Assert.Empty(StartupRequirements.Check(new Facts(), standalone: true));
    }

    [Fact]
    public void Ohne_64_Bit_Windows_startet_die_App_nicht()
    {
        var found = Assert.Single(Blocking(new Facts(Is64Bit: false)));
        Assert.Equal("Kein 64-Bit-Windows", found.Title);
        Assert.Empty(Blocking(new Facts(Is64Bit: true)));
    }

    [Fact]
    public void Aelter_als_Windows_10_22H2_Build_19045_startet_die_App_nicht()
    {
        var found = Assert.Single(Blocking(new Facts(Build: 19044)));
        Assert.Equal("Windows ist zu alt", found.Title);
        Assert.Contains("Build 19044", found.Text);
        Assert.Empty(Blocking(new Facts(Build: 19045)));
    }

    [Theory]
    [InlineData(19045, true)]
    [InlineData(21999, true)]
    [InlineData(22000, false)]
    [InlineData(26100, false)]
    public void Windows_10_bekommt_einen_Hinweis_ab_Windows_11_nicht(int build, bool hint)
    {
        var found = Hints(new Facts(Build: build)).Where(f => f.Title == "Windows 10").ToList();
        Assert.Equal(hint, found.Count == 1);
        if (hint)
        {
            Assert.Contains("Windows 11 besser", found[0].Text);
        }
    }

    [Fact]
    public void Zu_altes_Windows_zeigt_keinen_zusaetzlichen_Hinweis_zu_Windows_10()
    {
        Assert.DoesNotContain(Hints(new Facts(Build: 19044)), f => f.Title == "Windows 10");
    }

    [Fact]
    public void Weniger_als_4_GB_Arbeitsspeicher_ergibt_einen_Hinweis_genau_4_GB_nicht()
    {
        var hint = Assert.Single(Hints(new Facts(Memory: 4 * Gb - 1)));
        Assert.Equal("Wenig Arbeitsspeicher", hint.Title);
        Assert.Contains("mindestens 4 GB", hint.Text);
        Assert.DoesNotContain(Hints(new Facts(Memory: 4 * Gb)), f => f.Title == "Wenig Arbeitsspeicher");
        Assert.Empty(Blocking(new Facts(Memory: 4 * Gb - 1)));
    }

    [Fact]
    public void Ist_der_Arbeitsspeicher_unbekannt_gibt_es_keinen_Hinweis()
    {
        Assert.Empty(StartupRequirements.Check(new Facts(Memory: null), standalone: true));
    }

    [Fact]
    public void Ohne_WebView2_nennt_der_Hinweis_den_Weg_und_blockiert_nichts()
    {
        var hint = Assert.Single(Hints(new Facts(WebView2: false)));
        Assert.Equal(StartupSeverity.Hint, hint.Severity);
        Assert.StartsWith("Die Hilfe in der App lässt sich nicht öffnen", hint.Text, StringComparison.Ordinal);
        Assert.Contains("WebView2 Runtime", hint.Text);
        Assert.Contains("kostenlos", hint.Text);
        Assert.Empty(Blocking(new Facts(WebView2: false)));
    }

    [Fact]
    public void Im_Einzelplatz_startet_die_App_mit_weniger_als_1_GB_freiem_Platz_nicht_mit_genau_1_GB_schon()
    {
        var found = Assert.Single(Blocking(new Facts(Disk: Gb - 1), standalone: true));
        Assert.Equal("Zu wenig freier Speicherplatz", found.Title);
        Assert.Contains("Laufwerk C:", found.Text);
        Assert.Empty(Blocking(new Facts(Disk: Gb), standalone: true));
    }

    [Fact]
    public void Mit_Server_zaehlt_der_freie_Platz_nicht()
    {
        Assert.Empty(Blocking(new Facts(Disk: 0), standalone: false));
    }

    [Fact]
    public void Ist_der_freie_Platz_unbekannt_startet_der_Einzelplatz()
    {
        Assert.Empty(Blocking(new Facts(Disk: null), standalone: true));
    }

    [Fact]
    public void Der_Docker_Satz_kommt_nur_bei_localhost_und_127_0_0_1()
    {
        var withoutDocker = new Facts(Docker: false);
        Assert.Contains("Docker Desktop installieren", StartupRequirements.ServerAdvice(withoutDocker, new Uri("http://localhost:5086"))!);
        Assert.Contains("Auf diesem PC läuft kein Server", StartupRequirements.ServerAdvice(withoutDocker, new Uri("http://127.0.0.1:5086"))!);
        Assert.Contains("github.com/vompa/aispa-studio, docs/installation/server.md", StartupRequirements.ServerAdvice(withoutDocker, new Uri("http://localhost:5086"))!);
        Assert.Null(StartupRequirements.ServerAdvice(withoutDocker, new Uri("https://spa.example.at/")));
        Assert.Null(StartupRequirements.ServerAdvice(withoutDocker, new Uri("http://192.168.1.20:5086")));
    }

    [Fact]
    public void Mit_Docker_nennt_der_Satz_den_Befehl_und_den_Einzelplatz()
    {
        var advice = StartupRequirements.ServerAdvice(new Facts(Docker: true), new Uri("http://localhost:5086"))!;
        Assert.Contains("docker compose up -d", advice);
        Assert.Contains("--standalone", advice);
        Assert.DoesNotContain("Docker Desktop installieren", advice);
        Assert.Null(StartupRequirements.ServerAdvice(new Facts(Docker: true), new Uri("https://spa.example.at/")));
    }

    [Fact]
    public void Alle_Texte_halten_die_Textregeln_kein_bitte_kein_Du()
    {
        var all = new Facts(Build: 19044, Is64Bit: false, Memory: Gb, Disk: 0, WebView2: false);
        foreach (var text in StartupRequirements.Check(all, standalone: true).Select(f => f.Text)
                     .Append(StartupRequirements.ServerAdvice(new Facts(Docker: false), new Uri("http://localhost/"))!)
                     .Append(StartupRequirements.ServerAdvice(new Facts(Docker: true), new Uri("http://localhost/"))!))
        {
            Assert.DoesNotContain("bitte", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("erfolgreich", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- Rahmen der App: Prüfung vor dem Verbinden ----

    private static StubApi ReadyServer() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("1", "Production", DateTimeOffset.UtcNow))
        .On("GET /api/auth/me", null, HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Ein_Fund_der_den_Start_verhindert_laesst_die_App_nicht_verbinden()
    {
        var api = ReadyServer();
        var vm = new MainViewModel(api.Client(), facts: new Facts(Is64Bit: false, WebView2: false));

        await vm.StartAsync();

        Assert.Empty(api.Calls);
        Assert.True(vm.IsBlocked);
        Assert.True(vm.IsOffline);
        Assert.False(vm.IsConnected);
        // Auf der Karte stehen alle Funde, der blockierende zuerst; die Warnungen oben bleiben leer.
        Assert.Equal(["Kein 64-Bit-Windows", "Hilfe nicht verfügbar"], vm.BlockedFindings.Select(f => f.Title));
        Assert.Empty(vm.Hints);
        Assert.False(vm.AreHintsShown);
    }

    [Fact]
    public async Task Auch_nach_erneutem_Start_bleibt_die_App_gesperrt()
    {
        var api = ReadyServer();
        var vm = new MainViewModel(api.Client(), facts: new Facts(Build: 19044));

        await vm.StartAsync();
        await vm.StartAsync();

        Assert.Empty(api.Calls);
        Assert.True(vm.IsBlocked);
    }

    [Fact]
    public async Task Nur_Hinweise_halten_den_Start_nicht_auf_und_stehen_als_schliessbare_Warnungen_da()
    {
        var api = ReadyServer();
        var vm = new MainViewModel(api.Client(), facts: new Facts(Build: 19045, Memory: 2 * Gb, WebView2: false));

        await vm.StartAsync();

        Assert.NotEmpty(api.Calls);
        Assert.False(vm.IsBlocked);
        Assert.True(vm.IsConnected);
        Assert.Equal(["Wenig Arbeitsspeicher", "Hilfe nicht verfügbar", "Windows 10"], vm.Hints.Select(h => h.Title));
        Assert.True(vm.AreHintsShown);

        vm.Hints[0].CloseCommand.Execute(null);
        Assert.Equal(2, vm.Hints.Count);
        Assert.DoesNotContain(vm.Hints, h => h.Title == "Wenig Arbeitsspeicher");

        // „Erneut versuchen“ bringt geschlossene Hinweise nicht zurück.
        await vm.StartAsync();
        Assert.Equal(2, vm.Hints.Count);
    }

    [Fact]
    public async Task Ein_PC_ohne_Fund_zeigt_weder_Karte_noch_Warnung()
    {
        var vm = new MainViewModel(ReadyServer().Client(), facts: new Facts());

        await vm.StartAsync();

        Assert.False(vm.IsBlocked);
        Assert.Empty(vm.Hints);
        Assert.False(vm.AreHintsShown);
    }

    [Fact]
    public async Task Ohne_Pruefung_wie_bisher_im_Test_und_im_Designer()
    {
        var api = ReadyServer();
        var vm = new MainViewModel(api.Client());

        await vm.StartAsync();

        Assert.NotEmpty(api.Calls);
        Assert.False(vm.IsBlocked);
        Assert.Empty(vm.Hints);
    }

    [Fact]
    public void Die_Offline_Karte_nennt_den_Weg_nur_bei_einer_Adresse_auf_diesem_PC()
    {
        var stub = new StubApi();
        var onThisPc = new MainViewModel(new ApiClient(new Uri("http://localhost:5086"), new MemorySessionStore(), stub), facts: new Facts(Docker: false));
        var elsewhere = new MainViewModel(new ApiClient(new Uri("https://spa.example.at/"), new MemorySessionStore(), stub), facts: new Facts(Docker: false));
        var unchecked_ = new MainViewModel(new ApiClient(new Uri("http://localhost:5086"), new MemorySessionStore(), stub));

        Assert.Contains("Auf diesem PC läuft kein Server", onThisPc.OfflineAdvice);
        Assert.Null(elsewhere.OfflineAdvice);
        Assert.Null(unchecked_.OfflineAdvice);
    }
}
