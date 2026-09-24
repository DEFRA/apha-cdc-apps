using System.Data;
using System.Data.Common;
using System.Globalization;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileRepository"/>, reading the four result sets
/// returned by the legacy <c>spgaProfile</c> stored procedure.
/// </summary>
/// <remarks>
/// <para>
/// <c>spgaProfile</c> is user-scoped (role-based statuses, "my profiles" style filtering), but
/// this API has no authenticated user yet, so it is called with <see cref="Guid.Empty"/> for
/// <c>@UserId</c> and with searching disabled (<c>@PerformSqlSearch = 0</c>); text/status
/// filtering is applied afterwards in <c>ProfileSearchService</c>, exactly as it already was.
/// </para>
/// <para>
/// Result set 2 (scenario titles/user roles/user-specific status) and result set 4 (affected
/// species) are read positionally through a data reader, like <c>SpeciesRepository</c>, because
/// they contain columns that are meaningless without a real user or that duplicate column names.
/// Result set 2 is skipped entirely: everything it carries (which profile/scenario a version
/// belongs to) is already present in result set 3. Result set 4 is not read in this first pass -
/// <see cref="ProfileSearchResultDto.AffectedSpecies"/> is always empty until that is added.
/// </para>
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileRepository> logger)
    : IProfileRepository
{
    private const string PublishedStatus = "Published";
    private const string DraftStatus = "Draft";
    private const string ScenarioStatus = "Scenario";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSearchResultDto>> GetAllProfilesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileStoredProcedures.GetAllProfiles,
                    new
                    {
                        UserId = Guid.Empty,
                        ReturnQuestionAnswers = false,
                        ReturnNotes = false,
                        Words = string.Empty,
                        PerformSqlSearch = false,
                        SectionSelection = string.Empty
                    },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: every root profile (Id, Title), in title order.
            var titles = new Dictionary<Guid, string>();
            var orderedProfileIds = new List<Guid>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);

                titles[id] = ReadString(reader, 1);
                orderedProfileIds.Add(id);
            }

            // Result set 2 (scenario titles/user roles/user status) carries nothing this call
            // needs - skip straight past it to result set 3.
            await reader.NextResultAsync(cancellationToken);

            var profiles = await reader.NextResultAsync(cancellationToken)
                ? await ReadVersionsAsync(reader, titles, orderedProfileIds, cancellationToken)
                : [];

            logger.RetrievedAllProfiles(profiles.Count);

            return profiles;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileStoredProcedures.GetAllProfiles);
            throw;
        }
    }

    /// <summary>Reads result set 3 (every profile version) and assembles the per-profile DTOs.</summary>
    private static async Task<List<ProfileSearchResultDto>> ReadVersionsAsync(
        DbDataReader reader,
        Dictionary<Guid, string> titles,
        List<Guid> orderedProfileIds,
        CancellationToken cancellationToken)
    {
        var accumulator = new ProfileVersionAccumulator();

        while (await reader.ReadAsync(cancellationToken))
        {
            ProcessVersionRow(reader, titles, accumulator);
        }

        return BuildProfileResults(titles, orderedProfileIds, accumulator);
    }

    /// <summary>Reads one result-set-3 row and files its version under the right profile/bucket.</summary>
    private static void ProcessVersionRow(
        DbDataReader reader,
        Dictionary<Guid, string> titles,
        ProfileVersionAccumulator accumulator)
    {
        var row = ReadVersionRow(reader);

        // A version whose root profile is not in result set 1 cannot be displayed.
        if (!titles.TryGetValue(row.RootProfileId, out var title))
        {
            return;
        }

        var historyItem = new ProfileHistoryItemDto
        {
            VersionId = row.VersionId,
            VersionNumber = row.VersionNumber,
            Title = title,
            CreatedAtUtc = row.EffectiveDate,
            IsScenario = row.ScenarioId != row.RootProfileId
        };

        GetBucket(accumulator, row.RootProfileId, historyItem.IsScenario, row.StateName).Add(historyItem);

        UpdateEarliest(accumulator, row.RootProfileId, row.EffectiveDate);
        UpdateLatest(accumulator, row.RootProfileId, row.LastUpdated);
        UpdateIsPublic(accumulator, row.RootProfileId, row.IsPublic);
    }

    private static VersionRow ReadVersionRow(DbDataReader reader)
    {
        var effectiveDate = ReadNullableDateTime(reader, 6) ?? DateTime.UtcNow;

        return new VersionRow(
            VersionId: reader.GetGuid(0),
            ScenarioId: reader.GetGuid(1),
            RootProfileId: reader.GetGuid(2),
            VersionNumber: ReadInt32(reader, 3),
            StateName: ReadNullableString(reader, 5),
            EffectiveDate: effectiveDate,
            IsPublic: ReadBoolean(reader, 8),
            LastUpdated: ReadNullableDateTime(reader, 9) ?? effectiveDate);
    }

    /// <summary>Picks which per-profile bucket (scenario/published/draft) a version belongs in.</summary>
    private static List<ProfileHistoryItemDto> GetBucket(
        ProfileVersionAccumulator accumulator,
        Guid profileId,
        bool isScenario,
        string? stateName)
    {
        if (isScenario)
        {
            return GetOrAddBucket(accumulator.Scenarios, profileId);
        }

        if (string.Equals(stateName, PublishedStatus, StringComparison.Ordinal))
        {
            return GetOrAddBucket(accumulator.Published, profileId);
        }

        return GetOrAddBucket(accumulator.Draft, profileId);
    }

    private static void UpdateEarliest(ProfileVersionAccumulator accumulator, Guid profileId, DateTime effectiveDate)
    {
        if (!accumulator.Earliest.TryGetValue(profileId, out var earliest) || effectiveDate < earliest)
        {
            accumulator.Earliest[profileId] = effectiveDate;
        }
    }

    private static void UpdateLatest(ProfileVersionAccumulator accumulator, Guid profileId, DateTime lastUpdated)
    {
        if (!accumulator.Latest.TryGetValue(profileId, out var latest) || lastUpdated > latest)
        {
            accumulator.Latest[profileId] = lastUpdated;
        }
    }

    private static void UpdateIsPublic(ProfileVersionAccumulator accumulator, Guid profileId, bool isPublic)
    {
        accumulator.IsPublic[profileId] = accumulator.IsPublic.GetValueOrDefault(profileId) || isPublic;
    }

    private static List<ProfileSearchResultDto> BuildProfileResults(
        Dictionary<Guid, string> titles,
        List<Guid> orderedProfileIds,
        ProfileVersionAccumulator accumulator)
    {
        return [.. orderedProfileIds.Select(profileId => BuildProfileResult(profileId, titles, accumulator))];
    }

    private static ProfileSearchResultDto BuildProfileResult(
        Guid profileId,
        Dictionary<Guid, string> titles,
        ProfileVersionAccumulator accumulator)
    {
        var published = accumulator.Published.GetValueOrDefault(profileId, []);
        var draft = accumulator.Draft.GetValueOrDefault(profileId, []);
        var scenarios = accumulator.Scenarios.GetValueOrDefault(profileId, []);
        var createdAtUtc = accumulator.Earliest.GetValueOrDefault(profileId, DateTime.UtcNow);

        return new ProfileSearchResultDto
        {
            Id = profileId,
            Title = titles[profileId],
            Status = DetermineOverallStatus(published.Count, draft.Count, scenarios.Count),
            CreatedAtUtc = createdAtUtc,
            ModifiedAtUtc = accumulator.Latest.GetValueOrDefault(profileId, createdAtUtc),
            IsPublic = accumulator.IsPublic.GetValueOrDefault(profileId),
            AffectedSpecies = [],
            PublishedVersions = published,
            DraftVersions = draft,
            Scenarios = scenarios
        };
    }

    /// <summary>The profile-level status is the "best" version state it has: Published, else
    /// Draft, else Scenario, else Draft as a safe default for a profile with no versions.</summary>
    private static string DetermineOverallStatus(int publishedCount, int draftCount, int scenarioCount)
    {
        if (publishedCount > 0)
        {
            return PublishedStatus;
        }

        if (draftCount > 0)
        {
            return DraftStatus;
        }

        if (scenarioCount > 0)
        {
            return ScenarioStatus;
        }

        return DraftStatus;
    }

    private static List<ProfileHistoryItemDto> GetOrAddBucket(Dictionary<Guid, List<ProfileHistoryItemDto>> buckets, Guid profileId)
    {
        if (!buckets.TryGetValue(profileId, out var bucket))
        {
            bucket = [];
            buckets[profileId] = bucket;
        }

        return bucket;
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ProfileRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }

    private static string ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static string? ReadNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static bool ReadBoolean(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    private static int ReadInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static DateTime? ReadNullableDateTime(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    /// <summary>One row read from result set 3 (every profile version).</summary>
    private readonly record struct VersionRow(
        Guid VersionId,
        Guid ScenarioId,
        Guid RootProfileId,
        int VersionNumber,
        string? StateName,
        DateTime EffectiveDate,
        bool IsPublic,
        DateTime LastUpdated);

    /// <summary>Per-profile accumulators built up while result set 3 is read row by row.</summary>
    private sealed class ProfileVersionAccumulator
    {
        public Dictionary<Guid, List<ProfileHistoryItemDto>> Published { get; } = [];

        public Dictionary<Guid, List<ProfileHistoryItemDto>> Draft { get; } = [];

        public Dictionary<Guid, List<ProfileHistoryItemDto>> Scenarios { get; } = [];

        public Dictionary<Guid, DateTime> Earliest { get; } = [];

        public Dictionary<Guid, DateTime> Latest { get; } = [];

        public Dictionary<Guid, bool> IsPublic { get; } = [];
    }
}
