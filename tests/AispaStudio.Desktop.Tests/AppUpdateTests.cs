using AispaStudio.Desktop.Services;
using Velopack;

namespace AispaStudio.Desktop.Tests;

/// <summary>Setup und Updates (ADR 0104, Entscheidung 1): Programm und Daten liegen getrennt, die Prüfung stört nie.</summary>
public sealed class AppUpdateTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aispa-update-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Datenordner_liegt_nicht_im_Programmordner_des_Setups()
    {
        // Velopack leert bei Deinstallation und Reparatur den ganzen Ordner unter der packId (Probe T-130).
        // Rot, wenn jemand den Datenordner unter %LOCALAPPDATA%\AispaStudioApp legt. Die packId selbst steht in ci.yml und wird hier nicht gelesen.
        var programFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudioApp");

        var relative = Path.GetRelativePath(programFolder, LocalServerPaths.Default.DataDirectory);

        Assert.StartsWith("..", relative, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_wird_beim_Start_nur_ohne_Sicherungsschalter_und_ohne_andere_Instanz_eingespielt()
    {
        // Update.exe beendet jeden Prozess im Programmordner. Rot, wenn die Sicherung oder eine laufende App dabei mitgerissen würde.
        Assert.True(AppUpdate.MayApplyOnStartup([], () => false));
        Assert.False(AppUpdate.MayApplyOnStartup([ScheduledBackup.Argument], () => false));
        Assert.False(AppUpdate.MayApplyOnStartup([], () => true));
    }

    [Fact]
    public async Task Prüfung_tut_ohne_Setup_nichts_und_wirft_nicht()
    {
        // Rot, wenn die Prüfung ohne installierte App (ZIP, Entwicklung) trotzdem zum Netz geht und dabei Fehler wirft oder protokolliert.
        // Wie in Program.Main: Velopack ist initialisiert, aber dieser Prozess ist nicht installiert.
        VelopackApp.Build().SetArgs([]).Run();
        var file = Path.Combine(_root.FullName, "app.log");
        var update = new AppUpdate(new CrashLog(file, TimeProvider.System), "http://127.0.0.1:1/", TimeSpan.Zero);

        await update.CheckAsync();

        Assert.False(File.Exists(file));
    }
}
