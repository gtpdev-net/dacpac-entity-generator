# DataManager — DACPAC Entity Generator

A .NET 8 solution that automatically generates C# Entity Framework Core entity classes, configuration, and DbContext files from SQL Server DACPAC schema metadata. Includes a Blazor Server web application for managing database schemas, importing DACPACs, and controlling code generation through a modern UI.

## Key Features

- **DACPAC Schema Extraction** — Parse `model.xml` from DACPAC files (SQL Server 2005–2022+) to extract tables, columns, views, stored procedures, functions, triggers, indexes, foreign keys, and constraints
- **Entity Class Generation** — Produce properly-typed C# entity classes with Data Annotations (`[Table]`, `[Column]`, `[Key]`, `[Required]`, `[MaxLength]`, `[Timestamp]`, `[DatabaseGenerated]`)
- **Dual EF Core Configuration** — Generate both SQL Server and SQLite-compatible `ModelBuilder` configuration classes from the same schema
- **Smart Default Value Handling** — Backing-field pattern prevents EF Core sentinel value warnings for non-nullable columns with database defaults
- **Hash-Based Deduplication** — SHA-256 content hashing skips re-importing unchanged DACPACs
- **Multi-Database Support** — Process multiple servers and databases in a single generation run
- **Column-Level Control** — Select which columns to include via the web UI or Excel spreadsheets
- **Full Constraint Support** — Check constraints, unique constraints, filtered indexes, composite indexes, included columns
- **Discovery Reports** — JSON and HTML reports of stored procedures, triggers, sequences, and other schema elements
- **Blazor Server Web UI** — Browse schemas, import DACPACs, manage migration configs, and trigger generation with real-time log streaming
- **CSV Export API** — REST endpoint for exporting catalogue data with filtering

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (any edition) for the DataManagerDb metadata database
- SQL Server DACPAC files for schema extraction

## Quick Start

### 1. Clone and Build

```bash
git clone <repository-url>
cd dacpac-entity-generator
dotnet restore
dotnet build
```

### 2. Configure the Database

Edit the connection string in `src/DataManager.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DataManagerDb": "Server=localhost;Database=DataManagerDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Apply Migrations

```bash
cd src/DataManager.Web
dotnet ef database update --project ../DataManager.Infrastructure
```

### 4. Run the Web Application

```bash
dotnet run --project src/DataManager.Web
```

Navigate to `https://localhost:5001` to access the Blazor UI.

### 5. Import a DACPAC

1. Go to **Import → Import DACPAC** in the navigation
2. Upload a `.dacpac` file (named `<server>_<database>.dacpac`)
3. The schema is parsed and imported into the catalogue

### 6. Generate Entity Code

1. Go to **Generation → EF Generation**
2. Select the database(s) to generate
3. Click **Generate** — entity classes, configuration, and DbContext files are written to the configured output directories

## Configuration

### Generation Output Paths

Configure output directories in `appsettings.json`:

```json
{
  "GenerationPaths": {
    "SolutionRoot": "",
    "SqlEntityAndConfigOutputDir": "src/DataLayer.Core/Entities",
    "SqlDbContextFilePath": "src/DataLayer.Infrastructure/Persistence/Contexts/SQLDbContext.cs",
    "SqliteConfigOutputDir": "tests/DataLayer.Test.Utilities",
    "SqliteDbContextFilePath": "tests/DataLayer.Test.Utilities/SQLiteDbContext.cs",
    "DbSetMappingCsvPath": ""
  }
}
```

### DACPAC Naming Convention

DACPAC files must follow the pattern `<server-name>_<database-name>.dacpac`:

```
SQLPROD01_AdventureWorks.dacpac
DevServer_Northwind.dacpac
```

## Project Structure

