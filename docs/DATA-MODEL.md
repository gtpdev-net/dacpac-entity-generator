# Data Model

## Overview

DataManager uses two distinct model families:

1. **Entity Models** — EF Core entities persisted in the `DataManagerDb` SQL Server database, representing the catalogue of tracked servers, databases, tables, columns, and schema objects.
2. **DACPAC Models** — In-memory DTOs representing parsed DACPAC schema, used as intermediate structures during import and code generation.

## Entity Model Hierarchy

```
Server
├── ServerConnection          (1:1 — connection details)
├── SourceDatabase[]          (1:N — source databases)
│   ├── SourceTable[]         (1:N — tables)
│   │   ├── SourceColumn[]    (1:N — columns with catalogue metadata)
│   │   ├── SourceIndex[]     (1:N — indexes)
│   │   │   └── SourceIndexColumn[]
│   │   ├── SourceForeignKey[] (1:N — foreign keys)
│   │   │   └── SourceForeignKeyColumn[]
│   │   ├── SourceCheckConstraint[] (1:N)
│   │   ├── SourceUniqueConstraint[] (1:N)
│   │   │   └── SourceUniqueConstraintColumn[]
│   │   ├── SourceTrigger[]    (1:N)
│   │   └── MigrationConfig[]  (1:N — data migration mappings)
│   ├── SourceView[]           (1:N — views)
│   │   └── SourceViewColumn[]
│   ├── SourceStoredProcedure[] (1:N)
│   │   └── SourceStoredProcedureParameter[]
│   └── SourceFunction[]       (1:N)
└── TargetDatabase[]           (1:N — target/destination databases)

CopyActivityLog                (standalone — pipeline execution log)
```

## Core Entities

### Server

Represents a tracked database server instance.

| Field | Type | Description |
|---|---|---|
| `ServerId` | `int` (PK) | Auto-increment identifier |
| `ServerName` | `string` (max 255, unique) | Logical server name |
| `Description` | `string?` (max 1000) | Optional description |
| `IsActive` | `bool` (default: true) | Soft-delete flag |
| `Role` | `ServerRole` enum | `Source` (0) or `Target` (1) |
| `CreatedAt` | `DateTime` | UTC creation timestamp |
| `CreatedBy` | `string?` | Creator identity |
| `ModifiedAt` | `DateTime?` | Last modification timestamp |
| `ModifiedBy` | `string?` | Last modifier identity |

**Navigation:** `Databases`, `TargetDatabases`, `Connection`

### ServerRole (Enum)

| Value | Description |
|---|---|
| `Source` (0) | Source server for schema extraction |
| `Target` (1) | Destination server for data migration |

### ServerConnection

One-to-one connection details for a server.

| Field | Type | Description |
|---|---|---|
| `ServerConnectionId` | `int` (PK) | Auto-increment identifier |
| `ServerId` | `int` (FK, unique) | Parent server |
| `Hostname` | `string` (max 255) | Network hostname |
| `Port` | `int?` | TCP port (1–65535) |
| `NamedInstance` | `string?` (max 128) | SQL Server named instance |
| `AuthenticationType` | `AuthenticationType` enum | Authentication method |
| `Username` | `string?` (max 255) | SQL Auth username |

### AuthenticationType (Enum)

| Value | Description |
|---|---|
| `WindowsAuth` (0) | Windows Integrated Authentication |
| `SqlAuth` (1) | SQL Server Authentication |
| `AzureAD` (2) | Azure Active Directory |

### SourceDatabase

Represents a database on a tracked server.

| Field | Type | Description |
|---|---|---|
| `DatabaseId` | `int` (PK) | Auto-increment identifier |
| `ServerId` | `int` (FK) | Parent server |
| `DatabaseName` | `string` (max 255) | Database name |
| `Description` | `string?` (max 1000) | Optional description |
| `IsActive` | `bool` (default: true) | Soft-delete flag |
| `LastImportedModelHash` | `string?` | SHA-256 hex digest of last imported model.xml |
| `LastImportedAt` | `DateTime?` | UTC timestamp of last successful import |

**Unique constraint:** `(ServerId, DatabaseName)`

### SourceTable

Represents a table within a source database.

| Field | Type | Description |
|---|---|---|
| `TableId` | `int` (PK) | Auto-increment identifier |
| `DatabaseId` | `int` (FK) | Parent database |
| `SchemaName` | `string` (default: "dbo") | SQL schema name |
| `TableName` | `string` | Table name |
| `EstimatedRowCount` | `long?` | Approximate row count |
| `Notes` | `string?` (max 4000) | User notes |
| `IsActive` | `bool` (default: true) | Soft-delete flag |

### SourceColumn

Represents a column in a source table, combining catalogue metadata with schema details from DACPAC imports.

