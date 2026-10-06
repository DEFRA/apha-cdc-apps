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
| 0056-AddSsoUserIdExtToUser.sql | | | | |
| 0057-CreateSpgUserBySsoUserIdExt.sql | | | | |
| 0058-CreateSpgUserByEmailAddress.sql | | | | |
| 0059-CreateSpuUserSsoUserIdExt.sql | | | | |
| 0060-CreateSpiExternalUser.sql | | | | |
| 0061-AddSsoUserIdIntToUser.sql | | | | |
| 0062-CreateSpgUserBySsoUserIdInt.sql | | | | |
| 0063-CreateSpgUserByUserName.sql | | | | |
| 0064-CreateSpuUserSsoUserIdInt.sql | | | | |

If this grows to the point where manually tracking applied scripts becomes error-prone, introduce
a proper migration tool (e.g. DbUp) pointed at this same `Scripts` folder rather than changing how
scripts are authored.
