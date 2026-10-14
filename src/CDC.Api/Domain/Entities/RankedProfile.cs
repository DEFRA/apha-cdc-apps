namespace CDC.Api.Domain.Entities;

/// <summary>
/// One profile/species pair with its prioritisation rank for each reason for intervention (RFI),
/// as read by <c>spgaRankProfileData</c> / <c>spgaFilterRankProfileData</c>. A rank of zero means
/// the profile has no impact on that RFI.
/// </summary>
public sealed record RankedProfile
{
    /// <summary>Gets the profile identifier.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Gets the species identifier.</summary>
    public required Guid SpeciesId { get; init; }

    /// <summary>Gets the profile title, which may contain HTML markup.</summary>
    public required string ProfileTitle { get; init; }

    /// <summary>Gets the species name, which may contain HTML markup.</summary>
    public required string SpeciesName { get; init; }

    /// <summary>Gets a value indicating whether the disease is exotic rather than endemic.</summary>
    public required bool IsExotic { get; init; }

    /// <summary>Gets a value indicating whether the profile is still awaiting validation.</summary>
    public required bool IsAwaitingValidation { get; init; }

    /// <summary>Gets the public health rank.</summary>
    public int PublicHealthRank { get; init; }

    /// <summary>Gets the animal welfare rank.</summary>
    public int AnimalWelfareRank { get; init; }

    /// <summary>Gets the wider society rank.</summary>
    public int WiderSocietyRank { get; init; }

    /// <summary>Gets the international trade rank.</summary>
    public int InternationalTradeRank { get; init; }

    /// <summary>Gets the post mitigation risk rank.</summary>
    public int RiskRank { get; init; }
}
