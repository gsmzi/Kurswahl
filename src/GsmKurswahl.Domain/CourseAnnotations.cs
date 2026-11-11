namespace GsmKurswahl.Domain;

/// <summary>Shared wording for descriptive annotations, without any rule decisions.</summary>
public static class CourseAnnotations
{
    public static string Label(CourseOffer? offer) => offer is null ? "" : string.Join(" · ",
        new[] { offer.CooperationSchool switch
        {
            CooperationSchool.Ewg => "Koopkurs am EWG",
            CooperationSchool.Gsm => "Koopkurs am GSM",
            _ => ""
        }, offer.IsTopUpCourse ? "Aufsetzerkurs" : "" }.Where(s => s.Length > 0));

    public static string Suffix(CourseOffer? offer) => Label(offer) is { Length: > 0 } label ? " · " + label : "";
}
