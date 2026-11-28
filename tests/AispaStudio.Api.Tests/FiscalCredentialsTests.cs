using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class FiscalCredentialsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anbieter_ohne_Felder_lehnt_unbekannte_Zugangsdaten_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var response = await spa.Client.PostAsJsonAsync("/api/pos/registers", new
        {
            LocationId = spa.Location.Id,
            Code = $"T-{Guid.NewGuid():N}",
            Country = FiscalCountry.AT,
            ReceiptHeader = "Test-Spa",
            FiscalCredentials = new Dictionary<string, string> { ["unknown"] = "probe" },
        }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await VerifyAdapterBoundaryAsync(Ct);
    }

    private static async Task VerifyAdapterBoundaryAsync(CancellationToken ct)
    {
        const string AdapterMessage = "adapter-marker";
        Assert.False(await FiscalAdapterCalls.TryAsync<bool>(() => throw PosFailure.Conflict(AdapterMessage), false, ct));
        var unknown = await Assert.ThrowsAsync<PosFailure>(() => FiscalAdapterCalls.TryAsync<bool>(() => throw new KeyNotFoundException(AdapterMessage), false, ct));
        Assert.Equal(PosErrorKind.Conflict, unknown.Error.Kind);
        Assert.DoesNotContain(AdapterMessage, unknown.Message, StringComparison.Ordinal);
        Assert.Null(unknown.InnerException);
        using var timeoutClient = new HttpClient(new WaitingHandler()) { Timeout = TimeSpan.FromMilliseconds(40) };
        var available = await FiscalAdapterCalls.TryAsync(async () =>
        {
            using var result = await timeoutClient.GetAsync(new Uri("https://fiscal.test"), ct);
            return result.IsSuccessStatusCode;
        }, false, ct);
        Assert.False(available);

        using var canceled = new CancellationTokenSource();
        var cancellation = await Assert.ThrowsAsync<OperationCanceledException>(() => FiscalAdapterCalls.TryAsync<bool>(async () =>
        {
            await canceled.CancelAsync();
            throw new OperationCanceledException(AdapterMessage, canceled.Token);
        }, false, canceled.Token));
        Assert.Equal(canceled.Token, cancellation.CancellationToken);
        Assert.Null(cancellation.InnerException);
        Assert.DoesNotContain(AdapterMessage, cancellation.Message, StringComparison.Ordinal);
        var invoked = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() => FiscalAdapterCalls.TryAsync(() => Task.FromResult(invoked = true), false, canceled.Token));
        Assert.False(invoked);
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Zugangsdaten_vor_Initialisierung_bleiben_geschuetzt_und_werden_verwaltet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = ProviderHost();
        using var client = Client(host, spa.Cookie);
        const string UnknownAvailability = "ok-unexpected-availability";
        using var unexpectedCreate = await client.PostAsJsonAsync("/api/pos/registers", Input(spa, UnknownAvailability), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, unexpectedCreate.StatusCode);
        var unexpected = (await unexpectedCreate.Content.ReadFromJsonAsync<CashRegisterDto>(TestSpa.Json, Ct))!;
        Assert.Equal(FiscalReadinessState.NotReady, unexpected.Readiness!.State);
        var setup = await client.GetFromJsonAsync<PosSetupDto>($"/api/pos/setup?locationId={spa.Location.Id}", TestSpa.Json, Ct);
        Assert.Contains(unexpected.Code, setup!.NextStep, StringComparison.Ordinal);
        Assert.DoesNotContain(UnknownAvailability, setup.NextStep, StringComparison.Ordinal);
        using var unexpectedCheck = await client.PostAsync($"/api/pos/registers/{unexpected.Id}/fiscal-check", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, unexpectedCheck.StatusCode);
        Assert.DoesNotContain(UnknownAvailability, await unexpectedCheck.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        var key = $"ok-{Guid.NewGuid():N}";
        var input = Input(spa, key);
        using var created = await client.PostAsJsonAsync("/api/pos/registers", input, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.False((await created.Content.ReadAsStringAsync(Ct)).Contains(key, StringComparison.Ordinal));
        var register = (await created.Content.ReadFromJsonAsync<CashRegisterDto>(TestSpa.Json, Ct))!;
        Assert.Equal(1, register.LastReceiptNumber);
        Assert.NotNull(register.CertificateSerial);
        var path = $"/api/pos/registers/{register.Id}";
        var status = await client.GetFromJsonAsync<FiscalCredentialsDto>($"{path}/fiscal-credentials", TestSpa.Json, Ct);
        Assert.Equal(["token"], status!.ConfiguredFields);
        Assert.NotNull(status.UpdatedAt);

        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var encrypted = await db.Set<FiscalCredential>().AsNoTracking().SingleAsync(c => c.RegisterId == register.Id, Ct);
        Assert.False(encrypted.ProtectedValues.Contains(key, StringComparison.Ordinal));
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        Assert.Throws<CryptographicException>(() => protection.CreateProtector("Pos.Fiscal.Credentials", spa.TenantId.ToString(), Guid.NewGuid().ToString()).Unprotect(encrypted.ProtectedValues));
        Assert.Throws<CryptographicException>(() => protection.CreateProtector("Pos.Fiscal.Credentials", Guid.NewGuid().ToString(), register.Id.ToString()).Unprotect(encrypted.ProtectedValues));
        using var check = await client.PostAsync($"{path}/fiscal-check", null, Ct);
        Assert.True((await check.Content.ReadFromJsonAsync<FiscalCheckDto>(TestSpa.Json, Ct))!.Available);

        var replacement = $"rejected-{Guid.NewGuid():N}";
        using var saved = await client.PutAsJsonAsync($"{path}/fiscal-credentials", new FiscalCredentialsInput(new Dictionary<string, string> { ["token"] = replacement }), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False((await saved.Content.ReadAsStringAsync(Ct)).Contains(replacement, StringComparison.Ordinal));
        using var unavailable = await client.PostAsync($"{path}/fiscal-check", null, Ct);
        Assert.False((await unavailable.Content.ReadFromJsonAsync<FiscalCheckDto>(TestSpa.Json, Ct))!.Available);
        Assert.False((await unavailable.Content.ReadAsStringAsync(Ct)).Contains(replacement, StringComparison.Ordinal));
        using var failedSignature = await client.PostAsync($"{path}/null-receipt", null, Ct);
        Assert.True((await TestSpa.ReadAsync<ReceiptDto>(failedSignature, Ct)).SignatureDeviceFailed);
        async Task SellDuringOutageAsync()
        {
            using var opened = await client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, []), TestSpa.Json, Ct);
            var bill = await TestSpa.ReadAsync<BillDto>(opened, Ct);
            using var line = await client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Custom, null, null, 1, 10m, 0, "Massage", TaxCategory.Normal, null, null, null), TestSpa.Json, Ct);
            line.EnsureSuccessStatusCode();
            using var checkout = await client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
                new CheckoutRequest(null, [new(PaymentMethod.Cash, 10m, null, null)]), TestSpa.Json, Ct);
            var sale = await TestSpa.ReadAsync<ReceiptDto>(checkout, Ct);
            Assert.True(sale.SignatureDeviceFailed);
            Assert.False((await checkout.Content.ReadAsStringAsync(Ct)).Contains(replacement, StringComparison.Ordinal));
        }

        await SellDuringOutageAsync();
        using var removed = await client.DeleteAsync($"{path}/fiscal-credentials", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False(await db.Set<FiscalCredential>().AnyAsync(c => c.RegisterId == register.Id, Ct));
        status = await client.GetFromJsonAsync<FiscalCredentialsDto>($"{path}/fiscal-credentials", TestSpa.Json, Ct);
        Assert.Empty(status!.ConfiguredFields);
        await SellDuringOutageAsync();
        Assert.False(await db.Set<FiscalCredential>().AnyAsync(c => c.RegisterId == register.Id, Ct));

        // Beide deutschen Kassen verwenden denselben Anbieter; erreichbar ist nur die mit gültigem Zugang.
        var firstGerman = Input(spa, key) with { Country = FiscalCountry.DE };
        var secondGerman = Input(spa, replacement) with { Country = FiscalCountry.DE };
        using var firstResponse = await client.PostAsJsonAsync("/api/pos/registers", firstGerman, TestSpa.Json, Ct);
        using var secondResponse = await client.PostAsJsonAsync("/api/pos/registers", secondGerman, TestSpa.Json, Ct);
        var first = await TestSpa.ReadAsync<CashRegisterDto>(firstResponse, Ct);
        var second = await TestSpa.ReadAsync<CashRegisterDto>(secondResponse, Ct);
        Assert.True(first.IsPractice);
        Assert.True(second.IsPractice);
        Assert.Contains(first.Warnings, warning => warning.Contains("Test-TSE", StringComparison.Ordinal));
        using var germanReceipt = await client.PostAsync($"/api/pos/registers/{first.Id}/null-receipt", null, Ct);
        Assert.True((await TestSpa.ReadAsync<ReceiptDto>(germanReceipt, Ct)).IsPractice);
        using var secondCheck = await client.PostAsync($"/api/pos/registers/{second.Id}/fiscal-check", null, Ct);
        using var firstCheck = await client.PostAsync($"/api/pos/registers/{first.Id}/fiscal-check", null, Ct);
        Assert.False((await secondCheck.Content.ReadFromJsonAsync<FiscalCheckDto>(TestSpa.Json, Ct))!.Available);
        Assert.True((await firstCheck.Content.ReadFromJsonAsync<FiscalCheckDto>(TestSpa.Json, Ct))!.Available);
        async Task ExportDuringOutageAsync()
        {
            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            var export = new TseExport { RegisterId = second.Id, From = day, Until = day, RequestedAt = DateTimeOffset.UtcNow, Status = TseExportStatus.Pending };
            db.Add(export);
            await db.SaveChangesAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<TseExports>().RunAsync(export.Id, Ct);
            Assert.Equal(TseExportStatus.Failed, export.Status);
            Assert.Equal("Die TSE hat den Export nicht geliefert. Bitte erneut anfordern.", export.Error);
        }

        await ExportDuringOutageAsync();
        using var removedGerman = await client.DeleteAsync($"/api/pos/registers/{second.Id}/fiscal-credentials", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removedGerman.StatusCode);
        await ExportDuringOutageAsync();
        Assert.False(await db.Set<FiscalCredential>().AnyAsync(c => c.RegisterId == second.Id, Ct));
    }

    [Fact]
    public async Task Fehlende_oder_abgelehnte_Zugangsdaten_hinterlassen_keine_Kassa()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = ProviderHost(misclassifyGerman: true);
        using var client = Client(host, spa.Cookie);
        var invalid = $"invalid-{Guid.NewGuid():N}";
        var inputs = new[] { Input(spa, null), Input(spa, invalid), Input(spa, "fail-start") };
        foreach (var input in inputs)
        {
            using var response = await client.PostAsJsonAsync("/api/pos/registers", input, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False((await response.Content.ReadAsStringAsync(Ct)).Contains(invalid, StringComparison.Ordinal));
        }

        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        using var contradictory = await client.PostAsJsonAsync("/api/pos/registers", Input(spa, "ok-demo") with { Country = FiscalCountry.DE }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, contradictory.StatusCode);
        Assert.False(await db.CashRegisters.AnyAsync(Ct));
        Assert.False(await db.Set<FiscalCredential>().AnyAsync(Ct));
        Assert.False(await db.Receipts.AnyAsync(Ct));
    }

    [Fact]
    public async Task Zugangsdaten_und_Pruefung_verlangen_Kassenstandort_und_Leitungsrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var second = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Monday], Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", Input(spa, null) with { FiscalProvider = null }, Ct);
        using var local = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [second.Id]);
        using var outsider = await TestUsers.ClientAsync(fixture, Ct);
        foreach (var payload in new[] { "{}", "{\"values\":null}" })
        {
            using var missing = new StringContent(payload, global::System.Text.Encoding.UTF8, "application/json");
            using var rejected = await local.PutAsync($"/api/pos/registers/{register.Id}/fiscal-credentials", missing, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        foreach (var (client, query, expected) in new[] {
            (local, "", HttpStatusCode.OK), (reception, "", HttpStatusCode.Forbidden),
            (manager, $"?locationId={second.Id}", HttpStatusCode.Forbidden), (outsider, "", HttpStatusCode.NotFound) })
        {
            var path = $"/api/pos/registers/{register.Id}/fiscal-credentials{query}";
            using var get = await client.GetAsync(path, Ct);
            using var put = await client.PutAsJsonAsync(path, new FiscalCredentialsInput(new Dictionary<string, string>()), TestSpa.Json, Ct);
            using var delete = await client.DeleteAsync(path, Ct);
            using var check = await client.PostAsync($"/api/pos/registers/{register.Id}/fiscal-check{query}", null, Ct);
            Assert.Equal(expected, get.StatusCode);
            Assert.Equal(expected, put.StatusCode);
            Assert.Equal(expected == HttpStatusCode.OK ? HttpStatusCode.NoContent : expected, delete.StatusCode);
            Assert.Equal(expected, check.StatusCode);
        }
    }

    private WebApplicationFactory<Program> ProviderHost(bool misclassifyGerman = false) => fixture.Factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            var provider = new FiscalProviderDto("protected", "Prüfgerät", FiscalProviderKind.Test, true, [new("token", "Zugang", true)], "kassa");
            services.AddFiscalProvider<IRksvSignatureDevice, ProtectedDevice>(FiscalCountry.AT, provider);
            services.AddFiscalProvider<ITseClient, ProtectedTse>(FiscalCountry.DE, provider with { IsTest = !misclassifyGerman });
        }));

    private sealed class ProtectedTse(FiscalCredentialStore store, AispaDbContext db, TimeProvider time) : ITseClient
    {
        private readonly TestTseClient _device = new(db, time);
        public string ProviderId => "protected";
        public bool IsTest => true;

        public async Task<bool> IsAvailableAsync(CashRegister register, CancellationToken ct) =>
            (await store.ReadAsync(register.Id, ct)).TryGetValue("token", out var token) && token.StartsWith("ok-", StringComparison.Ordinal);

        public Task<string> RegisterClientAsync(string clientId, CancellationToken ct) => _device.RegisterClientAsync(clientId, ct);
        public Task<TseSignature?> SignTransactionAsync(string clientId, string processType, string processData, CancellationToken ct) =>
            _device.SignTransactionAsync(clientId, processType, processData, ct);
        public Task<byte[]?> GetPublicKeyAsync(string clientId, CancellationToken ct) => _device.GetPublicKeyAsync(clientId, ct);
        public async Task<byte[]?> ExportAsync(string clientId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
        {
            var id = await db.CashRegisters.Where(r => r.Code == clientId).Select(r => r.Id).SingleAsync(ct);
            var token = (await store.ReadAsync(id, ct))["token"];
            if (!token.StartsWith("ok-", StringComparison.Ordinal))
            {
                throw new HttpRequestException(token);
            }

            return await _device.ExportAsync(clientId, from, until, ct);
        }
    }

    [Fact]
    public async Task Migration_erhaelt_Chiffrat_beim_Downgrade_und_stellt_geloeschte_Werte_nicht_wieder_her()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        var options = new DbContextOptionsBuilder<AispaDbContext>().UseNpgsql(connection).Options;
        await using var db = new AispaDbContext(options, new TenantContext(new Microsoft.AspNetCore.Http.HttpContextAccessor()));
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA aispa;
            CREATE TABLE aispa."Tenants" ("Id" uuid PRIMARY KEY);
            CREATE TABLE aispa."CashRegisters" ("Id" uuid PRIMARY KEY, "TenantId" uuid REFERENCES aispa."Tenants");
            """, Ct);
        var tenant = Guid.NewGuid();
        var register = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""INSERT INTO aispa."Tenants" VALUES ({tenant})""", Ct);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO aispa."CashRegisters" VALUES ({register}, {tenant})""", Ct);
        var migration = new AispaStudio.Api.Data.Migrations.FiscalCredentials();
        async Task ApplyAsync(bool up)
        {
            var ops = up ? migration.UpOperations : migration.DownOperations;
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(ops, db.GetService<IDesignTimeModel>().Model))
            {
                await db.Database.ExecuteSqlRawAsync(command.CommandText, Ct);
            }
            await transaction.CommitAsync(Ct);
        }

        await ApplyAsync(true);
        await db.Database.ExecuteSqlRawAsync(AispaStudio.Api.Sync.ChangeLog.InstallSql, Ct);
        await db.Database.ExecuteSqlRawAsync("SELECT sync.attach_all()", Ct);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO aispa."FiscalCredential" ("RegisterId","TenantId","ProtectedValues","UpdatedAt")
            VALUES ({register},{tenant},'test-ciphertext',now())
            """, Ct);
        await ApplyAsync(false);
        await db.Database.ExecuteSqlRawAsync("""
            DROP TABLE aispa."CashRegisters";
            DROP TABLE aispa."Tenants";
            CREATE TABLE aispa."Tenants" ("Id" uuid PRIMARY KEY);
            CREATE TABLE aispa."CashRegisters" ("Id" uuid PRIMARY KEY, "TenantId" uuid REFERENCES aispa."Tenants");
            """, Ct);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO aispa."Tenants" VALUES ({tenant})""", Ct);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO aispa."CashRegisters" VALUES ({register}, {tenant})""", Ct);
        await ApplyAsync(true);
        Assert.Equal("test-ciphertext", await db.Database.SqlQueryRaw<string>("""SELECT "ProtectedValues" AS "Value" FROM aispa."FiscalCredential" """).SingleAsync(Ct));
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM aispa."FiscalCredential" """, Ct);
        await ApplyAsync(false);
        await ApplyAsync(true);
        Assert.False(await db.Database.SqlQueryRaw<string>("""SELECT "ProtectedValues" AS "Value" FROM aispa."FiscalCredential" """).AnyAsync(Ct));
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, string cookie)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static CashRegisterInput Input(TestSpa spa, string? key) => new(spa.Location.Id, $"P-{Guid.NewGuid():N}", FiscalCountry.AT,
        "Test-Spa", null, null, null, "protected", FiscalCredentials: key is null ? null : new Dictionary<string, string> { ["token"] = key });

    private sealed class ProtectedDevice(FiscalCredentialStore store, AispaDbContext db, TestSignatureDeviceSwitch failure) : IRksvSignatureDevice
    {
        private readonly TestSignatureDevice _device = new(db, failure);
        public string ProviderId => "protected";
        public string ZdaId => "AT0";

        public async Task<string> SetUpAsync(CashRegister register, CancellationToken ct)
        {
            var key = (await store.ReadAsync(register.Id, ct))["token"];
            if (!key.StartsWith("ok-", StringComparison.Ordinal) && key != "fail-start") throw new InvalidOperationException(key);
            return await _device.SetUpAsync(register, ct);
        }

        public async Task<bool> IsAvailableAsync(CashRegister register, CancellationToken ct)
        {
            var values = await store.ReadAsync(register.Id, ct);
            if (!values.TryGetValue("token", out var key)) return false;
            if (key == "ok-unexpected-availability") throw new KeyNotFoundException(key);
            if (!key.StartsWith("ok-", StringComparison.Ordinal)) throw new InvalidOperationException(key);
            return true;
        }

        public async Task<byte[]?> SignAsync(CashRegister register, byte[] data, CancellationToken ct)
        {
            var key = (await store.ReadAsync(register.Id, ct))["token"];
            if (key.StartsWith("rejected-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(key);
            }

            return key == "fail-start" ? null : await _device.SignAsync(register, data, ct);
        }

        public Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) => _device.GetCertificateAsync(register, ct);
    }
}
