# Configuration & Setup Guide

## Prerequisites

- **.NET 8.0 SDK** or later
- **SQL Server** (any edition) for the DataManagerDb metadata database
- SQL Server DACPAC files for schema extraction
- (Optional) Excel files (.xlsx) for column selection filtering

## Building the Solution

```bash
# From the repository root
dotnet restore
dotnet build

# Release build
dotnet build -c Release
```

## Database Setup

### 1. Configure the Connection String

Edit `src/DataManager.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DataManagerDb": "Server=localhost;Database=DataManagerDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

For **production**, use `appsettings.Production.json` with Azure Key Vault:

```json
{
  "ConnectionStrings": {
    "DataManagerDb": "@Microsoft.KeyVault(SecretUri=https://YOUR_KEYVAULT.vault.azure.net/secrets/DataManagerDbConnectionString/)"
  }
}
```

### 2. Apply Migrations

```bash
cd src/DataManager.Web
dotnet ef database update --project ../DataManager.Infrastructure
```

This creates the `DataManagerDb` database with all catalogue tables.

### Design-Time Factory

The `DataManagerDbContextFactory` in `DataManager.Infrastructure/Data/` resolves the connection string from `appsettings.json` in the Web project at design time, supporting EF Core CLI commands.

## Application Configuration

### appsettings.json

```json
{
  "AppTitle": "Source DataManager",
  "GenerationPaths": {
    "SolutionRoot": "",
    "SqlEntityAndConfigOutputDir": "src/DataLayer.Core/Entities",
    "SqlDbContextFilePath": "src/DataLayer.Infrastructure/Persistence/Contexts/SQLDbContect.cs",
    "SqliteConfigOutputDir": "tests/DataLayer.Test.Utilities",
    "SqliteDbContextFilePath": "tests/DataLayer.Test.Utilities/SQLiteDbContext.cs",
    "DbSetMappingCsvPath": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DataManagerDb": "Server=localhost;Database=DataManagerDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### GenerationPaths Settings

These configure the output directories for entity code generation:

| Setting | Description |
|---|---|
| `SolutionRoot` | Base path for all output directories (empty = current directory) |
| `SqlEntityAndConfigOutputDir` | Directory for generated entity classes and SQL Server EF Core configuration files |
| `SqlDbContextFilePath` | Full path for the generated SQL Server `DbContext` file |
| `SqliteConfigOutputDir` | Directory for SQLite-compatible EF Core configuration files |
| `SqliteDbContextFilePath` | Full path for the generated SQLite `DbContext` file |
| `DbSetMappingCsvPath` | (Optional) Path to a CSV file mapping old DbSet names to new ones for automated refactoring |

## Running the Web Application

```bash
cd src/DataManager.Web
dotnet run
```

The application starts on `https://localhost:5001` (HTTPS) or `http://localhost:5000` (HTTP) by default.

### Development Mode

In development mode, detailed error pages are enabled. Configure the environment:

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/DataManager.Web
```

## Dependency Injection

All infrastructure services are registered via `InfrastructureServiceExtensions.AddInfrastructure()`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

This registers:
- **DbContext** with SQL Server provider and `IDbContextFactory` for Blazor-safe access
- **Repository** (`IDataManagerRepository` → `EfDataManagerRepository`)
- **Import services** (DataManagerImportService, ExcelImportService, DacpacSchemaImportService)
- **DACPAC parsing** (DacpacExtractorService, ModelXmlParserService, PrimaryKeyEnricher)
- **Generation** (EntityClassGenerator, EntityConfigurationGenerator, DbContextGenerator, etc.)
- **Orchestrators** (GenerationOrchestrator, DataManagerGenerationOrchestrator)

Additional Web-layer registrations in `Program.cs`:
- **FluentValidation** auto-validation
- **BlazorGenerationLogger** (scoped, implements `IGenerationLogger`)
- **NotificationService** (scoped)

## DACPAC File Naming Convention

DACPAC files must follow the naming pattern:

```
<server-name>_<database-name>.dacpac
```

For example:
- `SQLPROD01_AdventureWorks.dacpac`
- `DevServer_Northwind.dacpac`

The server name and database name are parsed from the filename by splitting on the first underscore.

## Excel File Format

The Excel input file (.xlsx) should contain these columns:

| Column | Type | Description |
|---|---|---|
| Server | string | Server name matching the DACPAC prefix |
| Database | string | Database name matching the DACPAC suffix |
| Schema | string | SQL schema name (e.g., `dbo`) |
| Table | string | Table name |
| Column | string | Column name |
| TableInDaoAnalysis | bool | Whether the table was analyzed |
| PersistenceType | string | `R` (Relational), `D` (Document), `B` (Both) |
| AddedByAPI | bool | Whether added programmatically |
| DevPersistenceType | string | Developer-assigned persistence type |
| Generate | bool | **Filter flag** — only `true` rows are processed |

## Validation Rules

FluentValidation is used for entity validation:

| Entity | Rules |
|---|---|
| **Server** | `ServerName`: required, max 255; `Description`: max 1000; `Role`: valid enum |
| **TargetDatabase** | `DatabaseName`: required, max 255; `ServerId`: > 0 |
| **ServerConnection** | `Hostname`: required, max 255; `Port`: 1–65535 if set; `Username`: required for SqlAuth |
| **SourceDatabase** | `DatabaseName`: required, max 255; `ServerId`: > 0 |
| **SourceTable** | `SchemaName`: required, max 128; `TableName`: required, max 255; `DatabaseId`: > 0 |

## Environment Variables

| Variable | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment (`Development`, `Production`) |
| `ConnectionStrings__DataManagerDb` | Override connection string via environment |
