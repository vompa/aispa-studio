namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä284 (Slice 122): Die Windows-App spricht nur die API an. Kein Verweis auf EF Core oder Npgsql, und kein ViewModel wartet
/// blockierend auf einen Task. Das Vorgehen je Maske steht in <c>docs/architektur.md</c>, Abschnitt „Client gegen API“.
/// </summary>
public sealed class ClientArchitectureTests
{
    private const string Fake = "src/AispaStudio.Desktop/ViewModels/FakeViewModel.cs";

    [Fact]
    public void Eine_Projektdatei_mit_Npgsql_Verweis_ist_ein_Verstoss()
    {
        var found = ArchitectureScan.ForbiddenReferences("""<Project><ItemGroup><PackageReference Include="Npgsql" /></ItemGroup></Project>""");

        Assert.Equal(["Npgsql"], found);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData(@"..\AispaStudio.Api\AispaStudio.Api.csproj")]
    public void Eine_Projektdatei_mit_EF_Core_oder_Api_Verweis_ist_ein_Verstoss(string include)
    {
        var found = ArchitectureScan.ForbiddenReferences($"""<Project><ItemGroup><ProjectReference Include="{include}" /></ItemGroup></Project>""");

        Assert.Equal([include], found);
    }

    [Fact]
    public void Eine_Projektdatei_nur_mit_Client_und_Contracts_ist_kein_Verstoss()
    {
        var found = ArchitectureScan.ForbiddenReferences(
            """
            <Project><ItemGroup>
              <PackageReference Include="Avalonia" />
              <ProjectReference Include="..\AispaStudio.Client\AispaStudio.Client.csproj" />
              <ProjectReference Include="..\AispaStudio.Contracts\AispaStudio.Contracts.csproj" />
            </ItemGroup></Project>
            """);

        Assert.Empty(found);
    }

    [Fact]
    public void Die_Windows_App_und_ihre_Projekte_verweisen_weder_auf_EF_Core_noch_auf_Npgsql()
    {
        var projects = ArchitectureScan.DesktopProjectFiles();

        Assert.Contains(projects, p => p.EndsWith("AispaStudio.Desktop.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, p => p.EndsWith("AispaStudio.Client.csproj", StringComparison.Ordinal));
        Assert.Empty(projects.SelectMany(p => ArchitectureScan.ForbiddenReferences(File.ReadAllText(p)).Select(f => $"{p}: {f}")));
    }

    [Fact]
    public void Kein_Quelltext_der_Windows_App_nutzt_EF_Core_oder_Npgsql()
    {
        var files = ArchitectureScan.DesktopSourceFiles().ToList();

        Assert.NotEmpty(files);
        Assert.Empty(files.SelectMany(f => ArchitectureScan.ForbiddenUsings(f, File.ReadAllText(f))));
    }

    [Fact]
    public void Ein_Quelltext_mit_using_Npgsql_ist_ein_Verstoss_und_nennt_die_Datei()
    {
        var found = ArchitectureScan.ForbiddenUsings("Services/Fake.cs", "using System;\nusing Npgsql;\n");

        Assert.Equal(["Services/Fake.cs:2: using Npgsql;"], found);
    }

    [Fact]
    public void Ein_ViewModel_mit_Wait_ist_ein_Verstoss_und_nennt_die_Datei()
    {
        var found = ArchitectureScan.BlockingCalls(Fake, "class A\n{\n    void Run(Task task)\n    {\n        task.Wait();\n    }\n}\n");

        var message = Assert.Single(found);
        Assert.StartsWith($"{Fake}:5:", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_ViewModel_mit_GetResult_ist_ein_Verstoss()
    {
        var found = ArchitectureScan.BlockingCalls(Fake, "var x = api.GetAsync()\n    .GetAwaiter()\n    .GetResult();\n");

        Assert.Single(found);
    }

    [Theory]
    [InlineData("var x = api.GetAsync<int>(\"a\").Result;")]
    [InlineData("var task = api.GetAsync<int>(\"a\");\nvar x = task.Result;")]
    public void Ein_ViewModel_mit_Result_auf_einem_offenen_Task_ist_ein_Verstoss(string source)
    {
        var found = ArchitectureScan.BlockingCalls(Fake, source);

        Assert.Single(found);
    }

    [Fact]
    public void Result_nach_Task_WhenAll_ist_kein_Verstoss()
    {
        const string Source = "await Task.WhenAll(a, b).ConfigureAwait(true);\nvar v = a.Result.Value;\nvar w = b.Result.Error;\nvar u = c.Result;\n";

        var found = ArchitectureScan.BlockingCalls(Fake, Source);

        var message = Assert.Single(found);
        Assert.StartsWith($"{Fake}:4:", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("var v = a.Result.Value;\nawait Task.WhenAll(a, b);\n")]
    [InlineData("var all = Task.WhenAll(a, b);\nvar v = a.Result.Value;\n")]
    [InlineData("await Task.WhenAll(a, b);\nvar v = LoadTask.Result;\n")]
    [InlineData("var v = task?.Result;\n")]
    [InlineData("var v = tasks[0].Result;\n")]
    public void Result_vor_dem_Abwarten_ohne_await_oder_auf_einer_Task_Eigenschaft_ist_ein_Verstoss(string source)
    {
        Assert.Single(ArchitectureScan.BlockingCalls(Fake, source));
    }

    [Fact]
    public void Result_nach_mehrzeiligem_Task_WhenAll_ist_kein_Verstoss()
    {
        Assert.Empty(ArchitectureScan.BlockingCalls(Fake, "await Task.WhenAll(\n    first,\n    second).ConfigureAwait(true);\nvar v = second.Result.Value;\n"));
    }

    [Fact]
    public void Result_als_Typname_oder_Eigenschaft_eines_Ergebnisses_ist_kein_Verstoss()
    {
        Assert.Empty(ArchitectureScan.BlockingCalls(Fake, "public static DayLayout.Result Build() => null!;"));
        Assert.Empty(ArchitectureScan.BlockingCalls("src/AispaStudio.Desktop/ViewModels/SubscriptionsViewModel.cs", "var failed = imported.Result == Failed;"));
        Assert.Single(ArchitectureScan.BlockingCalls(Fake, "var failed = imported.Result == Failed;"));
    }

    [Fact]
    public void Kein_ViewModel_wartet_blockierend_auf_einen_Task()
    {
        var files = ArchitectureScan.ViewModelFiles().ToList();

        Assert.NotEmpty(files);
        Assert.Empty(files.SelectMany(f => ArchitectureScan.BlockingCalls(f, File.ReadAllText(f))));
    }
}
