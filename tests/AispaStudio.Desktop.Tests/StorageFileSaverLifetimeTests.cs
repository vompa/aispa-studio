using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    public enum SaveStep { Download, Picker, Open, Write, Dispose }
    public enum OwnerChange { Navigation, SignOut, Idle, Server }

    [Theory]
    [InlineData(SaveStep.Download, OwnerChange.Navigation)]
    [InlineData(SaveStep.Download, OwnerChange.SignOut)]
    [InlineData(SaveStep.Download, OwnerChange.Idle)]
    [InlineData(SaveStep.Download, OwnerChange.Server)]
    [InlineData(SaveStep.Picker, OwnerChange.SignOut)]
    [InlineData(SaveStep.Open, OwnerChange.SignOut)]
    [InlineData(SaveStep.Write, OwnerChange.SignOut)]
    [InlineData(SaveStep.Dispose, OwnerChange.SignOut)]
    public Task PDF_Speichern_bleibt_an_die_Shell_gebunden(SaveStep pause, OwnerChange change) => Session.Dispatch(async () =>
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pdfRoute = $"GET /api/reports/locations/{ReportSample.LocationId}/management/pdf?from=2026-10-01&until=2026-10-19";
        byte[] pdf = [1, 2, 3];
        var stub = ReportsApi().OnFile(pdfRoute, pdf, "Test.pdf")
            .On("GET /api/auth/quick-switch", new QuickSwitchState(QuickSwitchTests.Device, []))
            .On("POST /api/auth/logout", null, HttpStatusCode.NoContent);
        if (pause == SaveStep.Download)
        {
            stub.Hold(pdfRoute, gate.Task);
        }
        var time = new IdleTime { Now = SpaSample.At(11, 40) };
        var api = stub.Client();
        var main = new MainViewModel(api, time: time, theme: new ThemeController(new MemorySettingsStore()));
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        try
        {
            var (shell, reports) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
            var document = Assert.IsType<DocumentActionsViewModel>(reports.Management.Document);
            var saver = Assert.IsType<StorageFileSaver>(document.Files);
            var calls = new List<SaveStep>();
            async Task Visit(SaveStep step)
            {
                calls.Add(step);
                // Der Testadapter verweigert selbst jeden neuen Zugriff nach Sitzungsende.
                if (step != SaveStep.Dispose && !ReferenceEquals(shell, main.Content))
                {
                    throw new IOException("Speichervorgang außerhalb seiner Sitzung");
                }
                if (step == pause)
                {
                    await gate.Task;
                }
            }

            var stream = new RecordingSaveStream(Visit);
            async Task<Stream> OpenAsync()
            {
                await Visit(SaveStep.Open);
                return stream;
            }
            var file = MemoryStorageProxy.For<IStorageFile>(member => member switch
            {
                nameof(IStorageFile.OpenWriteAsync) => OpenAsync(),
                nameof(IDisposable.Dispose) => null,
                "get_Path" => new Uri("file:///C:/Test.pdf"),
                _ => throw new NotSupportedException(member),
            });
            async Task<IStorageFile?> PickAsync()
            {
                await Visit(SaveStep.Picker);
                return file;
            }
            WindowStorage(window) = MemoryStorageProxy.For<IStorageProvider>(member => member switch
            {
                "get_CanSave" => true,
                nameof(IStorageProvider.SaveFilePickerAsync) => PickAsync(),
                _ => throw new NotSupportedException(member),
            });
            var saving = document.SaveCommand.ExecuteAsync(null);
            try
            {
                await PumpUntil(() => pause == SaveStep.Download ? stub.Calls.Any(c => c.Route == pdfRoute) : calls.Contains(pause));
                switch (change)
                {
                    case OwnerChange.Navigation:
                        shell.SelectedPage = shell.Pages.Single(page => page.Page == shell.Calendar);
                        await Pump();
                        Assert.Same(shell, main.Content);
                        break;
                    case OwnerChange.SignOut:
                        await shell.SignOutCommand.ExecuteAsync(null);
                        Assert.IsType<LoginViewModel>(main.Content);
                        break;
                    case OwnerChange.Idle:
                        time.Now += TimeSpan.FromMinutes(QuickSwitchTests.Device.IdleMinutes);
                        await main.TickAsync();
                        Assert.IsType<LoginViewModel>(main.Content);
                        break;
                    case OwnerChange.Server:
                        await main.EndConnectionContextAsync(api.ForgetSignIn);
                        Assert.Null(main.Content);
                        break;
                }
            }
            finally
            {
                gate.TrySetResult();
                await saving;
            }

            Assert.Null(document.Error);
            if (change == OwnerChange.Navigation)
            {
                Assert.Equal([SaveStep.Picker, SaveStep.Open, SaveStep.Write, SaveStep.Dispose], calls);
                Assert.Equal(pdf, stream.ToArray());
                Assert.NotNull(saver.LastSavedPath);
                Assert.Equal("Gespeichert: Test.pdf", reports.Toast.Text);
            }
            else
            {
                Assert.Equal(pause >= SaveStep.Picker, calls.Contains(SaveStep.Picker));
                Assert.Equal(pause >= SaveStep.Open, calls.Contains(SaveStep.Open));
                Assert.Equal(pause >= SaveStep.Write, calls.Contains(SaveStep.Write));
                Assert.Equal(pause >= SaveStep.Open, calls.Contains(SaveStep.Dispose));
                Assert.Null(saver.LastSavedPath);
                Assert.DoesNotContain("Gespeichert", reports.Toast.Text ?? "", StringComparison.Ordinal);
            }
        }
        finally
        {
            window.Close();
        }
        return true;
    }, TestContext.Current.CancellationToken);

    // Avalonia bietet am einzelnen Fenster nur einen Getter. Die Headless-Sitzung bleibt für andere Tests unverändert.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_storageProvider")]
    private static extern ref IStorageProvider? WindowStorage(TopLevel window);

    private sealed class RecordingSaveStream(Func<SaveStep, Task> visit) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await visit(SaveStep.Write);
            await base.WriteAsync(buffer, cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            await visit(SaveStep.Dispose);
            await base.DisposeAsync();
        }
    }

    // Die Avalonia-Referenzassembly erlaubt keine eigenen Implementierungen der Storage-Schnittstellen.
    public class MemoryStorageProxy : DispatchProxy
    {
        private Func<string, object?> _call = null!;

        internal static T For<T>(Func<string, object?> call) where T : class
        {
            var mock = Create<T, MemoryStorageProxy>();
            ((MemoryStorageProxy)(object)mock)._call = call;
            return mock;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _call(targetMethod!.Name);
    }
}
