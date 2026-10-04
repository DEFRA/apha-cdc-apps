# CDC.Database scripts

Numbered SQL scripts for changes to the shared Surveillance Profiles database, applied manually
(no migration tool) since the volume of new changes here is still small. Each script is run once
per environment, in order, via `sqlcmd` (or SSMS "Open File" + Execute) against that environment's
connection - the same script, unedited, every time.

```powershell
sqlcmd -S <server> -d <database> -i Scripts\0056-AddCidmSsoIdToUser.sql
```

## Applied-to tracker

There is no journal table, so track what has been run here:

| Script | Local | Dev | Test | Prod |
|---|---|---|---|---|
| 0056-AddCidmSsoIdToUser.sql | | | | |
| 0057-CreateSpgUserByCidmSsoId.sql | | | | |
| 0058-CreateSpgUserByEmailAddress.sql | | | | |
| 0059-CreateSpuUserCidmSsoId.sql | | | | |
| 0060-CreateSpiExternalUser.sql | | | | |

If this grows to the point where manually tracking applied scripts becomes error-prone, introduce
a proper migration tool (e.g. DbUp) pointed at this same `Scripts` folder rather than changing how
scripts are authored.
