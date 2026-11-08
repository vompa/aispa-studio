using AispaStudio.AppHost;
// Startet die komplette Entwicklungsumgebung: PostgreSQL, API und Web-Oberfläche (Vite).
// Aufruf: dotnet run --project src/AispaStudio.AppHost  (Docker muss laufen)
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("aispa-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("aispadb");

// Test-Postfach für Mails aus der Entwicklung (Anmeldecodes, Buchungsbestätigungen); Weboberfläche im Aspire-Dashboard.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");
var smtp = mailpit.GetEndpoint("smtp");

var api = builder.AddProject<Projects.AispaStudio_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Mail__Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Mail__Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Mail__Smtp__Security", "None")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Dashboard-Befehle an PostgreSQL: Sicherung erstellen/einspielen, Beispiel-Spa neu einspielen (nur Entwicklung, nie im Betrieb).
if (builder.ExecutionContext.IsRunMode)
{
    postgres.WithDatabaseCommands(
        Path.Combine(FindRepositoryRoot(builder.AppHostDirectory), DatabaseBackupFiles.FolderName), api.Resource.Name);
}

// Optional lokales KI-Modell (ADR 0057): dotnet run --project src/AispaStudio.AppHost -- --LocalAi:Enabled=true
// Das Modell lädt man danach unter Stammdaten > KI; es bleibt im Volume.
if (bool.TryParse(builder.Configuration["LocalAi:Enabled"], out var localAi) && localAi)
{
    var ollama = builder.AddContainer("ollama", "ollama/ollama", "0.40.6")
        .WithVolume("aispa-ollama-models", "/root/.ollama")
        .WithEnvironment("OLLAMA_KEEP_ALIVE", "10m")
        .WithHttpEndpoint(targetPort: 11434, name: "http");
    api.WithEnvironment("Ai__Local__Url", ollama.GetEndpoint("http"))
        .WithEnvironment("Ai__Local__Model", builder.Configuration["LocalAi:Model"] ?? "gemma3:4b");
}

// Optional Redis als Backplane des Live-Kanals (ADR 0087): dotnet run --project src/AispaStudio.AppHost -- --Live:Backplane=Redis
// Ohne den Schalter fehlt Redis, und die normale Entwicklung bleibt unverändert.
if (string.Equals(builder.Configuration["Live:Backplane"], "Redis", StringComparison.OrdinalIgnoreCase))
{
    // Valkey statt Redis 8 (ADR 0089); das Image bringt redis-server und redis-cli als Verweise mit.
    var redis = builder.AddRedis("redis").WithImage("valkey/valkey").WithImageTag("8-alpine");
    api.WithReference(redis)
        .WaitFor(redis)
        .WithEnvironment("Live__Backplane", "Redis");
}

var web = builder.AddViteApp("web", "../../web")
    .WithReference(api)
    .WaitFor(api);

// Links in Mails zeigen auf die Web-Oberfläche (Kundenbereich unter /buchen/).
api.WithEnvironment("OnlineBooking__PublicUrl", web.GetEndpoint("http"));

// Windows-App auf Knopfdruck im Dashboard („windows-app“ › Starten): verbunden mit dieser API; AISPA_TEAM_WEB_URL nennt Vite, damit die Hilfe
// (help/index.html) beim Entwickeln von dort kommt (Slice 103: eine eingebettete Verwaltung gibt es nicht mehr).
builder.AddProject<Projects.AispaStudio_Desktop>("windows-app")
    .WithEnvironment("AISPA_API_URL", api.GetEndpoint("http"))
    .WithEnvironment("AISPA_TEAM_WEB_URL", web.GetEndpoint("http"))
    .WithExplicitStart()
    .WaitFor(web);

builder.Build().Run();

static string FindRepositoryRoot(string startFolder)
{
    for (var folder = new DirectoryInfo(startFolder); folder is not null; folder = folder.Parent)
    {
        if (File.Exists(Path.Combine(folder.FullName, "AispaStudio.slnx")))
        {
            return folder.FullName;
        }
    }

    return startFolder;
}