| Field | Type | Description |
|---|---|---|
| `ColumnId` | `int` (PK) | Auto-increment identifier |
| `TableId` | `int` (FK) | Parent table |
| `ColumnName` | `string` | Column name |
| `PersistenceType` | `char` | `'R'` = Relational, `'D'` = Document, `'B'` = Both |
| `IsInDaoAnalysis` | `bool` | Included in DAO analysis |
| `IsAddedByApi` | `bool` | Added via API import |
| `IsSelectedForLoad` | `bool` | Selected for code generation |
| `SortOrder` | `int` | Display/generation order |
| `IsActive` | `bool` | Soft-delete flag |

**Schema metadata fields** (populated by `DacpacSchemaImportService`):

| Field | Type | Description |
|---|---|---|
| `SqlType` | `string?` | SQL Server data type (e.g., `nvarchar(255)`) |
| `IsNullable` | `bool` | Column allows NULL |
| `MaxLength` | `int?` | Maximum character length |
| `IsIdentity` | `bool` | IDENTITY column |
| `IsPrimaryKey` | `bool` | Part of primary key |
| `Precision` | `int?` | Decimal precision |
| `Scale` | `int?` | Decimal scale |
| `DefaultValue` | `string?` | SQL default expression |
| `IsComputed` | `bool` | Computed column |
| `IsComputedPersisted` | `bool` | Persisted computed column |
| `ComputedExpression` | `string?` | Computation SQL expression |
| `IsRowVersion` | `bool` | Row version / timestamp |
| `IsConcurrencyToken` | `bool` | Concurrency token |
| `Collation` | `string?` | Column collation |
| `Description` | `string?` | Column description |

### TargetDatabase

Represents a destination database on a target server.

| Field | Type | Description |
|---|---|---|
| `TargetDatabaseId` | `int` (PK) | Auto-increment identifier |
| `ServerId` | `int` (FK) | Parent server |
| `DatabaseName` | `string` (max 255) | Database name |
| `Description` | `string?` | Optional description |
| `IsActive` | `bool` | Soft-delete flag |

**Unique constraint:** `(ServerId, DatabaseName)`

## Schema Objects

### SourceIndex / SourceIndexColumn

Indexes on source tables, with per-column sort direction tracking.

### SourceForeignKey / SourceForeignKeyColumn

Foreign key relationships, including cascade-delete flag and cardinality.

### SourceCheckConstraint

Named check constraint with its SQL expression.

### SourceUniqueConstraint / SourceUniqueConstraintColumn

Named unique constraints with ordered column lists.

### SourceView / SourceViewColumn

Database views with optional SQL body and column metadata.

### SourceStoredProcedure / SourceStoredProcedureParameter

Stored procedures with parameters (type, direction, defaults).

### SourceFunction

User-defined functions with function type and return type.

### SourceTrigger

Table-level triggers with optional SQL body.

## Migration & Activity Tracking

### MigrationConfig

Defines source→destination table migration mappings.

| Field | Type | Description |
|---|---|---|
| `MigrationConfigId` | `int` (PK) | Auto-increment identifier |
| `TableId` | `int` (FK) | Associated source table |
| `SourceServer` | `string` | Source server name |
| `SourceDatabase` | `string` | Source database name |
| `SourceSchema` | `string` | Source schema name |
| `SourceTableName` | `string` | Source table name |
| `DestinationServer` | `string?` | Target server name |
| `DestinationDatabase` | `string?` | Target database name |
| `DestinationSchema` | `string` | Target schema name |
| `DestinationTable` | `string` | Target table name |
| `ColumnList` | `string` | Comma-separated column list |
| `FilterCondition` | `string?` | Optional WHERE clause |
| `IsActive` | `bool` | Active flag |

### CopyActivityLog

Records pipeline execution results for data copy operations.

| Field | Type | Description |
|---|---|---|
| `LogId` | `int` (PK) | Auto-increment identifier |
| `PipelineRunId` | `string` | Pipeline execution identifier |
| `MigrationConfigId` | `int` | Associated migration config |
| `Status` | `string` | Success/Failure |
| `ErrorMessage` | `string?` | Error details (failures only) |
| `RowsCopied` | `long` | Number of rows transferred |
| `StartTime` | `DateTime` | Execution start |
| `EndTime` | `DateTime` | Execution end |
| `DurationSeconds` | `int` | Elapsed time |

## DACPAC Models (In-Memory)

These models represent parsed DACPAC schema and are used transiently during import and code generation. They are **not** persisted directly.

### TableDefinition

