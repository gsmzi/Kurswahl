using System.Text;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage.Tests;

// Profile validation without certificate or SID requirement.
public sealed class SignedProfileReaderTests
{
    private static StudentProfile Profile => new(1, 3, "demo-001", "", 2029,
        DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "local-profile", PriorEducation.Unknown with
        { InformaticsImp = Knowledge.Yes, ContinuousBilingual = Knowledge.Yes, ItalianProfile = Knowledge.Yes }, "");

    private static ProfileContext Check(StudentProfile profile)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, JsonDocuments.Options);
        return new SignedProfileReader().Verify(bytes, "demo-001", 2029);
    }

    [Fact] public void ProfileBytesGrantOnlyVerifiedPrerequisites()
    {
        var result = Check(Profile);
        Assert.Equal(ProfileStatus.Verified, result.Status);
        Assert.Equal(3, result.Revision);
        Assert.Equal(Knowledge.Yes, result.Prerequisites.InformaticsImp);
        Assert.Equal(Knowledge.Unknown, result.Prerequisites.InformaticsBridge);
    }
    [Fact] public void DuplicateJsonFieldsAreRejected()
    {
        var json = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1}");
        AssertInvalid(new SignedProfileReader().Verify(json, "demo-001", 2029));
    }
    [Theory] [InlineData("student")] [InlineData("year")]
    [InlineData("schema")] [InlineData("revision")] [InlineData("future")] [InlineData("semesters")]
    public void InapplicableProfileIsRejected(string change)
    {
        var profile = change switch
        {
            "student" => Profile with { StudentId = "demo-002" },
            "year" => Profile with { GraduationYear = 2030 },
            "schema" => Profile with { SchemaVersion = 99 },
            "revision" => Profile with { ProfileRevision = 0 },
            "future" => Profile with { IssuedAt = DateTimeOffset.UtcNow.AddDays(1) },
            _ => Profile with { Prerequisites = Profile.Prerequisites with { SportExemptSemesters = [5] } }
        };
        AssertInvalid(Check(profile));
    }
    [Fact] public void MalformedFilesReturnInvalidWithoutThrowing() => AssertInvalid(
        new SignedProfileReader().Verify([1, 2], "demo-001", 2029));
    [Fact] public void MissingFilesDoNotCreateAnything()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Equal(ProfileStatus.Missing, new SignedProfileReader().Read(path, "demo-001", 2029).Status);
        Assert.False(Directory.Exists(path));
    }
    private static void AssertInvalid(ProfileContext result)
    {
        Assert.Equal(ProfileStatus.ProfileInvalid, result.Status);
        Assert.Equal(PriorEducation.Unknown, result.Prerequisites);
    }
}
