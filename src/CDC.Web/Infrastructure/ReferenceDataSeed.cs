using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// The maintainable reference tables and their values, taken verbatim from the legacy Profiles
/// database baseline script (ProfilesDatabase/Create Scripts/PopulateBaselineData.sql) - every
/// [ReferenceTable] row with [IsMaintainable] = 1 and its [ReferenceValue] rows, with their
/// original identifiers and sequence numbers preserved.
/// </summary>
internal static class ReferenceDataSeed
{
    private static readonly Guid SpreadMechanism = new("92104271-8505-4189-A43D-23F88E25DBEE");
    private static readonly Guid TransmissionMechanism = new("BFC9F1C2-77EA-4704-B586-301BDD03B7B7");
    private static readonly Guid SurveillanceMechanism = new("D41B31E1-D0C0-464D-B640-3D26DD21546E");
    private static readonly Guid GeographicArea = new("948B3612-BEE6-4A07-892A-7861E5FAD085");
    private static readonly Guid DiagnosisAndDetectionMechanism = new("19A8013C-E7D6-4AED-A58F-AA1F775CBA54");
    private static readonly Guid ControlMechanism = new("D9889426-96A2-4C69-A3DB-C26F256C5FC1");

    /// <summary>Gets the maintainable reference tables, in the order they are offered to the user.</summary>
    public static IReadOnlyList<ReferenceTableSummary> Tables { get; } =
    [
        new(ControlMechanism, "Control Mechanism"),
        new(DiagnosisAndDetectionMechanism, "Diagnosis And Detection Mechanism"),
        new(GeographicArea, "Geographic Area"),
        new(SpreadMechanism, "Spread Mechanism"),
        new(SurveillanceMechanism, "Surveillance Mechanism"),
        new(TransmissionMechanism, "Transmission Mechanism")
    ];

