# CDC.Database scripts

Numbered SQL scripts for changes to the shared Surveillance Profiles database, applied manually
(no migration tool) since the volume of new changes here is still small. Each script is run once
per environment, in order, via `sqlcmd` (or SSMS "Open File" + Execute) against that environment's
connection - the same script, unedited, every time.

```powershell
sqlcmd -S <server> -d <database> -i Scripts\0056-AddSsoUserIdExtToUser.sql
```

## Applied-to tracker

There is no journal table, so track what has been run here:

| Script | Local | Dev | Test | Prod |
|---|---|---|---|---|
| 0056-AddSsoUserIdExtToUser.sql | Y | | | |
| 0057-CreateSpgUserByEmailAddress.sql | Y | | | |
| 0058-CreateSpuUserSsoUserIdExt.sql | Y | | | |
| 0059-CreateSpiExternalUser.sql | Y | | | |
| 0060-AddSsoUserIdIntToUser.sql | Y | | | |
| 0061-CreateSpuUserSsoUserIdInt.sql | Y | | | |
| 0062-ExtendSpgUserAuthorisationForSsoColumns.sql | Y | | | |
| 0063-AlterSpuSpeciesSequenceNumberForAuditTrail.sql | Y | | | |
| 0064-CreateSppSpecies.sql | | | | |
| 0065-CreateSpdSpecies.sql | | | | |
| 0066-AlterSpuSpeciesSequenceNumberForSequenceGaps.sql | Y | | | |
| 0067-ResequenceSpeciesAlphabetically.sql | | | | |
| 0068-CreateSpgDiseaseRankingFilters.sql | Y | | | |
| 0069-CreateSpgDiseaseRankingFilterDetail.sql | Y | | | |
| 0070-SeedD2R2QualityStatement.sql (needs `-v PdfPath="<pdf path>"`) | Y | | | |
| 0071-FixSpiSpdGlobalUserQuotedIdentifier.sql | Y | | | |
| 0072-FixSpiProfileContributorQuotedIdentifier.sql | Y | | | |
| 0073-CreateSpiInternalUser.sql | Y | | | |
| 0074-CreateSpgActingUser.sql | Y | | | |
| 0075-CreateSpiExternalUserPreRegistration.sql | Y | | | |
| 0076-AlterSpgUserByEmailAddressReturnSsoUserIdInt.sql | | | | |
| 0077-CreateSpuUserEmailAddress.sql | | | | |
| 0078-SpeedUpSpgReferenceValueByTable.sql | Y | | | |
| 0079-SpeedUpSpgaProfileSpeciesSort.sql | Y | | | |

The original 0057/0062/0063 scripts (`spgUserBySsoUserIdExt`, `spgUserBySsoUserIdInt`,
`spgUserByUserName`) were deleted and the rest renumbered - those procedures were superseded by
the `spgUserAuthorisation` consolidation (now 0062) and were never called by any application code.

0078 and 0079 are performance-only changes to procedures and a table the legacy site shares: the rows,
columns and order they return are unchanged (checked against the original procedures on the local
database), so they are safe for the legacy site and can be run before or after CDC.Api is deployed;
without them CDC.Api returns the same data, only slower (validating a profile version, building a
report and every Search took several seconds). 0078 builds an index on
`ProfileVersionFieldValue([ListValue])` (about 800,000 rows), which takes a few seconds; it is not a filtered index because
the legacy procedures that write that table run with `QUOTED_IDENTIFIER OFF`, which a filtered index rejects (error 1934).

If this grows to the point where manually tracking applied scripts becomes error-prone, introduce
a proper migration tool (e.g. DbUp) pointed at this same `Scripts` folder rather than changing how
scripts are authored.
