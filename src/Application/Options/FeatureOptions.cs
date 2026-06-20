namespace Application.Options;

public class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// When true (production default), newly created users must verify their email
    /// via OTP before they can log in.
    /// Set to false in Development to skip verification and unblock local testing.
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;
}
