using AispaStudio.LiveTests;

// Live-Test der Windows-App (ADR 0090), Konsolenbetrieb. Aufruf über scripts/live-tests.ps1; die Starter-App
// (AispaStudio.LiveTests.Launcher) nutzt dieselben Klassen mit Knöpfen.
//   AispaStudio.LiveTests --app <AispaStudio.Desktop.exe> [--api http://localhost:5086/] [--speed slow|normal|fast]
//                         [--step] [--topics customer,appointment] [--out <Ordner>]
// Tasten in der Konsole: Leertaste Pause/Weiter, Enter Einzelschritt (in der Pause), S Bild jetzt, Q Stopp.
Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    var options = LiveOptions.Parse(args);
    var journey = ReceptionJourney.Journey;
    var topics = journey.Select(options.Topics);
    using var session = LiveSession.Start(options, journey.Role);
    Console.WriteLine($"Live-Test {journey.Role} ({string.Join(", ", topics.Select(topic => topic.Name))}): Bericht in {session.Folder}");
    Console.WriteLine("Tasten: Leertaste Pause/Weiter, Enter Einzelschritt, S Bild, Q Stopp");
    session.StepStarted += (number, name) => Console.Write($"{number,3}. {name} … ");
    session.StepFinished += step => Console.WriteLine(step.Passed ? "✓" : $"✗ {step.Error}");
    var code = journey.Run(session, topics);
    Console.WriteLine(session.Result);
    return code;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
