# Architecture

## Overview

DataManager is a .NET 8 solution that automatically generates C# Entity Framework Core entity classes and configuration from SQL Server DACPAC files. It provides both a legacy console-based pipeline and a modern Blazor Server web application for managing database schemas, importing DACPAC metadata, and generating code.

## Solution Structure

```
DataManager.sln
├── DataManager.Core            (Domain layer — models, interfaces, DTOs, utilities)
├── DataManager.Infrastructure  (Data access, services, generation, import logic)
└── DataManager.Web             (Blazor Server UI, API endpoints, configuration)
```

### Layer Responsibilities

| Layer | Purpose | Dependencies |
|---|---|---|
| **Core** | Domain models, abstractions, DTOs, validation, utility classes | FluentValidation |
| **Infrastructure** | EF Core data access, DACPAC parsing, code generation, schema import | Core |
| **Web** | Blazor Server UI, HTTP pipeline, DI composition root, config | Infrastructure |

Dependency flow is strictly **Web → Infrastructure → Core** (Clean Architecture).

## Architectural Patterns

### Clean Architecture

The solution follows Clean Architecture principles with clear separation of concerns:

- **Core** contains no framework dependencies beyond FluentValidation. It defines the domain models and abstractions.
- **Infrastructure** implements all I/O: database access, file system operations, DACPAC extraction, and code generation.
- **Web** is the composition root that wires everything together via dependency injection.

### Repository Pattern

`IDataManagerRepository` defines all data access operations. The single implementation, `EfDataManagerRepository`, uses `IDbContextFactory<DataManagerDbContext>` to create short-lived DbContext instances per operation. This prevents concurrent-access errors inherent to Blazor Server's long-lived SignalR circuits.

```
IDataManagerRepository (Core)
    └── EfDataManagerRepository (Infrastructure)
            └── IDbContextFactory<DataManagerDbContext>
```

### Strategy Pattern — Schema Data Sources

The `ISchemaDataSource` abstraction decouples the generation pipeline from its data source:

```
ISchemaDataSource
├── DataManagerDbSchemaDataSource  (reads from the DataManager SQL Server database)
└── (Excel + DACPAC files)         (legacy file-based pipeline via GenerationOrchestrator)
```

### Observer Pattern — Generation Logging

`IGenerationLogger` decouples log message emission from the transport:

```
IGenerationLogger
├── BlazorGenerationLogger  (fires events for Blazor UI streaming)
└── (Console logger)        (console output for CLI pipeline)
```

`BlazorGenerationLogger` raises an `OnLog` event that Blazor components subscribe to for real-time UI updates during generation.

### Orchestrator Pattern

Two orchestrators coordinate the generation pipeline:

| Orchestrator | Data Source | Use Case |
|---|---|---|
| `GenerationOrchestrator` | Excel + DACPAC files on disk | Legacy console pipeline |
| `DataManagerGenerationOrchestrator` | `ISchemaDataSource` (database) | Web UI generation |

Both follow the same pipeline stages but differ in how they acquire table definitions.

## High-Level Data Flow

```
┌──────────────────────┐     ┌──────────────────────┐     ┌────────────────────────┐
│   Input Sources      │     │  Processing          │     │  Output                │
│                      │     │                      │     │                        │
│  DACPAC files (.zip) │────>│  DacpacExtractor     │     │  Entity .cs files      │
│                      │     │  ModelXmlParser      │────>│  Configuration .cs     │
│  Excel spreadsheet   │────>│  PrimaryKeyEnricher  │     │  SQLDbContext.cs       │
│                      │     │                      │     │  SQLiteDbContext.cs    │
│  DataManagerDb       │────>│  Generation          │     │  Discovery reports     │
│  (SQL Server)        │     │  Orchestrator(s)     │     │  (JSON + HTML)         │
└──────────────────────┘     └──────────────────────┘     └────────────────────────┘
```

### DACPAC Import Flow

```
1. DACPAC file (.dacpac = ZIP)
   └── Extract model.xml via System.IO.Compression
       └── Parse XML with System.Xml.Linq (XDocument)
           ├── Parse tables, columns, primary keys, defaults
           ├── Parse indexes, foreign keys, constraints
           ├── Parse views, stored procedures, functions, triggers
           └── Compute SHA-256 hash for deduplication
               └── Upsert into DataManagerDb
                   ├── Server / SourceDatabase (ensure exist)
                   ├── SourceTable / SourceColumn (upsert)
                   └── Schema objects (delete + replace)
```

