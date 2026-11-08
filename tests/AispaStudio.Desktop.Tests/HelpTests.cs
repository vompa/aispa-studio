using System.Text.RegularExpressions;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Help;

namespace AispaStudio.Desktop.Tests;

/// <summary>Hilfe zu allen Einstellungen: dieselben Texte wie in der Team-Oberfläche, eingebaut, und jedes Fragezeichen findet seine Stelle.</summary>
public sealed partial class HelpTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AispaStudio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository nicht gefunden.");
    }

    [Fact]
    public void Liest_Ueberschriften_Absaetze_Listen_und_Hervorhebungen_wie_die_Team_Oberflaeche()
    {
        var blocks = HelpMarkdown.Parse("# Titel\n\n## Erster Teil {#erst}\nText mit **fett**, `Code` und [Link](https://x.test).\nweiter\n\n- eins\n- zwei\n  noch zwei\n\n1. Schritt");

        Assert.Equal(new HelpHeading(2, "Erster Teil", "erst"), blocks[1]);
        Assert.Equal([new("Text mit "), new("fett", Bold: true), new(", "), new("Code", Code: true), new(" und "), new("Link", Href: "https://x.test"), new(". weiter")],
            ((HelpParagraph)blocks[2]).Spans);
        var list = (HelpList)blocks[3];
        Assert.False(list.Ordered);
        Assert.Equal("zwei noch zwei", Assert.Single(list.Items[1]).Text);
        Assert.True(((HelpList)blocks[4]).Ordered);
        Assert.Equal("oeffnungszeiten-raeume", HelpMarkdown.Slug("Öffnungszeiten & Räume"));
    }

    [Fact]
    public void Alle_Hilfetexte_sind_eingebaut()
    {
        var files = Directory.GetFiles(Path.Combine(RepoRoot(), "web", "src", "help", "content"), "*.md").Select(Path.GetFileNameWithoutExtension).Order();

        Assert.Equal(files, HelpContent.Topics.Select(t => t.Id).Order());
        Assert.All(HelpContent.Topics, t => Assert.NotEqual(t.Id, t.Title));
        Assert.True(HelpContent.Find("kassa")!.HasSection("barzahlungsrabatt"));
    }

    [Fact]
    public void Liest_Kopfangaben_Bilder_und_Verweise_in_die_Hilfe()
    {
        var topic = HelpMarkdown.ToTopic("kalender-termin-verschieben",
            "---\nart: ablauf\nbereich: Kalender\noberflaeche: windows-app\nrollen: rezeption, leitung\nseite: kalender\nstichwoerter: verlegen, umbuchen\n---\n"
            + "# Termin verschieben\n\nEinleitung.\n\n## Per Ziehen {#ziehen}\n\n1. Ziehen.\n\n![Beim Ziehen](images/app/kalender-ziehen.png)\n\nSiehe [Stornieren](hilfe:kalender-termin-stornieren#no-show).");

        Assert.Equal("Termin verschieben", topic.Title);
        Assert.Equal(new HelpMeta(HelpKind.Task, "Kalender", ["windows-app"], ["rezeption", "leitung"], "kalender", ["verlegen", "umbuchen"]),
            topic.Meta with { Surfaces = [.. topic.Meta.Surfaces], Roles = [.. topic.Meta.Roles], Keywords = [.. topic.Meta.Keywords] },
            new MetaComparer());
        Assert.Equal("Einleitung.", topic.Summary);
        Assert.Equal(new HelpImage("app/kalender-ziehen", "Beim Ziehen"), Assert.Single(topic.Images));
        Assert.Equal(("kalender-termin-stornieren", "no-show"), Assert.Single(topic.Links));
        Assert.Equal(4, topic.Section("ziehen").Count);
    }

    [Fact]
    public void Jeder_Verweis_zeigt_auf_ein_vorhandenes_Thema_und_jedes_Bild_ist_erzeugt()
    {
        var images = Path.Combine(RepoRoot(), "web", "src", "help", "images");
        Assert.All(HelpContent.Topics, t =>
        {
            foreach (var (topic, section) in t.Links)
            {
                var target = HelpContent.Find(topic);
                Assert.True(target is not null, $"{t.Id}: Verweis auf fehlendes Thema {topic}");
                Assert.True(section is null || target.HasSection(section), $"{t.Id}: Verweis auf fehlenden Abschnitt {topic}#{section}");
            }

            foreach (var image in t.Images)
            {
                Assert.True(File.Exists(Path.Combine(images, $"{image.Name}.png")),
                    $"{t.Id}: Bild images/{image.Name}.png fehlt (scripts/help-images.sh bzw. web/scripts/help-images.mjs)");
            }
        });
    }

    [Fact]
    public void Jede_Seite_der_App_hat_eine_Erklaerung()
    {
        foreach (var page in (string[])["kalender", "kunden", "anfragen", "kassa", "geraete", "verwaltung", "anmeldung"])
        {
            Assert.True(HelpContent.ForPage(page) is not null, $"Seitenthema für {page} fehlt");
        }
    }

    [Fact]
    public void Suche_findet_den_Ablauf_und_reiht_ihn_vor_die_Einstellungen()
    {
        var context = new HelpContext(HelpContent.Surface, HelpContent.RoleKey(AispaStudio.Contracts.StaffRole.Reception), null);
        var hits = HelpSearch.Search(HelpContent.Topics, "Wie verschiebe ich einen Termin?", context, 5);

        Assert.NotEmpty(hits);
        Assert.Equal(HelpKind.Task, hits[0].Topic.Meta.Kind);
        Assert.Contains("verschieb", hits[0].Topic.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(HelpSearch.Search(HelpContent.Topics, "wie kann ich", context, 5));
    }

    [Fact]
    public void Der_Kurzhinweis_nennt_den_ersten_Schritt_des_Abschnitts_und_ohne_Abschnitt_den_ersten_Absatz()
    {
        var topic = HelpContent.Find("kalender-termin-verschieben")!;

        Assert.StartsWith("Den Termin mit der Maus fassen", Controls.HelpTeaser.ExcerptOf(topic, "schritte"), StringComparison.Ordinal);
        Assert.Equal(topic.Summary, Controls.HelpTeaser.ExcerptOf(topic, null));
        Assert.Equal(topic.Summary, Controls.HelpTeaser.ExcerptOf(topic, "gibt-es-nicht"));
    }

    [Fact]
    public void Jedes_Fragezeichen_der_App_zeigt_auf_ein_vorhandenes_Thema_und_einen_vorhandenen_Abschnitt()
    {
        var views = Directory.GetFiles(Path.Combine(RepoRoot(), "src", "AispaStudio.Desktop", "Views"), "*.axaml");
        var links = views.SelectMany(f => HelpLink().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Topic: m.Groups[1].Value,
            Section: m.Groups[2].Success ? m.Groups[2].Value : null))).ToList();

        Assert.NotEmpty(links);
        Assert.All(links, l =>
        {
            var topic = HelpContent.Find(l.Topic);
            Assert.True(topic is not null, $"{l.File}: Thema {l.Topic}");
            Assert.True(l.Section is null || topic.HasSection(l.Section), $"{l.File}: Abschnitt {l.Topic}#{l.Section}");
        });
    }

    [Fact]
    public void Jedes_feste_Hilfeziel_im_Code_der_App_loest_ein_vorhandenes_Thema_auf()
    {
        var sources = Directory.GetFiles(Path.Combine(RepoRoot(), "src", "AispaStudio.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        var topics = sources.SelectMany(f => HelpTopicConstant().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Topic: m.Groups[1].Value))).ToList();

        Assert.NotEmpty(topics);
        Assert.All(topics, t => Assert.True(HelpContent.Find(t.Topic) is not null, $"{t.File}: Thema {t.Topic}"));
    }

    [GeneratedRegex("(?:HelpTopic|ManualTopic)\\s*=\\s*\"([a-z0-9-]+)\"")]
    private static partial Regex HelpTopicConstant();

    [Fact]
    public async Task Fragezeichen_oeffnet_die_Hilfe_in_der_App_beim_Thema_und_Abschnitt_der_Hilfe_Seite()
    {
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        await shell.ShowHelpAsync("kassa", "barzahlungsrabatt");

        Assert.Same(shell.Help, shell.SelectedPage!.Page);
        var address = Assert.IsType<Uri>(shell.Help.Address);
        Assert.Equal("/help/", address.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(address.Query);
        Assert.Equal("kassa", query["thema"]);
        Assert.Equal("barzahlungsrabatt", query["abschnitt"]);
        Assert.Equal("windows-app", query["oberflaeche"]);
        Assert.Equal("rezeption", query["rolle"]);
    }

    [Fact]
    public void Mit_der_Hilfe_neben_der_App_oeffnet_sich_die_Seite_ueber_den_virtuellen_Host_ohne_Server()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aispa-hilfe-{Guid.NewGuid():N}", "web");
        Directory.CreateDirectory(Path.Combine(root, "help"));
        File.WriteAllText(Path.Combine(root, "help", "index.html"), "<!doctype html>");
        var help = new HelpViewModel(() => throw new InvalidOperationException("Der Server wird nicht gebraucht."), root) { Role = AispaStudio.Contracts.StaffRole.Reception };

        help.Show("kassa", "barzahlungsrabatt");

        Assert.True(help.IsLocal);
        Assert.Null(help.Error);
        Assert.Equal(root, help.LocalFolder);
        var address = help.Address!;
        Assert.Equal("https", address.Scheme);
        Assert.Equal(HelpViewModel.VirtualHost, address.Host);
        Assert.Equal("/help/index.html", address.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(address.Query);
        Assert.Equal("kassa", query["thema"]);
        Assert.Equal("barzahlungsrabatt", query["abschnitt"]);
        Assert.Equal("rezeption", query["rolle"]);
        Assert.False(address.IsFile);
        Assert.True(help.IsOwnPage(new Uri($"https://{HelpViewModel.VirtualHost}/assets/help.js")));
        Assert.False(help.IsOwnPage(new Uri("https://example.test/help/")));
        Assert.False(help.IsOwnPage(new Uri(Path.Combine(root, "help", "index.html"))));

        help.ReportMappingFailed();
        Assert.Null(help.Address);
        Assert.NotNull(help.Error);
    }

    [Fact]
    public void Der_Weg_fuer_die_Ordnerzuordnung_steckt_noch_in_der_Avalonia_Assembly()
    {
        var type = typeof(Avalonia.Controls.NativeWebView).Assembly.GetType(Views.EmbeddedBrowser.FolderMappingInterface);

        Assert.NotNull(type);
        var method = type.GetMethod("SetVirtualHostNameToFolderMapping");
        Assert.NotNull(method);
        Assert.Equal([typeof(string), typeof(string), typeof(int)], method.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void Ohne_Hilfe_neben_der_App_und_ohne_Server_steht_ein_Hinweis_da_und_mit_Server_die_Seite_des_Servers()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"aispa-hilfe-{Guid.NewGuid():N}");

        var without = new HelpViewModel(() => null, missing);
        without.Show("kassa", null);
        Assert.Null(without.Address);
        Assert.Contains("nicht eingebaut", without.Error);

        var withServer = new HelpViewModel(() => new Uri("https://spa.test:8443/"), missing);
        withServer.EnsureShown();
        Assert.False(withServer.IsLocal);
        Assert.Equal("https://spa.test:8443/help/", withServer.Address!.GetLeftPart(UriPartial.Path));
        Assert.DoesNotContain("thema=", withServer.Address.Query);
        Assert.True(withServer.IsOwnPage(new Uri("https://spa.test:8443/assets/x.js")));
        Assert.False(withServer.IsOwnPage(new Uri("https://example.test/")));

        // Jedes Ziel bekommt eine neue Adresse, auch dasselbe noch einmal.
        var first = withServer.Address;
        withServer.Show(null, null);
        Assert.NotEqual(first, withServer.Address);
    }

    [Fact]
    public void Die_Hilfe_Seite_ist_ein_Einstieg_des_Web_Builds()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "web", "help", "index.html")));
        Assert.Contains("help/index.html", File.ReadAllText(Path.Combine(RepoRoot(), "web", "vite.config.ts")));
    }

    [Fact]
    public void Hilfe_Adresse_sagt_der_Seite_dass_sie_eingebettet_ist_und_die_Nachricht_companion_meldet_KI_und_Hilfe()
    {
        // R02 Slice 31 (Ä74): nur mit „eingebettet“ schickt die Hilfe-Seite Strg+Leertaste als Nachricht an die App (appShortcuts.ts).
        var help = new HelpViewModel(() => new Uri("http://localhost:5173/"));
        help.Show("kassa", null);
        var query = System.Web.HttpUtility.ParseQueryString(help.Address!.Query);
        Assert.Equal("1", query["eingebettet"]);

        var requested = 0;
        help.CompanionRequested += () => requested++;
        help.OnWebMessage("""{"type":"companion"}""");
        help.OnWebMessage("""{"type":"saved"}""");
        help.OnWebMessage(null);
        Assert.Equal(1, requested);
    }

    [Fact]
    public async Task Hilfe_vom_Server_nimmt_die_vorgegebene_Adresse_sonst_die_vom_Server_genannte()
    {
        // Slice 103 (Annahme 8): übrig von der Suche nach der Team-Oberfläche, ohne Prüfung auf eine index.html an der Wurzel.
        var stub = new StubApi().On("GET /api/system/team-web", new AispaStudio.Contracts.TeamWebInfo("http://localhost:5173/"));
        var configured = new ServerWeb(stub.Client(), new Uri("http://localhost:62346/"));
        await configured.LookUpAsync();
        Assert.Equal(new Uri("http://localhost:62346/"), configured.Address);
        Assert.Empty(stub.Calls);

        var named = new ServerWeb(stub.Client());
        Assert.Equal(new Uri("http://localhost/"), named.Address);
        await named.LookUpAsync();
        Assert.Equal(new Uri("http://localhost:5173/"), named.Address);
        Assert.Equal(new Uri("http://localhost:5173/help/?eingebettet=1&oberflaeche=windows-app&n=1"), ShownAt(named));

        var silent = new ServerWeb(new StubApi().Client());
        await silent.LookUpAsync();
        Assert.Equal(new Uri("http://localhost/"), silent.Address);
    }

    [Fact]
    public async Task Die_Hilfe_zeigt_erst_wenn_der_Server_seine_Web_Adresse_genannt_hat()
    {
        // Slice 103 (Review): ohne Warten lud das erste Anzeigen die Hilfe unter der Adresse der API, die dort keine Hilfe ausliefert.
        var asked = new TaskCompletionSource();
        var web = new Uri("http://localhost/");
        var help = new HelpViewModel(() => web, webRoot: Path.Combine(Path.GetTempPath(), "aispa-ohne-hilfe"), prepare: () => asked.Task);

        var shown = help.ShowAsync("kassa", null);
        Assert.Null(help.Address);

        web = new Uri("http://localhost:5173/");
        asked.SetResult();
        await shown;

        Assert.Equal("http://localhost:5173/help/", help.Address!.GetLeftPart(UriPartial.Path));
        Assert.Equal("kassa", System.Web.HttpUtility.ParseQueryString(help.Address.Query)["thema"]);
    }

    private static Uri? ShownAt(ServerWeb server)
    {
        var help = new HelpViewModel(() => server.Address, webRoot: Path.Combine(Path.GetTempPath(), "aispa-ohne-hilfe"));
        help.Show(null, null);
        return help.Address;
    }

    private sealed class MetaComparer : IEqualityComparer<HelpMeta>
    {
        public bool Equals(HelpMeta? x, HelpMeta? y) => x is not null && y is not null && x.Kind == y.Kind && x.Area == y.Area && x.Page == y.Page
            && x.Surfaces.SequenceEqual(y.Surfaces) && x.Roles.SequenceEqual(y.Roles) && x.Keywords.SequenceEqual(y.Keywords);

        public int GetHashCode(HelpMeta obj) => obj.Kind.GetHashCode();
    }

    [GeneratedRegex("<ui:HelpButton\\s+Topic=\"([^\"]+)\"(?:\\s+Section=\"([^\"]+)\")?")]
    private static partial Regex HelpLink();
}
