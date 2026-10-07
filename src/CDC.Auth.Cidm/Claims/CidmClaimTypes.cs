namespace CDC.Auth.Cidm.Claims;

/// <summary>Claim type names issued by DEFRA CIDM, per its OpenID Connect onboarding guide.</summary>
public static class CidmClaimTypes
{
    /// <summary>Correlates this sign-in with CIDM's own logs.</summary>
    public const string CorrelationId = "correlationId";
    /// <summary>Identifies the CIDM session, independent of the app's own session/cookie.</summary>
    public const string SessionId = "sessionId";
    /// <summary>CIDM's own identifier for the signed-in contact.</summary>
    public const string ContactId = "contactId";
    /// <summary>Echoes back the serviceId the app requested on the /authorize call.</summary>
    public const string ServiceId = "serviceId";
    /// <summary>A stable identifier for the contact, unique across CIDM.</summary>
    public const string UniqueReference = "uniqueReference";
    /// <summary>Identity-proofing level of assurance reached during sign-in.</summary>
    public const string LevelOfAssurance = "loa";
    /// <summary>Authentication assurance level (how strongly the credential itself was verified).</summary>
    public const string AuthenticationAssuranceLevel = "aal";
    /// <summary>Number of organisations/relationships the contact is enrolled against.</summary>
    public const string EnrolmentCount = "enrolmentCount";
    /// <summary>Number of pending enrolment requests for the contact.</summary>
    public const string EnrolmentRequestCount = "enrolmentRequestCount";
    /// <summary>The relationship selected via the B2C picker for this session.</summary>
    public const string CurrentRelationshipId = "currentRelationshipId";

    /// <summary>Raw claim name for each "relationships" array entry, before parsing.</summary>
    public const string RawRelationships = "relationships";

    /// <summary>Raw claim name for each "roles" array entry, before parsing.</summary>
    public const string RawRoles = "roles";

    /// <summary>Claim type used to store each parsed <see cref="RelationshipInfo"/> (as JSON) on the principal.</summary>
    public const string Relationship = "cidm:relationship";

    /// <summary>Claim type used to store each parsed <see cref="RoleInfo"/> (as JSON) on the principal.</summary>
    public const string Role = "cidm:role";
}
