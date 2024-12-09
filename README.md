# DataVoyager
Datamigration CLI tool for SQL server. Support exporting and imports databases from Azure SQL and SQL Standalone.

## Usage

### Export
Exports a database to a DVO file.

```bash
dvicli export \
  --output "c:\temp\export.dvo" \
  --connection "Server=(azureserver).database.windows.net;Authentication=Active Directory Interactive;Encrypt=True;Database=(database)" \
  --ignore "MyTable"
```

**Options**
| parameter | description |
|-|-|
| output | File name. |
| connection | Connection-string to the database. |
| ignore | Comma-seperated list of tables to ignore. |

### Import
Imports a DVO file to a database.
