using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Application;

public sealed record TeacherDashboard(int Students, int UnconfirmedMappings, int ProfilesToIssue,
    int ProfilesToDeliver, int Delivered, int Choices, int Reselections, int Reviews);
public static class TeacherDashboardBuilder
{
    public static TeacherDashboard Build(IAdministrationStore store, TeacherSession session)
    {
        var states = session.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear).ToArray();
        var logs = store.DistributionLog();
        var issue = 0; var deliver = 0; var delivered = 0;
        foreach (var state in states.Where(s => s.Record.MappingConfirmed))
        {
            var r = state.Record; var profile = store.LatestProfile(r.Student.ExternalStudentId)?.Profile;
            if (profile is null || profile.GraduationYear != r.Student.GraduationYear
                || JsonSerializer.Serialize(profile.Prerequisites, DocumentJson.Options) != JsonSerializer.Serialize(r.Student.Prerequisites, DocumentJson.Options))
            { issue++; continue; }
            var last = logs.LastOrDefault(l => l.StudentId == r.Student.ExternalStudentId && l.ProfileRevision == profile.ProfileRevision);
            var deliveredRecord = last is null ? null : store.StudentHistory(r.Student.ExternalStudentId).LastOrDefault(s => s.ChangedAt <= last.At);
            if (last is not null && last.Status == "verteilt" && last.At >= profile.IssuedAt
                && deliveredRecord is { MappingConfirmed: true }
                && string.Equals(deliveredRecord.HomeDirectory, r.HomeDirectory, StringComparison.OrdinalIgnoreCase)) delivered++;
            else deliver++;
        }
        return new(states.Length, states.Count(s => !s.Record.MappingConfirmed), issue, deliver, delivered,
            states.Count(s => s.Active is not null), states.Count(s => s.RequiresReselection), states.Count(s => s.Validation?.Status == ReviewStatus.NeedsReview));
    }
}
