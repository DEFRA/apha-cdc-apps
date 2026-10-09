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
| 0063-AlterSpuSpeciesSequenceNumberForAuditTrail.sql | | | | |
| 0064-CreateSppSpecies.sql | | | | |
| 0065-CreateSpdSpecies.sql | | | | |
| 0066-AlterSpuSpeciesSequenceNumberForSequenceGaps.sql | | | | |
| 0067-ResequenceSpeciesAlphabetically.sql | | | | |

The original 0057/0062/0063 scripts (`spgUserBySsoUserIdExt`, `spgUserBySsoUserIdInt`,
`spgUserByUserName`) were deleted and the rest renumbered - those procedures were superseded by
the `spgUserAuthorisation` consolidation (now 0062) and were never called by any application code.

If this grows to the point where manually tracking applied scripts becomes error-prone, introduce
a proper migration tool (e.g. DbUp) pointed at this same `Scripts` folder rather than changing how
scripts are authored.
