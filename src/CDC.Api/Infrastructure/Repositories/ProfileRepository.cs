using System.Data;
using System.Data.Common;
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
        var publishedByProfile = new Dictionary<Guid, List<ProfileHistoryItemDto>>();
        var draftByProfile = new Dictionary<Guid, List<ProfileHistoryItemDto>>();
        var scenariosByProfile = new Dictionary<Guid, List<ProfileHistoryItemDto>>();
        var earliestByProfile = new Dictionary<Guid, DateTime>();
        var latestByProfile = new Dictionary<Guid, DateTime>();
        var isPublicByProfile = new Dictionary<Guid, bool>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var scenarioId = reader.GetGuid(1);
            var rootProfileId = reader.GetGuid(2);

            // A version whose root profile is not in result set 1 cannot be displayed.
            if (!titles.TryGetValue(rootProfileId, out var title))
            {
                continue;
            }

            var effectiveDate = ReadNullableDateTime(reader, 6) ?? DateTime.UtcNow;
            var isPublic = ReadBoolean(reader, 8);
            var lastUpdated = ReadNullableDateTime(reader, 9) ?? effectiveDate;

            var historyItem = new ProfileHistoryItemDto
            {
                VersionId = reader.GetGuid(0),
                VersionNumber = ReadInt32(reader, 3),
                Title = title,
                CreatedAtUtc = effectiveDate,
                IsScenario = scenarioId != rootProfileId
            };

            var bucket = historyItem.IsScenario
                ? GetOrAddBucket(scenariosByProfile, rootProfileId)
                : GetOrAddBucket(ReadNullableString(reader, 5) == "Published" ? publishedByProfile : draftByProfile, rootProfileId);

            bucket.Add(historyItem);

            earliestByProfile[rootProfileId] = earliestByProfile.TryGetValue(rootProfileId, out var earliest)
                ? (effectiveDate < earliest ? effectiveDate : earliest)
                : effectiveDate;

            latestByProfile[rootProfileId] = latestByProfile.TryGetValue(rootProfileId, out var latest)
                ? (lastUpdated > latest ? lastUpdated : latest)
                : lastUpdated;

            isPublicByProfile[rootProfileId] = isPublicByProfile.GetValueOrDefault(rootProfileId) || isPublic;
        }

        return
        [
            .. orderedProfileIds.Select(profileId =>
            {
                var published = publishedByProfile.GetValueOrDefault(profileId, []);
                var draft = draftByProfile.GetValueOrDefault(profileId, []);
                var scenarios = scenariosByProfile.GetValueOrDefault(profileId, []);
                var createdAtUtc = earliestByProfile.GetValueOrDefault(profileId, DateTime.UtcNow);

                return new ProfileSearchResultDto
                {
                    Id = profileId,
                    Title = titles[profileId],
                    Status = published.Count > 0 ? "Published" : draft.Count > 0 ? "Draft" : scenarios.Count > 0 ? "Scenario" : "Draft",
                    CreatedAtUtc = createdAtUtc,
                    ModifiedAtUtc = latestByProfile.GetValueOrDefault(profileId, createdAtUtc),
                    IsPublic = isPublicByProfile.GetValueOrDefault(profileId),
                    AffectedSpecies = [],
                    PublishedVersions = published,
                    DraftVersions = draft,
                    Scenarios = scenarios
                };
            })
        ];
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
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static DateTime? ReadNullableDateTime(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
