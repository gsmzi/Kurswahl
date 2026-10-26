using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage.Tests;

// Pflichtenheft §5a, Akzeptanzfälle 19/20. Ephemeral test keys are never persisted.
public sealed class SignedProfileReaderTests
{
    private sealed class Identity : ICurrentIdentityProvider
    {
        public CurrentIdentity GetCurrent() => new("DEMO-SCHOOL\\demo01", "synthetic-sid-001");
    }
    private static StudentProfile Profile => new(1, 3, "demo-001", "DEMO-SCHOOL\\demo01", 2029,
        DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "demo-key", PriorEducation.Unknown with
        { InformaticsImp = Knowledge.Yes, ContinuousBilingual = Knowledge.Yes, ItalianProfile = Knowledge.Yes }, "synthetic-sid-001");

    private static ProfileContext Check(StudentProfile profile, bool tamper = false, bool unknownKey = false, string algorithm = "ECDSA-P256-SHA256-P1363")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, JsonDocuments.Options);
        var signature = new ProfileSignature(1, profile.KeyId, algorithm,
            Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        if (tamper) bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"yes\"", "\"no\""));
        var trust = new ProfileTrust(1, unknownKey ? [] : [new(profile.KeyId, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()))], ["DEMO-SCHOOL"]);
        return new SignedProfileReader(trust, new Identity()).Verify(bytes, JsonSerializer.SerializeToUtf8Bytes(signature, JsonDocuments.Options), "demo-001", 2029);
    }

    [Fact] public void SignedBytesGrantOnlyVerifiedPrerequisites()
    {
        var result = Check(Profile);
        Assert.Equal(ProfileStatus.Verified, result.Status);
        Assert.Equal(3, result.Revision);
        Assert.Equal(Knowledge.Yes, result.Prerequisites.InformaticsImp);
        Assert.Equal(Knowledge.Unknown, result.Prerequisites.InformaticsBridge);
    }
    [Fact] public void AlteredBytesDiscardAllPrerequisites() => AssertInvalid(Check(Profile, tamper: true));
    [Fact] public void UnknownKeyIsRejected() => AssertInvalid(Check(Profile, unknownKey: true));
    [Fact] public void AlgorithmIsNotNegotiatedFromUntrustedInput() => AssertInvalid(Check(Profile, algorithm: "none"));
    [Theory] [InlineData("student")] [InlineData("account")] [InlineData("sid")] [InlineData("year")]
    [InlineData("schema")] [InlineData("revision")] [InlineData("future")] [InlineData("semesters")]
    public void SignedButInapplicableProfileIsRejected(string change)
    {
        var profile = change switch
        {
            "student" => Profile with { StudentId = "demo-002" },
            "account" => Profile with { WindowsUsername = "LOCAL\\demo01" },
            "sid" => Profile with { AccountId = "synthetic-sid-002" },
            "year" => Profile with { GraduationYear = 2030 },
            "schema" => Profile with { SchemaVersion = 99 },
            "revision" => Profile with { ProfileRevision = 0 },
            "future" => Profile with { IssuedAt = DateTimeOffset.UtcNow.AddDays(1) },
            _ => Profile with { Prerequisites = Profile.Prerequisites with { SportExemptSemesters = [5] } }
        };
        AssertInvalid(Check(profile));
    }
    [Fact] public void MalformedFilesReturnInvalidWithoutThrowing() => AssertInvalid(
        new SignedProfileReader(new(1, [], []), new Identity()).Verify([1, 2], [3, 4], "demo-001", 2029));
    [Fact] public void MissingFilesDoNotCreateAnything()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Equal(ProfileStatus.Missing, new SignedProfileReader(new(1, [], []), new Identity()).Read(path, "demo-001", 2029).Status);
        Assert.False(Directory.Exists(path));
    }
    private static void AssertInvalid(ProfileContext result)
    {
        Assert.Equal(ProfileStatus.ProfileInvalid, result.Status);
        Assert.Equal(PriorEducation.Unknown, result.Prerequisites);
    }
}