```
DataManager.sln
├── src/
│   ├── DataManager.Core/               # Domain layer
│   │   ├── Abstractions/               # IGenerationLogger, ISchemaDataSource
│   │   ├── DTOs/                        # Data transfer objects
│   │   ├── Interfaces/                 # IDataManagerRepository
│   │   ├── Models/
│   │   │   ├── Dacpac/                 # In-memory DACPAC parse models
│   │   │   ├── Entities/              # EF Core entity models (20+ entities)
│   │   │   └── StoredProcedures/       # Stored procedure definitions
│   │   ├── Utilities/                  # SqlTypeMapper, NameConverter, HashHelper
│   │   └── Validation/                # FluentValidation validators
│   │
│   ├── DataManager.Infrastructure/      # Data access & services layer
│   │   ├── Dacpac/                     # DACPAC extraction & XML parsing
│   │   ├── Data/                        # DbContext, DbContextFactory, schema data source
│   │   ├── Extensions/                 # DI registration
│   │   ├── Generation/                 # Entity/config/DbContext generators, orchestrators
│   │   ├── Import/                     # DACPAC, Excel, and migration config import services
│   │   ├── Migrations/                 # EF Core migrations
│   │   └── Repositories/              # EfDataManagerRepository
│   │
│   └── DataManager.Web/                # Blazor Server web application
│       ├── Components/
│       │   ├── Layout/                 # MainLayout, NavMenu
│       │   ├── Pages/                  # Feature pages (Servers, Databases, Tables, Schema, Import, Generation, etc.)
│       │   └── Shared/                 # Reusable components (PaginatedTable, ConfirmDialog, Toast, etc.)
│       ├── Services/                   # BlazorGenerationLogger, NotificationService
│       ├── Program.cs                  # Composition root
│       └── appsettings.json            # Application configuration
│
└── docs/                               # Documentation
    ├── ARCHITECTURE.md                 # Solution architecture & patterns
    ├── DATA-MODEL.md                   # Entity models, DACPAC models, DTOs
    ├── CODE-GENERATION.md              # Generation pipeline details
    ├── CONFIGURATION.md                # Setup & configuration guide
    └── SERVICES.md                     # Service & API reference
```

## Architecture

The solution follows **Clean Architecture** with three layers:

| Layer | Project | Responsibility |
|---|---|---|
| **Domain** | `DataManager.Core` | Models, interfaces, DTOs, validation, utilities |
| **Infrastructure** | `DataManager.Infrastructure` | EF Core, DACPAC parsing, code generation, imports |
| **Presentation** | `DataManager.Web` | Blazor Server UI, HTTP pipeline, DI composition |

Dependency flow: **Web → Infrastructure → Core**

Key patterns:
- **Repository pattern** with `IDbContextFactory` for Blazor-safe data access
- **Strategy pattern** for pluggable schema data sources (database vs. file-based)
- **Observer pattern** for real-time generation log streaming to the UI
- **Orchestrator pattern** for coordinating multi-step generation pipelines

## Technology Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 8.0 | Runtime and SDK |
| C# | 12 | Language |
| ASP.NET Core Blazor Server | 8.0 | Interactive web UI |
| Entity Framework Core | 8.0.11 | ORM, migrations, data access |
| SQL Server | 2005–2022+ | Database (DACPAC source + metadata store) |
| FluentValidation | 12.1.1 | Input validation |
| ClosedXML | 0.105.0 | Excel file processing |

## Web UI Features

| Page | Description |
|---|---|
| **Home** | Dashboard with catalogue summary statistics |
| **Servers** | Manage tracked server instances and connections |
| **Databases** | Manage source databases per server |
| **Tables** | Browse and edit table metadata, column selection |
| **Schema Browser** | Tree-based schema explorer with detail panels for views, procs, functions, triggers, indexes, FKs, constraints |
| **Import DACPAC** | Upload single DACPAC files or import entire folders |
| **Import Excel** | Import column catalogue from Excel spreadsheets |
| **EF Generation** | Trigger code generation with real-time progress streaming |
| **Global Catalogue** | Cross-server/database column search with CSV export |
| **Migration Config** | Source→destination table mapping configuration |

## API Endpoints

```
GET /api/catalogue/export?server=&database=&table=&column=&persistence=
```

Returns a CSV download of filtered catalogue columns.

## DACPAC Compatibility

Supports DACPAC FileFormatVersion 1.2, SchemaVersion 3.5:
- SQL Server 2005 (Sql90) through SQL Server 2022 (Sql160)
- Standard Microsoft Data Tools DACPAC format

## Documentation

| Document | Description |
|---|---|
| [Architecture](docs/ARCHITECTURE.md) | Solution architecture, patterns, data flow, component hierarchy |
| [Data Model](docs/DATA-MODEL.md) | Entity models, DACPAC models, DTOs, database schema |
| [Code Generation](docs/CODE-GENERATION.md) | Generation pipeline, type mapping, naming conventions, backing-field pattern |
| [Configuration](docs/CONFIGURATION.md) | Setup guide, connection strings, app settings, validation rules |
| [Services Reference](docs/SERVICES.md) | Complete service, repository, and utility API reference |

## Building

```bash
dotnet restore
dotnet build
dotnet build -c Release
```
