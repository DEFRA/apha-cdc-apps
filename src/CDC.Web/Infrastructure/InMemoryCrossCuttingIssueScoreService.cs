using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// In-memory <see cref="ICrossCuttingIssueScoreService"/>, registered as a singleton so committed
/// edits survive between requests. Seeded from the legacy Phase2Release3 seed data (ids, names and
/// the flat starting score of 10). Placeholder until CDC.Api exposes prioritisation metadata.
/// </summary>
public sealed class InMemoryCrossCuttingIssueScoreService : ICrossCuttingIssueScoreService
{
    private const int SeedScore = 10;

    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, int> _scores = [];
    private readonly IReadOnlyList<CategorySeed> _seed = BuildSeed();

    public InMemoryCrossCuttingIssueScoreService()
    {
        foreach (var value in _seed.SelectMany(c => c.Criteria).SelectMany(c => c.Values))
        {
            _scores[value.Id] = SeedScore;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CrossCuttingIssueCategory>> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<CrossCuttingIssueCategory> categories =
            [
                .. _seed.OrderBy(category => category.SequenceNumber)
                    .Select(category => new CrossCuttingIssueCategory(
                        category.Id,
                        category.Name,
                        category.SequenceNumber,
                        [
                            .. category.Criteria.OrderBy(criterion => criterion.SequenceNumber)
                                .Select(criterion => new CrossCuttingIssueCriterion(
                                    criterion.Id,
                                    criterion.Name,
                                    criterion.SequenceNumber,
                                    [
                                        .. criterion.Values.OrderBy(value => value.SequenceNumber)
                                            .Select(value => new CrossCuttingIssueCriterionValue(
                                                value.Id, value.Label, _scores[value.Id], value.SequenceNumber))
                                    ]))
                        ]))
            ];

            return Task.FromResult(categories);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scores);

        lock (_sync)
        {
            foreach (var (id, score) in scores)
            {
                if (score is < 0 or > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(scores), score, "Score must be between 0 and 100.");
                }

                if (!_scores.ContainsKey(id))
                {
                    throw new KeyNotFoundException($"Criterion value {id} was not found.");
                }

                _scores[id] = score;
            }
        }

