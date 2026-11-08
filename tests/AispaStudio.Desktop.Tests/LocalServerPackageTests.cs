using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Einzelplatz mit dem echten Windows-Paket (PostgreSQL, API, Web-Oberfläche): läuft in der CI nach dem Veröffentlichen,
/// wenn <c>AISPA_LOCAL_PACKAGE</c> auf den Paketordner zeigt; sonst übersprungen.
/// </summary>
public sealed class LocalServerPackageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Paket_startet_Datenbank_und_API_richtet_ein_und_startet_erneut()
    {
        var package = Environment.GetEnvironmentVariable("AISPA_LOCAL_PACKAGE");
        if (string.IsNullOrWhiteSpace(package))
        {
            Assert.Skip("AISPA_LOCAL_PACKAGE ist nicht gesetzt.");
        }

        var data = Directory.CreateTempSubdirectory("aispa-einzelplatz-");
        var paths = new LocalServerPaths(package, data.FullName);
        var server = new LocalServer(paths, new SystemProcessRunner(), new PlainProtector());
        var passed = false;
        try
        {
            await server.StartAsync(ct: Ct);
            using var http = new HttpClient { BaseAddress = server.ApiAddress };

            var modules = await http.GetFromJsonAsync<SystemModules>("api/system/modules", ApiClient.Json, Ct);
            Assert.True(modules!.Standalone);
            Assert.Contains("<html", await http.GetStringAsync("", Ct), StringComparison.OrdinalIgnoreCase);
            var setup = await http.PostAsJsonAsync("api/auth/standalone/setup",
                new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), ApiClient.Json, Ct);
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

            // Neustart wie am nächsten Morgen: dieselbe Datenbank, das Konto ist noch da.
            await server.StopAsync();
            var again = new LocalServer(paths, new SystemProcessRunner(), new PlainProtector());
            await again.StartAsync(ct: Ct);
            server = again;
            var login = await http.PostAsJsonAsync("api/auth/standalone/login", new PasswordLogin("eva", "seeblick-2026"), ApiClient.Json, Ct);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            // Tägliche Sicherung (Schritt 5) mit dem echten pg_dump; pg_restore liest sie.
            var code = server.CreateRecoveryKey();
            var dump = await server.BackupAsync(Path.Combine(data.FullName, "sicherung"), Ct);
            var list = await new SystemProcessRunner().RunAsync(paths.Tool("pg_restore"), ["-l", dump], ct: Ct);
            Assert.Equal(0, list.ExitCode);
            Assert.Contains("Customers", list.Output, StringComparison.Ordinal);
            var key = RecoveryKey.Open(await File.ReadAllTextAsync(Path.ChangeExtension(dump, ".schluessel.json"), Ct), code);
            Assert.StartsWith("lokal-", key.KeyId, StringComparison.Ordinal);

            // Neuer PC: leere Installation, dann aus der Sicherung wiederherstellen; Eva meldet sich wie gewohnt an.
            await server.StopAsync();
            var fresh = new LocalServer(paths with { DataDirectory = Path.Combine(data.FullName, "neuer-pc") }, new SystemProcessRunner(),
                new PlainProtector());
            server = fresh;
            await fresh.StartAsync(ct: Ct);
            using var empty = new HttpClient { BaseAddress = fresh.ApiAddress };
            Assert.Contains("true", await empty.GetStringAsync("api/auth/standalone/setup", Ct), StringComparison.Ordinal);
            await fresh.RestoreAsync(dump, code, ct: Ct);
            var restored = await empty.PostAsJsonAsync("api/auth/standalone/login", new PasswordLogin("eva", "seeblick-2026"), ApiClient.Json, Ct);
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            passed = true;
        }
        finally
        {
            await server.StopAsync();
            // Bei einem Fehler bleiben die Protokolle für die CI liegen.
            try
            {
                if (passed)
                {
                    data.Delete(recursive: true);
                }
            }
            catch (IOException)
            {
                // Protokolldateien können unter Windows kurz gesperrt bleiben.
            }
        }
    }
}