### Entity Generation Flow

```
1. Load tables from data source (DB or Excel+DACPAC)
2. Validate each table definition
3. For each valid table:
   a. Generate entity class (.cs)      → EntityClassGenerator
   b. Write to {Server}/{Database}/    → FileWriterService
4. Group by Server/Database:
   a. Generate SQL Server config       → EntityConfigurationGenerator
   b. Generate SQLite config           → EntityConfigurationGenerator
5. Generate SQLDbContext.cs            → DbContextGenerator
6. Generate SQLiteDbContext.cs         → DbContextGenerator
7. (Optional) Replace old DbSet names  → DbSetReplacementService
8. Write discovery reports             → ReportWriterService
```

## Web Application Architecture

### Blazor Server

The web layer uses ASP.NET Core Blazor with Interactive Server rendering. All UI logic executes on the server over a persistent SignalR connection.

**Key characteristics:**
- Razor components under `Components/Pages/` organized by feature
- Shared components under `Components/Shared/` for reusable UI elements
- Scoped service lifetime for per-circuit isolation
- `IDbContextFactory` prevents shared DbContext issues across components

### Minimal API

A single REST endpoint provides CSV export:

```
GET /api/catalogue/export?server=&database=&table=&column=&persistence=
```

Returns a filtered CSV download of all catalogue columns.

### Component Hierarchy

```
App.razor
└── MainLayout.razor
    ├── NavMenu.razor
    └── Pages/
        ├── Home.razor
        ├── Servers/          (ServerList, ServerEdit, TargetDatabaseList, TargetDatabaseEdit)
        ├── Databases/        (DatabaseList, DatabaseEdit)
        ├── Tables/           (TableList, TableEdit)
        ├── Schema/           (SchemaBrowser, SchemaOverview, ViewDetail, StoredProcedureDetail, ...)
        ├── Import/           (ImportDacpac, ImportDacpacFolder, ImportExcel)
        ├── Generation/       (EfGeneration, DiscoveryReportPanel)
        ├── GlobalView/       (GlobalCatalogueView)
        └── MigrationConfig/  (MigrationConfigView)
```

### Shared Components

| Component | Purpose |
|---|---|
| `PaginatedTable` | Generic paginated data table |
| `ConfirmDialog` | Modal confirmation dialog |
| `Toast` | Notification toast messages |
| `ProgressLog` | Streaming log viewer for generation |
| `SchemaTreePanel` / `SchemaTreeNode` | Tree-based schema browser |
| `DetailPanels/*` | Inline detail panels for schema objects |
| `FlagBadge` / `PersistenceTypeBadge` | Visual status indicators |

## Dependency Injection Configuration

All services are registered in `InfrastructureServiceExtensions.AddInfrastructure()`:

| Registration | Lifetime | Rationale |
|---|---|---|
| `DataManagerDbContext` | Scoped | Standard EF Core lifetime |
| `IDbContextFactory<DataManagerDbContext>` | Scoped | Safe for Blazor Server circuits |
| `IDataManagerRepository` | Scoped | Per-circuit data access |
| `DacpacSchemaImportService` | Scoped | Stateful import operations |
| `EntityClassGenerator` | Transient | Stateless code generation |
| `DacpacExtractorService` | Transient | Stateless file operations |
| `ExcelImportService` | Singleton | Thread-safe, no state |
| `BlazorGenerationLogger` | Scoped | Per-circuit event stream |

## Technology Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 8.0 | Runtime and SDK |
| C# | 12 | Language |
| ASP.NET Core | 8.0 | Web framework |
| Blazor Server | 8.0 | Interactive UI |
| Entity Framework Core | 8.0.11 | ORM and migrations |
| SQL Server | 2005–2022+ | Database (DACPAC source + DataManagerDb) |
| FluentValidation | 12.1.1 | Input validation |
| ClosedXML | 0.105.0 | Excel file reading |
| System.IO.Compression | — | DACPAC (ZIP) extraction |
| System.Xml.Linq | — | model.xml parsing |