    /// <summary>Gets the baseline reference values. All are active - no end date was ever set.</summary>
    public static IReadOnlyList<ReferenceDataValue> Values { get; } =
    [
        Value("A6219B68-6216-4F2D-8384-55EE4FF73024", SpreadMechanism, "Aerosol", 1),
        Value("CC1FD6D5-D5AF-4B44-BDC7-23AC61238D77", SpreadMechanism, "Sexual transmission", 2),
        Value("DE70CCCA-4D28-4B7F-9CB4-BBC9993D9E7E", SpreadMechanism, "Ingestion", 3),
        Value("D7A8BCF9-600C-4277-BA5C-74C81E4BAA09", SpreadMechanism, "Fomite", 4),
        Value("59543A13-BC62-4ACC-92F6-982B2D66DD84", SpreadMechanism, "Vector", 5),
        Value("B344E953-E842-47DD-ACC1-D8A1023544D1", SpreadMechanism, "Long distance aerosol", 6),
        Value("A22F1693-39DE-49AD-A9DD-F67BA7594CD2", SpreadMechanism, "Other", 7),

        Value("B2068D4E-6712-4D73-851A-8F906F0D8CC6", TransmissionMechanism, "Aerosol", 1),
        Value("469B42D5-8BC7-4701-A98E-0AE0994853DD", TransmissionMechanism, "Direct animal contact", 2),
        Value("ACEA173A-6997-452B-AEE7-124E6524BB13", TransmissionMechanism, "Waterborne", 3),
        Value("C93353DC-32B3-4E3E-AFEB-111FC1ACF201", TransmissionMechanism, "Sexual", 4),
        Value("E5E0E3AF-B122-42C3-8B82-82101ABC6FB9", TransmissionMechanism, "Foodborne", 5),
        Value("7159297A-344E-42D1-A603-5946927B8FF2", TransmissionMechanism, "Vector", 6),
        Value("79DF15AC-5290-42D3-AE64-810D706D94A1", TransmissionMechanism, "Other environmental", 7),

        Value("691D354F-9BDB-4870-A785-D8B350293833", SurveillanceMechanism, "Notification", 1),
        Value("C878CD62-7CD7-4F6F-AC59-0900D8F58141", SurveillanceMechanism, "Outbreak investigation", 2),
        Value("8F078954-1ADE-44ED-AEAE-A36E5EC4E7D8", SurveillanceMechanism, "Sentinel surveillance", 3),
        Value("0AE4BBC5-726B-4CAC-A645-ABD08A149219", SurveillanceMechanism, "Continuous survey", 4),
        Value("47870AF7-483D-4728-8009-52A84FD4CBF4", SurveillanceMechanism, "Time limited survey", 5),
        Value("0DEB0107-FF61-426F-9A24-8051D41187C1", SurveillanceMechanism, "Census", 6),
        Value("13692C1E-83A4-4194-94AD-F313959F655C", SurveillanceMechanism, "Scanning surveillance", 7),

        Value("E865A287-B948-46F9-A3E3-A0E331BE3EDA", GeographicArea, "Western Europe", 1),
        Value("1DBA2008-F52D-4FB6-B2CC-D079BD722122", GeographicArea, "Eastern Europe", 2),
        Value("8CA8E861-5BE4-42C4-B5B2-3ED85CD8CDD9", GeographicArea, "Africa", 3),
        Value("76E5922D-32D9-4CEA-93B5-35DB7A46A82C", GeographicArea, "Middle East", 4),
        Value("4FB0E20B-3002-4991-B098-8BD0E8477803", GeographicArea, "North America", 5),
        Value("8041CDBB-57E3-47BC-8390-274ECD82D340", GeographicArea, "Central / South America", 6),
        Value("C1A74D91-9BAA-4BCE-83FD-4F07B74BFEEF", GeographicArea, "South East Asia", 7),
        Value("0963FF1D-D8CF-4FAE-BC7F-4B163D75A166", GeographicArea, "Indian subcontinent", 8),
        Value("D3BB4D8C-D62D-4ADB-B225-664DBE2CFE3C", GeographicArea, "Central / Far East Asia", 9),
        Value("0F30B14E-A9CC-4E26-8E63-685DA2964154", GeographicArea, "Australasia", 10),

        Value("72EFD34D-88ED-46B0-B054-E299AE730E19", DiagnosisAndDetectionMechanism, "Clinical signs", 1),
        Value("13495FCE-845A-46D4-82D5-CD311F3CDFAD", DiagnosisAndDetectionMechanism, "Behavioural changes", 2),
        Value("9AC9E6FA-C62F-4528-988A-C7B568C1367F", DiagnosisAndDetectionMechanism, "Post-mortem examination", 3),
        Value("6F4973E5-2F89-4719-82FA-C0EE5327E1D7", DiagnosisAndDetectionMechanism, "Laboratory testing", 4),
        Value("4609E099-128D-4A8F-A403-511788C185F9", DiagnosisAndDetectionMechanism, "Livestock production data", 5),

        Value("533262C1-454C-45E3-876B-2B47841AB5C2", ControlMechanism, "Import controls", 1),
        Value("39D5AE2D-29C9-4958-8A83-BA9646B6B136", ControlMechanism, "Movement restrictions (enforced)", 2),
        Value("F3E04DBB-AEFE-4157-8F69-8BEFD5A8BE97", ControlMechanism, "Movement restrictions (voluntary)", 3),
        Value("1D4E585E-BCB8-486C-8FAD-9DD1A8CB9D03", ControlMechanism, "Protection of foodstuffs", 4),
        Value("331EE4DB-25BB-4986-8E4E-5FCA090896CD", ControlMechanism, "Farm bio-security", 5),
        Value("3AA0FB56-ED65-4A8B-A732-6403F3EFCD73", ControlMechanism, "Vaccination", 6),
        Value("F3B22390-A586-4889-98AC-6162905101B8", ControlMechanism, "Treatment of infected animals", 7),
        Value("13957250-081B-4685-A142-63D0560B9A7B", ControlMechanism, "Culling", 8),
        Value("096B882F-C896-40D1-AF42-AC2BD42BA07C", ControlMechanism, "Vector control", 9),
        Value("84F36AE4-49CB-4F7C-8871-474D5B08D468", ControlMechanism, "Raise awareness for rapid detection", 10),
        Value("8C9327EA-553A-4EC9-8606-3179DDAFF596", ControlMechanism, "Improve stockmanship", 11),
        Value("37A64447-44CC-4704-B029-3C8D8441313C", ControlMechanism, "Routine testing to identify cases", 12),
        Value("C56A67BD-5B59-4D26-97F5-83E2F2A363A3", ControlMechanism, "Genetic methods", 13),
        Value("338E32D5-202C-46D6-B8A2-20E5D56D04D7", ControlMechanism, "Other unlisted control types", 14),
        Value("3B2E8D44-81BD-4F06-AC9F-50DA38B717CA", ControlMechanism, "No practical control method available", 15)
    ];

    // The baseline rows predate the effective date columns, so both dates are null: no start date
    // was backfilled, and a null end date means the value is still active.
    private static ReferenceDataValue Value(string id, Guid referenceTableId, string lookupValue, byte sequenceNumber) =>
        new(new Guid(id), referenceTableId, lookupValue, EffectiveDateFrom: null, EffectiveDateTo: null, sequenceNumber, IsInUse: false);
}
