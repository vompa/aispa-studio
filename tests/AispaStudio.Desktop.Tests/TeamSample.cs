using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Stub-Routen des Bereichs „Team“ (R05 Slice 98): Personen, Ausgeschiedene, Zugänge, Einladungen, Verlauf und Speichern. Speichern gibt zurück, was ankam, und die
/// Listen enthalten danach Neues, wie die API.
/// </summary>
internal static class TeamSample
{
    public static readonly DateTimeOffset InvitedAt = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

    public const string Token = "geheim-123";

    public const string PublicUrl = "http://team.test";

    public const string RejectRole = "Inhaber- und Manager-Zugänge legt nur der Inhaber an, ändert und entfernt sie.";

    public static StubApi AddTeam(StubApi api, IReadOnlyList<EmployeeDto> employees, IReadOnlyList<EmployeeDto> former, IReadOnlyList<StaffMemberDto> members,
        IReadOnlyList<InvitationDto> invitations, bool standalone = false)
    {
        var team = employees.ToList();
        var accesses = members.ToList();
        var open = invitations.ToList();
        api.OnJson("GET /api/employees", _ => team.ToList())
            .On("GET /api/employees/former", former.ToList())
            .OnJson("GET /api/staff", _ => accesses.ToList())
            .OnJson("GET /api/staff/invitations", _ => open.ToList())
            .On("GET /api/system/modules", new SystemModules(standalone, false, false, false, false, false, false, true))
            .OnJson("POST /api/employees", body =>
            {
                var created = Employee(Guid.NewGuid(), body);
                team.Add(created);
                return created;
            })
            .OnJson("POST /api/staff/invitations", body =>
            {
                var input = JsonSerializer.Deserialize<InvitationInput>(body!, ApiClient.Json)!;
                var invitation = new InvitationDto(Guid.NewGuid(), input.Email, input.DisplayName, input.Role, input.AllLocations, input.LocationIds, input.EmployeeId,
                    InvitedAt, InvitedAt.AddDays(7), input.AccessExpiresAt, input.AmountReports, input.PlainReports);
                open.Add(invitation);
                return new InvitationCreated(invitation, Token, $"{PublicUrl}/?einladung={Token}");
            })
            .OnJson("POST /api/staff/accounts", body =>
            {
                var input = JsonSerializer.Deserialize<StaffAccountInput>(body!, ApiClient.Json)!;
                var member = new StaffMemberDto(Guid.NewGuid().ToString(), "", input.DisplayName, input.Role, input.AllLocations, input.LocationIds, input.EmployeeId,
                    true, 0, input.ExpiresAt, input.UserName, true, input.AmountReports, input.PlainReports);
                accesses.Add(member);
                return member;
            });
        foreach (var employee in employees.Concat(former))
        {
            api.OnJson($"PUT /api/employees/{employee.Id}", body =>
                {
                    var saved = Employee(employee.Id, body);
                    team[team.FindIndex(e => e.Id == employee.Id)] = saved;
                    return saved;
                })
                .OnJson($"DELETE /api/employees/{employee.Id}", _ =>
                {
                    team.RemoveAll(e => e.Id == employee.Id);
                    return new { };
                })
                .On($"GET /api/employees/{employee.Id}/roster-history", new List<HistoryEntry>());
        }

        foreach (var member in members)
        {
            api.OnJson($"PUT /api/staff/{member.UserId}", body =>
                {
                    var input = JsonSerializer.Deserialize<StaffAccessInput>(body!, ApiClient.Json)!;
                    var saved = member with
                    {
                        Role = input.Role,
                        AllLocations = input.AllLocations,
                        LocationIds = input.LocationIds,
                        EmployeeId = input.EmployeeId,
                        IsActive = input.IsActive,
                        ExpiresAt = input.ExpiresAt,
                        AmountReports = input.AmountReports,
                        PlainReports = input.PlainReports,
                    };
                    accesses[accesses.FindIndex(m => m.UserId == member.UserId)] = saved;
                    return saved;
                })
                .On($"POST /api/staff/{member.UserId}/password", new { })
                .On($"GET /api/staff/{member.UserId}/history", new List<HistoryEntry>());
        }

        foreach (var invitation in invitations)
        {
            api.OnJson($"DELETE /api/staff/invitations/{invitation.Id}", _ =>
            {
                open.RemoveAll(i => i.Id == invitation.Id);
                return new { };
            });
        }

        return api;
    }

    public static EmployeeDto Employee(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<EmployeeInput>(body!, ApiClient.Json)!;
        return new EmployeeDto(id, input.DisplayName, input.Color, input.QualificationIds, input.WorkingHours, input.Languages, input.Gender, input.PreferredName);
    }

    public static StaffMemberDto Access(string userId, EmployeeDto? employee, string name, StaffRole role, IReadOnlyList<Guid> locations) =>
        new(userId, $"{userId}@spa.test", employee?.DisplayName ?? name, role, false, locations, employee?.Id, true, 1);

    public static InvitationDto Invitation(string name, string email, StaffRole role, IReadOnlyList<Guid> locations, Guid? employee = null) =>
        new(Guid.NewGuid(), email, name, role, false, locations, employee, InvitedAt, InvitedAt.AddDays(7));
}
