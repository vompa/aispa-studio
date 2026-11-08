using AispaStudio.LiveTests;

// Live-Test der Windows-App (ADR 0090). Aufruf über scripts/live-tests.ps1:
//   AispaStudio.LiveTests --app <AispaStudio.Desktop.exe> [--api http://localhost:5086/] [--speed slow|normal|fast] [--step] [--out <Ordner>]
// Tasten in der Konsole: Leertaste Pause/Weiter, Enter Einzelschritt (in der Pause), S Bild jetzt, Q Stopp.
Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    var options = LiveOptions.Parse(args);
    using var session = LiveSession.Start(options, "Rezeption");
    return ReceptionJourney.Run(session);
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
