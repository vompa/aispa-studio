using AispaStudio.Api.Assistant;
using AispaStudio.Help;

namespace AispaStudio.Api.Tests;

/// <summary>Die KI-Hilfe schlägt im Einzelplatz keine Themen vor, die nur im Server-Betrieb gelten (Kopfangabe <c>betrieb: server</c>, T-110).</summary>
public sealed class AssistantHelpStandaloneTests
{
    [Fact]
    public void Hilfe_Antwort_nennt_im_Einzelplatz_keine_Server_Themen_und_mit_Server_schon()
    {
        var withServer = RoleAssistant.HelpAnswer("Anfrage annehmen", "Anfrage annehmen", new HelpContext(), 0);
        var standalone = RoleAssistant.HelpAnswer("Anfrage annehmen", "Anfrage annehmen", new HelpContext(Standalone: true), 0);

        Assert.Contains(withServer.Links, l => l.Topic == "anfragen-annehmen");
        Assert.All(standalone.Links, l => Assert.False(HelpLibrary.IsServerOnly(l.Topic), l.Topic));
    }
}