```csharp
public class TableDefinition
{
    public string Server { get; set; }
    public string Database { get; set; }
    public string Schema { get; set; }
    public string TableName { get; set; }
    public List<ColumnDefinition> Columns { get; set; }
    public List<IndexDefinition> Indexes { get; set; }
    public List<ForeignKeyDefinition> ForeignKeys { get; set; }
    public List<CheckConstraintDefinition> CheckConstraints { get; set; }
    public List<UniqueConstraintDefinition> UniqueConstraints { get; set; }
}
```

### ColumnDefinition

```csharp
public class ColumnDefinition
{
    public string Name { get; set; }
    public string SqlType { get; set; }          // e.g., "nvarchar(255)", "int", "decimal(18,2)"
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsFromExcel { get; set; }         // true if column was in the Excel filter
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? DefaultValue { get; set; }     // SQL default expression, e.g., "((0))"
    public bool IsComputed { get; set; }
    public bool IsComputedPersisted { get; set; }
    public string? ComputedExpression { get; set; }
    public bool IsRowVersion { get; set; }
    public bool IsConcurrencyToken { get; set; }
    public string? Collation { get; set; }
    public string? Description { get; set; }
}
```

### Other DACPAC Models

| Model | Description |
|---|---|
| `IndexDefinition` | Index with columns, uniqueness, filter, included columns, sort order |
| `ForeignKeyDefinition` | FK with from/to schema.table.column mappings and cascade behavior |
| `CheckConstraintDefinition` | Named check constraint with SQL expression |
| `UniqueConstraintDefinition` | Named unique constraint with column list |
| `ViewDefinition` | View with columns, SQL body, and schema info |
| `StoredProcedureDefinition` | Stored procedure with parameters and SQL body |
| `FunctionDefinition` | User-defined function with type and return info |
| `TriggerDefinition` | Trigger with table association and SQL body |
| `ParameterDefinition` | Stored procedure / function parameter metadata |
| `ElementDiscoveryReport` | Aggregation of schema elements for reporting |
| `GenerationResult` | Summary of a generation run (counts, errors, reports) |
| `DacpacImportResult` | Summary of a DACPAC import operation |

## DTO Layer

DTOs in `DataManager.Core.DTOs` provide flattened, read-optimized projections for the UI:

| DTO | Purpose |
|---|---|
| `SourceDatabaseInfo` | Database summary with table/column counts |
| `SourceTableInfo` | Table summary with scope/selection counts |
| `SourceColumnInfo` | Flattened column with server/database/table context |
| `DataManagerSummaryDto` | Dashboard aggregate statistics |
| `ImportPreviewRow` | Excel/DACPAC import preview row |
| `ImportResultDto` | Import operation results |
| `SchemaImportResultDto` | DACPAC schema import results |
| `SourceViewSummary` / `SourceViewDetail` | View list/detail projections |
| `SourceStoredProcedureSummary` / `SourceStoredProcedureDetail` | Stored procedure projections |
| `SourceFunctionSummary` / `SourceFunctionDetail` | Function projections |
| `SourceTriggerSummary` / `SourceTriggerDetail` | Trigger projections |
| `SourceIndexSummary` | Index summary with column list |
| `SourceForeignKeySummary` / `SourceForeignKeyColumnDto` | Foreign key projections |
| `SourceCheckConstraintSummary` | Check constraint summary |
| `SourceUniqueConstraintSummary` | Unique constraint summary |
| `MigrationConfigInfo` | Migration config with source/destination info |
| `MigrationConfigLoadResult` | Bulk migration config load statistics |
| `TargetServerOption` | Server dropdown option DTO |

## Column Filtering Model

The `ColumnFilter` enum defines predefined column selection scopes:

| Value | Criteria |
|---|---|
| `All` | All active columns |
| `InScopeRelational` | `(IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'R'` |
| `InScopeDocument` | `(IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'D'` |
| `SelectedForLoad` | `IsSelectedForLoad = true` |

## ExcelRow Model

Represents a row from the input Excel spreadsheet used for column selection filtering:

```csharp
public class ExcelRow
{
    public string Server { get; set; }
    public string Database { get; set; }
    public string Schema { get; set; }
    public string Table { get; set; }
    public string Column { get; set; }
    public bool TableInDaoAnalysis { get; set; }
    public string PersistenceType { get; set; }
    public bool AddedByAPI { get; set; }
    public string DevPersistenceType { get; set; }
    public bool Generate { get; set; }           // Filter flag — only rows with Generate=true are processed
}
```

## Database Migrations

| Migration | Description |
|---|---|
| `InitialCreate` | Core tables: Servers, SourceDatabases, SourceTables, SourceColumns |
| (Schema entities) | Views, stored procedures, functions, indexes, FKs, constraints, triggers |
| (Model hash) | `LastImportedModelHash` and `LastImportedAt` on SourceDatabases |
| (Migration config) | MigrationConfigs table for source→destination mappings |