        // Legacy runs sppSpeciesPrioritisationScore here to rebuild SpeciesPrioritisationScore.
        return Task.CompletedTask;
    }

    private sealed record CategorySeed(Guid Id, string Name, int SequenceNumber, IReadOnlyList<CriterionSeed> Criteria);

    private sealed record CriterionSeed(Guid Id, string Name, int SequenceNumber, IReadOnlyList<ValueSeed> Values);

    private sealed record ValueSeed(Guid Id, string Label, int SequenceNumber);

    private static ValueSeed[] SeedValues(params (string Id, string Label)[] values) =>
        [.. values.Select((value, index) => new ValueSeed(Guid.Parse(value.Id), value.Label, index + 1))];

    private static IReadOnlyList<CategorySeed> BuildSeed() =>
    [
        new CategorySeed(Guid.Parse("5837CA89-9E06-4520-98A5-23AE0A8B28DE"), "Animal identification", 1,
        [
            new CriterionSeed(Guid.Parse("8BC2D0FF-1FC7-457B-9D62-A77F0FB8D27C"), "Accuracy of animal identification", 1,
                SeedValues(
                    ("43BFFEF6-0BE7-4C5E-908A-D81D0CFC8466", "Unique id & \"passport\""),
                    ("7FFD837E-7E01-43C6-9B3A-CB8E72D6699A", "\"Passport\" but can't always guarantee identity"),
                    ("2E9BAE66-AA58-4B6E-A2B1-D349C499410E", "Unique id, no \"passport\""),
                    ("C5C53FE4-09CA-4D88-AFCE-D3B9A6CEF562", "Some with unique id"),
                    ("334904BB-758C-4AEF-8E8C-66FEF8DA1763", "All identifiable to a holding"),
                    ("DA45A1E0-4411-4891-82DF-60BB75AB5EC0", "Some identifiable to a holding"),
                    ("B87D6FE8-6031-4B03-A763-13C80BCC117E", "Can rarely identify an area (wildlife)"),
                    ("69F57147-149C-4F7E-8841-AF61B6D3171A", "No id or association in an area")))
        ]),
        new CategorySeed(Guid.Parse("5B1A65EA-A19A-4239-8A32-DCC06E518598"), "Movements", 2,
        [
            new CriterionSeed(Guid.Parse("54D630FF-96CB-4528-8D1E-F7CCE1EB4A61"), "Ease of movement investigation", 1,
                SeedValues(
                    ("9332C010-117C-42F9-91C2-8CC6A837C34B", "Each individual & its movement recorded nationally"),
                    ("E97B64FD-AAB1-48B4-BBF5-99826B5D9EA9", "Each has id, group movements recorded nationally"),
                    ("CCF031DA-630B-46FE-89AD-6826627F0A17", "All movements of groups recorded nationally"),
                    ("AE63210A-8AA6-4C2D-8168-5620FB44A6F6", "Some movements of groups recorded nationally"),
                    ("6E1B40C5-8DE9-48D5-BB3A-4A1697780559", "Movements of groups recorded locally"),
                    ("CD9FF046-7535-4F1A-A750-CD2D05782A45", "Some may keep movement records voluntarily"),
                    ("1771AE1C-0033-4B9C-ACD4-18EC1D800DA2", "No movement recording anticipated")))
        ]),
        new CategorySeed(Guid.Parse("0985AAF4-3A61-46EB-A93C-6373D5374EBB"), "Animal locations and number", 3,
        [
            new CriterionSeed(Guid.Parse("F65F5017-0412-4963-9ADC-328069750224"), "Number of holdings", 1,
                SeedValues(
                    ("55F81B25-5229-4600-B614-9C8FE41B2669", "1-999"),
                    ("CD26F90F-0B59-4999-8B95-6929C08867C6", "1000-4999"),
                    ("4B6C25D3-7FB5-4EE9-A7D3-F39C8366AB59", "5000-9999"),
                    ("219197A1-8569-40E1-96FC-27E4C91C8A89", ">9999"))),
            new CriterionSeed(Guid.Parse("63560D8B-9DD4-4EC1-B6B2-743CADAF8552"), "Number of animals", 2,
                SeedValues(
                    ("4199EAA5-484D-4803-800C-CBE01C6733DF", "1-49,999"),
                    ("9015130A-ED26-4EFC-B2B4-F439F0EA9ED7", "50,000-999,999"),
                    ("5E5E043C-B1F7-46C2-A02F-D5EA1006856A", "1,000,000-9,999,999"),
                    ("3F9C1779-D361-4954-9F8D-0B7D8DE798D0", ">9,999,999"))),
            new CriterionSeed(Guid.Parse("4489FD25-C2BC-40AB-A738-066C866F44A3"), "Proportion keeping mixed species", 3,
                SeedValues(
                    ("63F569AA-05D3-4FA8-9F54-CF4831C06202", "0"),
                    ("6FC6D0CD-A27D-4379-9146-ED910B9F9F7E", "1-50%"),
                    ("0EADE30D-EEBB-4DF8-8224-0A0D3D341A28", "51-100%")))
        ]),
        new CategorySeed(Guid.Parse("E0C3A33E-CFAC-4033-83C0-A88AD53417B7"), "Bio-security", 4,
        [
            new CriterionSeed(Guid.Parse("10F04FB1-FEFA-49B6-B1C0-BA204C12B1B4"), "Livestock contacts", 1,
                SeedValues(
                    ("88AD7510-9D77-474A-B3C5-5FC32B98F380", "Regularly mix"),
                    ("1AA0FA83-85F5-40AD-9063-DB509C474FBC", "Occasionally mix"),
                    ("7CE33D07-215C-409E-97F8-0D887A102035", "Rarely mix, but replacements widely sourced"),
                    ("1863F784-13CA-4776-BBB2-18C6FB37A356", "Rarely mix, replacements specifically sourced"),
                    ("657DE796-84EA-462A-BBE0-5793B15BC3D0", "Majority are all in - all out"))),
            new CriterionSeed(Guid.Parse("5DD13E41-98CD-43D4-8657-21FFBA753F42"), "Imported livestock - isolation", 2,
                SeedValues(
                    ("4AEA7953-2AA2-4B49-8163-7F08DF659158", "Mixed on arrival"),
                    ("1126EE7B-DCC0-451E-B737-0A98D5228380", "Separate for a few days"),
                    ("55CE9EC8-6216-4E79-B530-AD85255F272F", "Separate throughout life (all in - all out)"))),
            new CriterionSeed(Guid.Parse("97ED641E-3E59-477E-8F5C-BC38F824053A"), "Imported livestock - testing/treatment regime", 3,
                SeedValues(
                    ("FE917497-6973-4A85-9864-F144B3CF604D", "No preventative action taken"),
                    ("8FBB4F15-0A78-49D1-9758-681300149F10", "Precautions taken for 1 - 2 specific diseases"),
                    ("41C85CC9-693F-40E1-8136-133C57FBA9C9", "Precautions taken for 3 + diseases"))),
            new CriterionSeed(Guid.Parse("06F0F824-125A-4E73-9384-ECC7F6D0A0E1"), "Farm personnel - animal contacts elsewhere", 4,
                SeedValues(
                    ("CA4D1FD9-28E5-46F2-A61E-8F7FB1BE528B", "Frequent association elsewhere"),
                    ("EC0FCFC0-9C37-4B85-B3D1-9952323202BD", "Rare association elsewhere"),
                    ("01489A5C-8832-495E-999E-55DD41FB7D50", "Association elsewhere banned"))),
            new CriterionSeed(Guid.Parse("C2D8DCEC-E81A-4DA8-A3CA-FAA2AB90B124"), "Farm personnel - observance of bio-security protocols", 5,
                SeedValues(
                    ("A3A9BB98-33A6-4941-B950-6D6E6891FE2A", "No bio-security precautions"),
                    ("036FC533-DFDE-46E8-8DB2-0D0BAA185B5D", "Some bio-security precautions"),
                    ("9E2CB5E6-9D08-4D8B-ABC1-2F060F678F9F", "Extensive bio-security precautions"))),
            new CriterionSeed(Guid.Parse("D3EAC9BB-E4D3-4E99-86BE-6C8665F4677F"), "Farm situation - modal type of group of this species", 6,
                SeedValues(
                    ("C3119628-A34D-4AA7-BC3C-34ACFBBD7277", "Wildlife - overlapping ranges"),
                    ("1254C1EC-F09E-4910-B8FE-8CE97630E63B", "Wildlife - discrete ranges"),
                    ("7B7CAA3D-76A8-4859-97C8-5BAA3B7FF561", "Lots of land parcels for holding"),
                    ("37E9AE5E-CD3A-4778-9676-3C1ABC2847DE", "5 or fewer land parcels are stocked at one time"),
                    ("EEB6110A-EC25-444B-A485-F454BBC80849", "Holding is a single entity"))),
            new CriterionSeed(Guid.Parse("E587B8E7-BC73-44F2-B8D6-14968B472AFC"), "Farm situation - degree of separation", 7,
                SeedValues(
                    ("334F777F-C85B-4777-B4DC-2DB192CA7453", "Frequent exchange, wide distribution"),
                    ("BF543DAA-1E3A-4373-938B-6BBEE238C0C2", "Occasional exchange, wide distribution"),
                    ("EB02462E-DA86-4E38-BBF6-87AB1640917B", "Frequent exchange limited distribution"),
                    ("6DE79C7E-90CE-4CBB-A2AF-A6AFD960E0C9", "No exchange"))),
            new CriterionSeed(Guid.Parse("BB30815F-25D1-4E5C-B463-F3C654D5E421"), "Farm situation - how many assurance schemes apply to livestock of this species", 8,
                SeedValues(
                    ("EE8C079D-99B1-4A6B-B627-92ADCE59BA82", "0"),
                    ("3F7C69F7-86B2-404B-8385-BF808AF5D32E", "1-3"),
                    ("B3AC4488-FD5A-4C01-B7CD-FBBCF0FC00FF", "4-6"),
                    ("052057D4-9CF3-46D2-921B-8AFAF367A9F2", ">6")))
        ])
    ];
}
