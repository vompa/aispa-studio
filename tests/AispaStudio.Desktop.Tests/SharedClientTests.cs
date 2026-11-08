using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä282 (Slice 120): Der HTTP-Zugang zur API liegt in einer Bibliothek ohne Oberfläche und ohne Windows, die ViewModels hängen an einer
/// Schnittstelle. Dazu Schutztests des verschobenen HTTP-Teils: Ablehnung, Fehlertexte, Offline-Erkennung bleiben wie bisher.
/// </summary>
public sealed class SharedClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] PlatformPrefixes =
        ["Avalonia", "CommunityToolkit", "Microsoft.Win32", "Microsoft.Windows", "System.Security.Cryptography.ProtectedData", "WindowsBase"];

    private static readonly string[] HttpTypes =
        ["AispaStudio.Desktop.Services.ApiClient", "AispaStudio.Client.HttpApiClient", "System.Net.Http.HttpClient", "System.Net.Http.HttpMessageHandler"];

    [Fact]
    public void Die_gemeinsame_Bibliothek_haengt_weder_an_Avalonia_noch_an_Windows()
    {
        var library = typeof(ApiResult<>).Assembly;

        Assert.Equal("AispaStudio.Client", library.GetName().Name);
        Assert.DoesNotContain(library.GetReferencedAssemblies(), r => PlatformPrefixes.Any(p => r.Name!.StartsWith(p, StringComparison.Ordinal)));
        Assert.DoesNotContain(library.GetTypes(), t => t.GetCustomAttribute<SupportedOSPlatformAttribute>() is not null);

        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "AispaStudio.Client", "AispaStudio.Client.csproj"));
        Assert.Empty(project.Descendants("PackageReference"));
        var reference = Assert.Single(project.Descendants("ProjectReference"));
        Assert.Equal("AispaStudio.Contracts.csproj", Path.GetFileName(reference.Attribute("Include")!.Value.Replace('\\', '/')));
    }

    [Fact]
    public void Kein_ViewModel_haengt_an_der_Klasse_ApiClient_oder_an_HTTP()
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        // Zustandsautomaten von async-Methoden zählen nicht: ihre Felder sind lokale Variablen (z. B. der Handler einer Statusabfrage), keine Abhängigkeit.
        var viewModels = typeof(ApiClient).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("AispaStudio.Desktop.ViewModels", StringComparison.Ordinal) == true)
            .Where(t => !typeof(System.Runtime.CompilerServices.IAsyncStateMachine).IsAssignableFrom(t));

        var found = viewModels.SelectMany(t =>
                t.GetConstructors(All).SelectMany(c => c.GetParameters()).Select(p => (t, p.ParameterType, p.Name))
                    .Concat(t.GetFields(All).Select(f => (t, f.FieldType, (string?)f.Name)))
                    .Concat(t.GetProperties(All).Select(p => (t, p.PropertyType, (string?)p.Name))))
            .Where(m => UsesHttp(m.Item2))
            .Select(m => $"{m.t.Name}.{m.Item3}: {m.Item2.Name}")
            .Distinct()
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public async Task Abgelehnte_Buchung_liefert_Begruendung_und_Ablehnung()
    {
        var rejected = new BookingRejected("Raum belegt", [new BookingConflict(ResourceKind.Room, Guid.NewGuid(), "Raum A", "schon gebucht")]);
        var api = new StubApi().On("POST /api/appointments", rejected, HttpStatusCode.Conflict).Client();

        var result = await api.PostAsync<AppointmentDto>("api/appointments", new { }, Ct);

        Assert.Equal("Raum belegt Raum A: schon gebucht.", result.Error);
        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("Raum belegt", result.Rejection?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Nicht mehr angemeldet.")]
    [InlineData(HttpStatusCode.Forbidden, "Dafür fehlt die Berechtigung.")]
    [InlineData(HttpStatusCode.NotFound, "Nicht gefunden.")]
    [InlineData(HttpStatusCode.InternalServerError, "Fehler 500.")]
    public async Task Fehler_ohne_Text_ergeben_die_bekannte_Meldung(HttpStatusCode status, string expected)
    {
        var api = new StubApi().On("GET /api/locations", null, status).Client();

        var result = await api.GetAsync<List<LocationDto>>("api/locations", Ct);

        Assert.Equal(expected, result.Error);
        Assert.Null(result.Rejection);
    }

    [Fact]
    public async Task Ohne_Verbindung_meldet_der_Client_offline_einmal()
    {
        var api = new StubApi { Down = true }.Client();
        var changes = 0;
        api.ConnectivityChanged += () => changes++;

        await api.GetAsync<List<LocationDto>>("api/locations", Ct);
        var result = await api.GetAsync<List<LocationDto>>("api/locations", Ct);

        Assert.Equal((HttpStatusCode)0, result.Status);
        Assert.StartsWith("Server nicht erreichbar", result.Error, StringComparison.Ordinal);
        Assert.True(api.IsOffline);
        Assert.Equal(1, changes);
    }

    /// <summary>Der Typ selbst oder ein Typargument darin (<c>Func&lt;ApiClient&gt;</c>, <c>Lazy&lt;HttpClient&gt;</c>) ist ein HTTP-Typ.</summary>
    private static bool UsesHttp(Type type) =>
        HttpTypes.Contains(type.FullName ?? type.Name) || type.GetGenericArguments().Any(UsesHttp) || (type.HasElementType && UsesHttp(type.GetElementType()!));

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AispaStudio.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }
}
