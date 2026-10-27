using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

public enum YearSetupStep { Offer, Import, Mapping, Review, Complete }
public sealed record TeacherYear(OfferCatalog Catalog, string DatabasePath, YearSetupStep Step, string Actor,
    string CertificateThumbprint, string KeyId);
public sealed record TeacherWorkspaceDocument(int SchemaVersion, int? ActiveYear, ImmutableArray<TeacherYear> Years);
