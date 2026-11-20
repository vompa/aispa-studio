using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 110 (Ä252): Reiter „Personalakte“ der Personenkarte mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly HrEntryDto HrSample = new(Guid.NewGuid(), HrEntryKind.Warning, new DateOnly(2026, 9, 1), "Dreimal zu spät zum Dienst.", null, null, [], null, false, "Katrin", true, new DateOnly(2028, 9, 1));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Personalakte_legt_ein_Gespraech_an_und_die_Reiter_passen_in_die_Karte(string theme)
    {
        // Lohn und Regelungen stehen im selben Bereich „Lohn und Akte“ (ADR 0098) und laden mit.
        var api = PayrollApi().On($"GET /api/employees/{RosterLena.Id}/regulations", new StaffRegulationsDto([], [], []));
        var saved = new List<HrEntryDto> { HrSample };
        var path = $"/api/employees/{RosterLena.Id}/hr-file";
        api.OnJson($"GET {path}", _ => saved.ToList());
        api.OnJson($"POST {path}", body =>
        {
            var input = JsonSerializer.Deserialize<HrEntryInput>(body!, ApiClient.Json)!;
            var entry = new HrEntryDto(Guid.NewGuid(), input.Kind, input.Date, input.Text, input.ConversationKind, input.Participants, input.Agreements ?? [], input.FollowUpOn, input.FollowUpDone,
                input.IssuedBy, input.HandedOverInWriting, input.ExpiresOn);
            saved.Insert(0, entry);
            return entry;
        });
        return Render(theme, "verwaltung-team-person-personalakte", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Personalakte");
            await Pump(Settling);
            var tabs = Named<Control>(window, "CardTabs");
            Assert.True(tabs.Bounds.Width <= 760, $"Reiterleiste {tabs.Bounds.Width} breit");

            await ClickButton(window, VisibleNamed<Button>(window, "HrAddConversationButton"));
            await Pump(Settling);
            Assert.Equal(VisibleNamed<TextBox>(window, "HrTextField"), TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());

            window.KeyTextInput("Ziele besprochen");
            await Pump();
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            await Pump(Settling);

            Assert.Equal("Ziele besprochen", saved[0].Text);
            Assert.False(team.Card!.HasUnsavedChanges);

            // 90a: „Gespeichert“ steht als Toast (meldungen.md M1, ohne Punkt), nicht als Erfolgs-InfoBar (M2).
            Assert.Equal("Gespeichert", team.Card.HrFile!.Toast.Text);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<AispaStudio.Desktop.Controls.InfoBar>(), b => b.IsEffectivelyVisible && b.Severity == AispaStudio.Desktop.Controls.InfoBarSeverity.Success);
            return team;
        }, api);
    }
}
