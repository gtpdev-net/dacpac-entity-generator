# DACPAC Entity Generator - Technical Specification

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Data Models](#data-models)
4. [Services](#services)
5. [Utilities](#utilities)
6. [Processing Pipeline](#processing-pipeline)
7. [Code Generation Strategy](#code-generation-strategy)
8. [File I/O Operations](#file-io-operations)
9. [Error Handling](#error-handling)
10. [Configuration](#configuration)

## Overview

### Purpose

The DACPAC Entity Generator is a code generation tool that creates Entity Framework Core entity classes from SQL Server database schemas. It bridges the gap between database-first development and code-first Entity Framework patterns by extracting schema metadata from DACPAC (Data-tier Application Package) files and generating strongly-typed C# entity classes.

### Technology Stack

- **Framework**: .NET 8.0
- **Language**: C# 12 with nullable reference types enabled
- **Dependencies**:
  - ClosedXML 0.105.0 (Excel file processing)
  - System.IO.Compression (DACPAC extraction)
  - System.Xml.Linq (XML parsing)

### Key Design Principles

1. **Separation of Concerns**: Each service has a single, well-defined responsibility
2. **Immutability**: Models use init-only properties where appropriate
3. **Fail-Fast**: Validation occurs early with clear error messages
4. **Explicit Configuration**: No hidden defaults; all decisions are logged
5. **Batch Processing**: Supports multiple servers/databases in a single execution

### Entity Design Pattern

The generated entities follow a **surrogate key pattern**:

- **BaseEntity Inheritance**: All generated entities inherit from `BaseEntity` (provided by the consuming application)
- **Surrogate Primary Key**: `BaseEntity` typically provides an `Id` property (e.g., `int`, `long`, `Guid`) as the actual EF Core primary key
- **Natural Keys as Unique Indexes**: The original database primary key columns are preserved in the entity but configured as unique indexes rather than EF Core keys
- **Data Integrity**: Unique indexes on the natural key columns maintain referential integrity and enforce business constraints

**Benefits**:
- Consistent primary key across all entities
- Simplified relationship mapping
- Better support for audit trails and soft deletes (common in BaseEntity implementations)
- Maintains original database constraints through unique indexes

**Example**:
```csharp
// BaseEntity (in consuming application)
public abstract class BaseEntity
{
    [Key]
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

// Generated Entity
public class Order : BaseEntity
{
    // Original PK columns become regular properties
    public int OrderNumber { get; set; }
    public string WarehouseCode { get; set; }
    // ... other properties
}

// Generated Configuration
modelBuilder.Entity<Order>()
    .HasIndex(e => new { e.OrderNumber, e.WarehouseCode })
    .IsUnique()
    .HasDatabaseName("IX_Orders_OrderNumber_WarehouseCode");
```

## Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Program.cs                              │
│                     (Orchestration Layer)                        │
└──────────────────────┬──────────────────────────────────────────┘
                       │
       ┌───────────────┼───────────────┐
       │               │               │
       ▼               ▼               ▼
┌──────────┐    ┌──────────┐    ┌──────────┐
│ Services │    │  Models  │    │Utilities │
└──────────┘    └──────────┘    └──────────┘
```

### Project Structure

```
DacpacEntityGenerator/
├── Program.cs                          # Entry point and orchestration
├── Models/                             # Data transfer objects
│   ├── ColumnDefinition.cs            # Column metadata
│   ├── TableDefinition.cs             # Table metadata
│   ├── IndexDefinition.cs             # Index metadata
│   ├── ForeignKeyDefinition.cs        # Foreign key metadata
│   ├── ViewDefinition.cs              # View metadata
│   ├── CheckConstraintDefinition.cs   # Check constraint metadata
│   ├── UniqueConstraintDefinition.cs  # Unique constraint metadata
│   ├── FunctionDefinition.cs          # User-defined function metadata
│   ├── ElementDiscoveryReport.cs      # Discovery report model
│   ├── ExcelRow.cs                    # Excel data row
│   └── GenerationResult.cs            # Process results
├── Services/                           # Business logic
│   ├── ExcelReaderService.cs          # Excel file processing
│   ├── DacpacExtractorService.cs      # DACPAC extraction
│   ├── ModelXmlParserService.cs       # XML schema parsing
│   ├── PrimaryKeyEnricher.cs          # PK metadata enrichment
│   ├── EntityClassGenerator.cs        # C# entity/view code generation
│   ├── DbContextGenerator.cs          # SQLDbContext class generation
│   ├── FileWriterService.cs           # File system operations
│   └── ReportWriterService.cs         # Discovery report output
├── Utilities/                          # Cross-cutting concerns
│   ├── ConsoleLogger.cs               # Colored console output
│   ├── SqlTypeMapper.cs               # SQL to C# type mapping
│   └── NameConverter.cs               # Naming convention conversion
├── docs/                               # Documentation
│   ├── SPEC.md                        # Technical specification (this file)
│   ├── PLAN.md                        # Feature implementation plan
│   ├── CONSOLE_APP_PLAN.md            # Console app feature plan
│   ├── BLAZOR_CONVERSION_PLAN.md      # Future Blazor UI plan
│   └── BOOL_DEFAULT_EXAMPLE.md        # Bool/int default value pattern
├── _input/                             # Input files (runtime)
│   └── dacpacs/                        # DACPAC files
└── _output/                            # Generated output (runtime)

```

### Layering Strategy

1. **Presentation Layer** (`Program.cs`): Console interface and workflow orchestration
2. **Service Layer** (`Services/`): Core business logic and processing
3. **Data Access Layer** (DACPAC and Excel files): External data sources
4. **Utilities Layer** (`Utilities/`): Reusable helper functions
5. **Model Layer** (`Models/`): Data structures

## Data Models

### ColumnDefinition

Represents a single database column's metadata.

```csharp
public class ColumnDefinition
{
    public string Name { get; set; }                  // Column name from database
    public string SqlType { get; set; }               // SQL Server data type
    public bool IsNullable { get; set; }              // NULL constraint
    public int? MaxLength { get; set; }               // String/binary length
    public bool IsIdentity { get; set; }              // IDENTITY specification
    public bool IsPrimaryKey { get; set; }            // PK membership
    public bool IsFromExcel { get; set; }             // User-requested vs auto-added
    public int? Precision { get; set; }               // Decimal precision
    public int? Scale { get; set; }                   // Decimal scale
    public string? DefaultValue { get; set; }         // SQL default constraint value
    public bool IsComputed { get; set; }              // Computed/calculated column
    public bool IsComputedPersisted { get; set; }     // Persisted computed column
    public string? ComputedExpression { get; set; }   // SQL expression for computed columns
    public bool IsRowVersion { get; set; }            // rowversion/timestamp column
    public bool IsConcurrencyToken { get; set; }      // Optimistic concurrency token
    public string? Collation { get; set; }            // Non-default collation
    public string? Description { get; set; }          // Extended property description
}
```

**Purpose**: Stores comprehensive column metadata needed for entity property generation.

**Key Fields**:
- `IsFromExcel`: Distinguishes user-requested columns from automatically-added PK columns
- `Precision` / `Scale`: Essential for decimal type mapping in EF Core
- `DefaultValue`: Captures SQL default constraint expressions (e.g., "0", "GETDATE()", "'Active'")
- `IsComputed` / `IsComputedPersisted` / `ComputedExpression`: Computed column support
- `IsRowVersion` / `IsConcurrencyToken`: EF Core concurrency token configuration
- `Collation`: Non-default collation configuration via `UseCollation()` in EF Core

### IndexDefinition

Represents a database index.

```csharp
public class IndexDefinition
{
    public string Name { get; set; }                         // Index name
    public List<string> Columns { get; set; }               // Ordered list of key column names
    public bool IsUnique { get; set; }                      // Unique constraint
    public bool IsClustered { get; set; }                   // Clustered index
    public bool IsPrimaryKeyIndex { get; set; }             // Generated from PK columns
    public List<string> IncludedColumns { get; set; }       // Non-key included columns
    public Dictionary<string, bool> ColumnSortOrder { get; set; } // true = ASC, false = DESC
    public string? FilterDefinition { get; set; }           // Filtered index WHERE clause
}
```

**Purpose**: Stores index metadata for EF Core index configuration generation.

**Key Fields**:
- `Columns`: Ordered list is important for composite indexes
- `IsPrimaryKeyIndex`: Indicates this index was auto-generated for primary key columns (since BaseEntity provides the actual key)
- `IncludedColumns`: Non-key columns included in index for covering scenarios
- `FilterDefinition`: WHERE clause for filtered index (mapped to `HasFilter()` in EF Core)
- `ColumnSortOrder`: Per-column sort direction (DESC columns require manual migration configuration)

### TableDefinition

Represents a complete database table with all its columns, indexes, and constraints.

```csharp
public class TableDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public List<IndexDefinition> Indexes { get; set; } = new();
    public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();
    public List<UniqueConstraintDefinition> UniqueConstraints { get; set; } = new();
}
```

**Purpose**: Aggregates all information needed to generate a single entity class and its EF Core configuration.

**Usage**: One `TableDefinition` instance generates one `.cs` entity file and contributes to the server/database configuration class.

**Constraint Collections**: `ForeignKeys`, `CheckConstraints`, and `UniqueConstraints` are parsed from the DACPAC and used to generate EF Core configuration (`HasCheckConstraint`, `HasAlternateKey`).

### ExcelRow

Represents a single row from the Excel input file.

```csharp
public class ExcelRow
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public bool TableInDaoAnalysis { get; set; }
    public string PersistenceType { get; set; } = string.Empty;
    public bool AddedByAPI { get; set; }
    public string DevPersistenceType { get; set; } = string.Empty;
    public bool Generate { get; set; }
}
```

**Purpose**: Captures user's column selection and filtering criteria.

**Filtering Logic**: Row is processed if:
```
(TableInDaoAnalysis == true OR AddedByAPI == true) AND PersistenceType == "R"
```

**Note**: The `Generate` flag in combination with the group filter (`tableGroups.Where(g => g.Any(r => r.Generate))`) ensures only tables with at least one active `Generate` row are processed.

### GenerationResult

Tracks the overall execution results.

```csharp
public class GenerationResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int EntitiesGenerated { get; set; }
    public int ViewsGenerated { get; set; }
    public int TablesSkipped { get; set; }
    public int ErrorsEncountered { get; set; }
}
```

**Purpose**: Provides summary statistics for the generation run.

**ViewsGenerated**: Count of view entities successfully generated (distinct from table entities).

**Errors**: List of error message strings displayed in the summary section.

### ViewDefinition

Represents a database view for keyless entity generation.

```csharp
public class ViewDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public bool HasStandardAuditColumns { get; set; }
}
```

**Purpose**: Stores view schema for generating keyless EF Core entities.

**HasStandardAuditColumns**: If the view contains `Id`, `CreatedDate`, and `ModifiedDate` columns (case-insensitive), the entity inherits from `BaseEntity`; otherwise it is decorated with `[Keyless]`.

### ForeignKeyDefinition

Represents a foreign key constraint relationship.

```csharp
public class ForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> FromColumns { get; set; } = new();
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public List<string> ToColumns { get; set; } = new();
    public bool OnDeleteCascade { get; set; }
    public bool OnUpdateCascade { get; set; }
    public ForeignKeyCardinality Cardinality { get; set; }
}

public enum ForeignKeyCardinality
{
    Unknown,
    OneToMany,
    OneToOne,
    ManyToMany
}
```

**Purpose**: Stores FK metadata parsed from DACPAC for inclusion in `TableDefinition.ForeignKeys`.

**Note**: Foreign keys are parsed and stored in the model but navigation properties are not currently generated in entity classes. The FK data is available for future navigation property generation.

### CheckConstraintDefinition

Represents a check constraint on a table.

```csharp
public class CheckConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public List<string> AffectedColumns { get; set; } = new();
}
```

**Purpose**: Stores check constraint expressions for EF Core `HasCheckConstraint()` configuration.

### UniqueConstraintDefinition

Represents a unique constraint (distinct from unique indexes).

```csharp
public class UniqueConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public bool IsClustered { get; set; }
}
```

**Purpose**: Stores unique constraint metadata for EF Core `HasAlternateKey()` configuration.

### FunctionDefinition

Represents a user-defined function discovered in the DACPAC.

```csharp
public class FunctionDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public FunctionType Type { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public List<FunctionParameter> Parameters { get; set; } = new();
}

public enum FunctionType { Scalar, TableValued, InlineTableValued }

public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
}
```

**Purpose**: Represents scalar, table-valued, and inline-table-valued functions. Currently surfaced in the discovery report for reference.

### ElementDiscoveryReport

Reports database elements that are discovered but not directly generated as entities.

```csharp
public class ElementDiscoveryReport
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public List<ElementDetail> ExtendedProperties { get; set; } = new();
    public List<ElementDetail> NonDefaultCollations { get; set; } = new();
    public List<ElementDetail> Sequences { get; set; } = new();
    public List<ElementDetail> SpatialColumns { get; set; } = new();
    public List<ElementDetail> HierarchyIdColumns { get; set; } = new();
    public List<ElementDetail> StoredProcedures { get; set; } = new();
    public List<ElementDetail> Triggers { get; set; } = new();
    public Dictionary<string, int> ElementTypeCounts { get; set; } = new();
    public List<string> UnhandledElementTypes { get; set; } = new();
}

public class ElementDetail
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
```

**Purpose**: Generated per server/database combination and written as both JSON and HTML reports to `_output/DiscoveryReports/`.

## Services

### ExcelReaderService

**Responsibility**: Read and parse Excel files to extract table/column selections.

**Key Methods**:

#### `FindExcelFile(string inputDirectory)`
- Scans directory for `.xlsx` files
- Returns first match
- Logs warning if multiple files found

#### `ReadAndFilterExcel(string filePath)`
- Opens workbook using ClosedXML
- Validates required columns exist
- Parses all rows into `ExcelRow` objects
- Applies filtering criteria
- Returns filtered list

**Column Validation**: Requires these columns in the first worksheet:
- Server
- Database
- Schema
- Table
- Column
- Table in DAO Analysis
- Persistence Type
- Added by API

**Boolean Parsing**: Supports multiple formats:
- Boolean literals: `true`, `false`
- Integer: `1`, `0`
- String: `"yes"`, `"no"`, `"true"`, `"false"`

#### `GroupByServerAndDatabase(List<ExcelRow> rows)`
Returns hierarchical dictionary:
```csharp
Dictionary<string, Dictionary<string, List<ExcelRow>>>
// Structure: Server -> Database -> List of rows
```

**Purpose**: Organizes data for batch processing by server/database combinations.

### DacpacExtractorService

**Responsibility**: Extract and read `model.xml` from DACPAC files.

**Key Methods**:

#### `ExtractModelXml(string inputDirectory, string server, string database)`
- Locates DACPAC file: `{server}_{database}.dacpac` in `dacpacs/` subdirectory
- Opens as ZIP archive (DACPAC is a ZIP file)
- Extracts `model.xml` entry
- Returns XML content as string
- Handles corrupted ZIPs gracefully

**DACPAC Structure**:
```
{Server}_{Database}.dacpac (ZIP file)
├── model.xml         ← Schema definition (target)
├── Origin.xml
└── [Content_Types].xml
```

#### `DacpacExists(string inputDirectory, string server, string database)`
- Pre-flight check before extraction
- Returns boolean indicating file presence

**Error Handling**:
- `InvalidDataException`: Corrupted ZIP file
- `FileNotFoundException`: Missing DACPAC
- Generic exceptions logged with context

### ModelXmlParserService

**Responsibility**: Parse SQL Server schema from DACPAC's `model.xml` file.

**XML Namespace**: `http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02`

**DACPAC Format Support**:
- **FileFormatVersion**: 1.2 (and compatible versions)
- **SchemaVersion**: 3.5 (and compatible versions)
- **SQL Server Versions**: 2005 (Sql90) through 2022 (Sql160) and beyond
- **DSP Provider**: Microsoft.Data.Tools.Schema.Sql.Sql140DatabaseSchemaProvider

**Key Methods**:

#### `ParseTable(...)`
Main entry point for table parsing.

**Parameters**:
- `modelXml`: Complete model.xml content
- `server`, `database`, `schema`, `tableName`: Identifiers
- `requiredColumns`: User-selected columns from Excel

**Algorithm**:
1. Parse XML document
2. **Validate DACPAC format** (see `ValidateDacpacFormat`)
3. Find table element by schema and name
4. Extract all columns from table
5. Extract primary key information
6. Extract default constraints
7. Filter columns: include if (in Excel OR is PK)
8. Mark columns with metadata flags
9. Apply default values where applicable
10. **Parse existing indexes** (see `ParseIndexes`)
11. **Ensure PK columns have an index**: If primary key columns don't have a covering index, create a unique index for them
12. Validate and return `TableDefinition`

**Architectural Note**: Entities inherit from `BaseEntity` which provides the actual primary key (typically an `Id` property). The original database primary key columns are preserved but configured as unique indexes rather than EF Core keys, maintaining data integrity while allowing the surrogate key pattern.

#### `ValidateDacpacFormat(XDocument doc, string server, string database)`
**New in current version**: Validates DACPAC file format and structure.

**Validations Performed**:
1. **Root Element**: Confirms `DataSchemaModel` exists
2. **Namespace**: Validates or warns about namespace mismatch
3. **Model Element**: Ensures `Model` element is present
4. **Format Attributes**: Logs FileFormatVersion and SchemaVersion

**SQL Server Version Detection**:
Extracts SQL Server version from `DspName` attribute:
```
Microsoft.Data.Tools.Schema.Sql.Sql140DatabaseSchemaProvider
                                  ^^^
                            Version code: 140 = SQL Server 2017
```

**Version Mapping**:
| Code | SQL Server Version |
|------|-------------------|
| 90   | SQL Server 2005   |
| 100  | SQL Server 2008   |
| 110  | SQL Server 2012   |
| 120  | SQL Server 2014   |
| 130  | SQL Server 2016   |
| 140  | SQL Server 2017   |
| 150  | SQL Server 2019   |
| 160  | SQL Server 2022   |

**Return Value**: `bool` - true if DACPAC is valid, false if critical errors found

**Error Handling**:
- Missing root element → Error, returns false
- Wrong root element name → Error, returns false
- Namespace mismatch → Warning, continues processing
- Missing Model element → Error, returns false

#### `FindTableElement(XDocument doc, string schema, string tableName)`
Locates the table in XML structure:
```xml
<Element Type="SqlTable" Name="[dbo].[Users]">
  ...
</Element>
```

**Search Strategy**: Find elements where:
- `Type` attribute = `"SqlTable"`
- `Name` attribute = `"[{schema}].[{tableName}]"`

**Enhanced Error Detection**:
- Logs warning if no `SqlTable` elements exist in DACPAC
- Helps identify format incompatibilities

**Return Value**: `XElement?` - Table element or null if not found

#### `ParseColumns(XElement tableElement)`
Extracts column definitions from table element.

**Supported Column Types**:
- `SqlSimpleColumn`: Standard table columns
- `SqlComputedColumn`: Computed/calculated columns

**XML Structure Handling** (two patterns supported):

**Pattern 1: SQL Server 2017+ DACPAC (Current Format)**
```xml
<Relationship Name="Columns">
  <Entry>
    <Element Type="SqlSimpleColumn" Name="[dbo].[Users].[UserId]">
      <Property Name="SqlDataType" Value="int" />
      <Property Name="IsNullable" Value="False" />
      ...
    </Element>
  </Entry>
</Relationship>
```

**Pattern 2: Legacy DACPAC (Pre-2017)**
```xml
<Relationship Name="Columns">
  <Entry>
    <References Name="[dbo].[Users].[UserId]" />
  </Entry>
</Relationship>
```

**Enhanced Error Detection**:
- Logs warning if no 'Columns' relationship found
- Handles both embedded and referenced column definitions
- Gracefully skips malformed entries

**Return Value**: `List<ColumnDefinition>` - All columns found in table

#### `ParseColumnProperties(XElement columnElement, string columnName)`
Extracts metadata from column's property elements.

**Property Value Extraction** (Enhanced):
Supports two XML patterns for property values:

1. **Value Attribute** (Simple values):
```xml
<Property Name="IsNullable" Value="False" />
```

2. **Value Element** (Complex values, CDATA):
```xml
<Property Name="DefaultExpressionScript">
  <Value><![CDATA[((0))]]></Value>
</Property>
```

**Property Mapping Table**:

| XML Property | Mapped To | Notes |
|--------------|-----------|-------|
| SqlDataType | SqlType | Base type (e.g., "nvarchar") - from direct properties |
| IsNullable | IsNullable | Boolean |
| Length | MaxLength | For string/binary types - from direct properties |
| IsIdentity | IsIdentity | AUTO_INCREMENT |
| Precision | Precision | For decimal types - from direct properties |
| Scale | Scale | For decimal types - from direct properties |

**TypeSpecifier Relationship** (SQL Server 2017+ Format):
The newer DACPAC format uses a nested `TypeSpecifier` element containing:

```xml
<Relationship Name="TypeSpecifier">
  <Entry>
    <Element Type="SqlTypeSpecifier">
      <Property Name="Length" Value="50" />
      <Property Name="Precision" Value="18" />
      <Property Name="Scale" Value="2" />
      <Relationship Name="Type">
        <Entry>
          <References Name="[nvarchar]" />
        </Entry>
      </Relationship>
    </Element>
  </Entry>
</Relationship>
```

**Type Information Priority**:
1. TypeSpecifier properties (newer format) override direct properties
2. Direct properties used as fallback (older format)
3. Default values used if neither available

**Type Construction**: Appends length/precision to type string:
- `nvarchar` + `Length=50` → `nvarchar(50)`
- `decimal` + `Precision=18, Scale=2` → `decimal(18,2)`

**Case-Insensitive Property Matching**: Property names compared without case sensitivity for robustness.

**Return Value**: `ColumnDefinition` with all available metadata populated

#### `FindColumnElement(XDocument doc, string columnFullName)`
**New in current version**: Enhanced to support multiple column types.

**Supported Column Types**:
- `SqlSimpleColumn`: Standard columns
- `SqlComputedColumn`: Computed columns

**Search Strategy**: Finds first element matching:
- `Type` attribute in supported types list
- `Name` attribute = columnFullName

**Return Value**: `XElement?` - Column element or null if not found

#### `ParsePrimaryKey(XDocument doc, string schema, string tableName)`
Identifies primary key columns.

**Algorithm**:
1. Find all `SqlPrimaryKeyConstraint` elements
2. Check if constraint belongs to target table (via `DefiningTable` relationship)
3. Extract column references from `ColumnSpecifications` relationship
4. Parse column names from references
5. Return set of PK column names

**XML Structure**:
```xml
<Element Type="SqlPrimaryKeyConstraint" Name="[dbo].[PK_Users]">
  <Relationship Name="DefiningTable">
    <Entry>
      <References Name="[dbo].[Users]" />
    </Entry>
  </Relationship>
  <Relationship Name="ColumnSpecifications">
    <Entry>
      <Element Type="SqlIndexedColumnSpecification">
        <Relationship Name="Column">
          <Entry>
            <References Name="[dbo].[Users].[UserId]" />
          </Entry>
        </Relationship>
      </Element>
    </Entry>
  </Relationship>
</Element>
```

**Composite Key Support**: Returns all columns if multiple PK columns exist.

**Note**: Primary key columns are marked in `ColumnDefinition.IsPrimaryKey` but do NOT generate `HasKey()` configurations, as entities inherit from `BaseEntity` which provides the actual primary key. Instead, indexes are created for PK columns to maintain uniqueness.

#### `ParseIndexes(XDocument doc, string schema, string tableName)`
**New in current version**: Extracts all indexes defined on the table.

**Algorithm**:
1. Find all `SqlIndex` elements
2. Check if index belongs to target table (via `IndexedObject` relationship)
3. Parse index properties (IsUnique, IsClustered)
4. Extract column references from `ColumnSpecifications` relationship (maintains order)
5. Parse column names from references
6. Return list of `IndexDefinition` objects

**XML Structure**:
```xml
<Element Type="SqlIndex" Name="[dbo].[IX_Users_Email]">
  <Property Name="IsUnique" Value="True" />
  <Property Name="IsClustered" Value="False" />
  <Relationship Name="IndexedObject">
    <Entry>
      <References Name="[dbo].[Users]" />
    </Entry>
  </Relationship>
  <Relationship Name="ColumnSpecifications">
    <Entry>
      <Element Type="SqlIndexedColumnSpecification">
        <Relationship Name="Column">
          <Entry>
            <References Name="[dbo].[Users].[Email]" />
          </Entry>
        </Relationship>
      </Element>
    </Entry>
  </Relationship>
</Element>
```

**Return Value**: `List<IndexDefinition>` - All indexes found for the table

**Post-Processing**: After parsing existing indexes, `ParseTable` ensures that primary key columns have a corresponding unique index. If no index exists covering the PK columns, one is auto-generated with `IsPrimaryKeyIndex = true`.

### PrimaryKeyEnricher

**Responsibility**: Validate and enrich table definitions with PK metadata.

**Key Methods**:

#### `EnrichTableWithPrimaryKeys(TableDefinition table)`
- Validates table has at least one column
- Identifies auto-added PK columns (not from Excel)
- Logs information about primary keys
- Returns validation success

**Validation Rules**:
- Must have ≥1 column
- PK columns logged if auto-added

**Purpose**: Ensures entities have proper PK configuration before code generation.

### EntityClassGenerator

**Responsibility**: Generate C# entity class source code.

**Key Methods**:

#### `GenerateEntityClass(TableDefinition table)`
Main code generation method.

**Generated Structure**:
```csharp
// 1. Using statements
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DataLayer.Core.Entities;

// 2. Namespace (dynamic)
namespace DataLayer.Core.Entities.{Server}.{Database}
{
    // 3. Comment with source
    // This entity was generated from:
    // [{Server}].[{Database}].[{Schema}].[{Table}]
    
    // 4. Table attribute
    [Table("{TableName}", Schema = "{Database}")]
    
    // 5. Class declaration
    public class {ClassName} : BaseEntity
    {
        // 6. Properties
    }
}
```

**Class Naming**:
1. Convert table name to PascalCase
2. Check for property name conflicts
3. If conflict: append "Entity" to class name

**Primary Key Column Handling**:
- Primary key columns from the database are included as regular properties
- No `[Key]` attribute is applied (BaseEntity provides the actual key)
- Unique indexes are generated for PK columns in `OnModelCreating` configuration

**Historical Note**: Previously, composite keys were configured using `HasKey()`. This has been replaced with unique index generation to support the BaseEntity surrogate key pattern.

#### `GenerateProperty(StringBuilder sb, ColumnDefinition column, bool forceRequired)`
Generates a single property with attributes.

**Attribute Generation Logic**:

1. **`[Column]`**: Always applied
   ```csharp
   [Column("{ColumnName}")]
   ```

2. **`[Required]`**: Applied if:
   - Column is not nullable, OR
   - `forceRequired` is true (for single PK columns)
   ```csharp
   [Required]
   ```

3. **`[MaxLength]`**: Applied for string types
   ```csharp
   [MaxLength({value})]
   ```
   Sources (in order of precedence):
   - `column.MaxLength` if set
   - Extracted from `SqlType` using regex

**Property Declaration Patterns**:

| Scenario | Generated Code |
|----------|---------------|
| Non-nullable string | `public required string Name { get; set; }` |
| Nullable string | `public string? Name { get; set; }` |
| Non-nullable value type | `public int Age { get; set; }` |
| Nullable value type | `public int? Age { get; set; }` |

**C# 11 Features Used**:
- `required` modifier for non-nullable reference types
- Nullable reference types (`string?`)

#### `ValidateEntityClass(TableDefinition table)`
Pre-generation validation.

**Checks**:
1. Class name is valid C# identifier
2. All property names are valid C# identifiers
3. Namespace is not empty

**Invalid Names**: Empty, whitespace-only, or only underscore

#### `GenerateOnModelCreatingBody(List<TableDefinition> tables)`
Generates EF Core configuration code.

**Generated Code Patterns**:

**Entity Registration** (All entities):
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>();
```

**Note**: No `HasKey()` configuration is generated. Entities inherit from `BaseEntity` which provides the actual primary key (typically an `Id` property). The original database primary key columns are configured as unique indexes instead.

**Single Column Index**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .HasIndex(e => e.Email)
    .IsUnique()
    .HasDatabaseName("IX_Users_Email");
```

**Composite Index**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .HasIndex(e => new { e.FirstName, e.LastName })
    .IsUnique()
    .HasDatabaseName("IX_Users_FirstName_LastName");
```

**Non-Unique Index**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .HasIndex(e => e.Status)
    .HasDatabaseName("IX_Orders_Status");
```

**Primary Key as Unique Index** (Auto-generated when PK columns detected):
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .HasIndex(e => new { e.OrderId, e.LineNumber })
    .IsUnique()
    .HasDatabaseName("IX_OrderDetails_OrderId_LineNumber");
```

**Decimal Precision**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .Property(e => e.Amount)
    .HasColumnType("decimal(18,2)");
```

**Default Value**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .Property(e => e.IsActive)
    .HasDefaultValueSql("1");
```

**Combined Configuration** (Decimal with Default):
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .Property(e => e.Amount)
    .HasColumnType("decimal(18,2)")
    .HasDefaultValueSql("0");
```

**Purpose**: Written to `_output/Configuration/{Server}/{Database}/{Database}EntityConfiguration.cs` and called from `SQLDbContext.OnModelCreating`.

### FileWriterService

**Responsibility**: Write generated entity classes, views, configuration files, and the DbContext file to the file system.

**Key Methods**:

#### `WriteEntityFile(...)`
Writes a single entity class file.

**File Path Structure**:
```
{outputDirectory}/{Server}/{Database}/{ClassName}.cs
```

**Example**:
- Input: `ProductionServer`, `CustomerDB`, `user_accounts`
- Output: `_output/ProductionServer/CustomerDB/UserAccounts.cs`

#### `WriteViewFile(...)`
Writes a view entity class file.

**File Path Structure**:
```
{outputDirectory}/{Server}/{Database}/Views/{ViewClassName}.cs
```

#### `WriteConfigurationFile(...)`
Writes a per-database EF Core configuration class.

**File Path Structure**:
```
{outputDirectory}/Configuration/{Server}/{Database}/{Database}EntityConfiguration.cs
```

#### `WriteDbContextFile(string outputDirectory, string dbContextCode)`
Writes the generated SQLDbContext class.

**File Path**:
```
{outputDirectory}/SQLDbContext.cs
```

#### `EnsureOutputDirectoryExists(string outputDirectory)`
Creates root output directory if missing.

#### `CleanOutputDirectory(string outputDirectory, bool force)`
Deletes all files in output directory if `force` is true.

**Note**: Currently not used in main workflow (output directory is manually purged in Program.cs).

### ReportWriterService

**Responsibility**: Write element discovery reports in JSON and HTML formats.

**Key Methods**:

#### `WriteJsonReport(string outputDirectory, List<ElementDiscoveryReport> reports)`
Writes one JSON file per server/database to `_output/DiscoveryReports/`.

**File Path Structure**:
```
{outputDirectory}/DiscoveryReports/{Server}_{Database}_Discovery.json
```

#### `WriteHtmlReport(string outputDirectory, List<ElementDiscoveryReport> reports)`
Writes one styled HTML file per server/database.

**File Path Structure**:
```
{outputDirectory}/DiscoveryReports/{Server}_{Database}_Discovery.html
```

**HTML Features**:
- Responsive grid summary cards
- Tables for stored procedures, sequences, triggers, extended properties
- Full element type count table
- Inline CSS (single-file output)

#### `WriteIndexHtml(string outputDirectory, List<ElementDiscoveryReport> reports)`
Writes an index page linking all individual discovery reports.

**File Path**:
```
{outputDirectory}/DiscoveryReports/index.html
```

### DbContextGenerator

**Responsibility**: Generate the `SQLDbContext` class with `DbSet` properties for all entities and views, plus `OnModelCreating` calls to all configuration classes.

**Key Method**:

#### `GenerateSQLDbContext(List<TableDefinition> allTables, List<ViewDefinition> allViews, List<(string Server, string Database)> serverDatabasePairs)`

**Generated File**: `_output/SQLDbContext.cs`

**Generated Class Structure**:
```csharp
public partial class SQLDbContext(DbContextOptions<SQLDbContext> options) : BaseDbContext(options)
{
    // Table Entity DbSets grouped by server/database
    public DbSet<Server1.Database1.Users> Server1Database1Users { get; set; }
    ...

    // View Entity DbSets grouped by server/database
    public DbSet<Server1.Database1.VwActiveUsers> Server1Database1VwActiveUsers { get; set; }
    ...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        Database1EntityConfiguration.Configure(modelBuilder);
        ...
    }
}
```

**DbSet Naming**: PascalCase entity name. If the same entity name appears in multiple databases, the name is prefixed with `{Server}{Database}` to avoid conflicts.

## Utilities

### ConsoleLogger

**Responsibility**: Colored, categorized console output.

**Log Levels**:

| Method | Color | Prefix | Use Case |
|--------|-------|--------|----------|
| `LogInfo` | White | [INFO] | General information |
| `LogProgress` | Green | [SUCCESS] | Successful operations |
| `LogWarning` | Yellow | [WARNING] | Non-critical issues |
| `LogError` | Red | [ERROR] | Critical failures |

**Implementation**:
```csharp
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"[SUCCESS] {message}");
Console.ResetColor();
```

**Purpose**: Provides visual feedback and aids in debugging by categorizing output.

### SqlTypeMapper

**Responsibility**: Map SQL Server data types to C# types.

**Type Mapping Table**:

| SQL Type | C# Type | Notes |
|----------|---------|-------|
| bit | bool | |
| tinyint | byte | |
| smallint | short | |
| int | int | |
| bigint | long | |
| decimal, numeric, money, smallmoney | decimal | Precision/scale preserved |
| float | double | |
| real | float | |
| char, nchar, varchar, nvarchar, text, ntext | string | MaxLength extracted |
| date | DateOnly | .NET 6+ |
| time | TimeOnly | .NET 6+ |
| datetime, datetime2, smalldatetime | DateTime | |
| datetimeoffset | DateTimeOffset | |
| uniqueidentifier | Guid | |
| binary, varbinary, image | byte[] | |
| xml | string | Stored as string |

**Key Methods**:

#### `MapToCSharpType(string sqlType, bool isNullable, out bool needsMaxLength)`
- Strips parameters from SQL type: `varchar(50)` → `varchar`
- Looks up in type map
- Appends `?` for nullable value types
- Sets `needsMaxLength` flag for string types with defined length

**Nullable Logic**:
```csharp
if (isNullable && IsValueType(csharpType))
    return csharpType + "?";
```

#### `ExtractMaxLength(string sqlType)`
Uses regex to extract length from type definition:
```csharp
Regex.Match(sqlType, @"\((\d+)\)")
```

Examples:
- `varchar(50)` → `50`
- `nvarchar(MAX)` → `null`
- `int` → `null`

#### `IsValueType(string csharpType)`
Checks if type requires `?` for nullability (vs. reference types that use `?` differently).

**Value Types**: All numeric types, DateTime variants, Guid

**Reference Types**: string, byte[]

### NameConverter

**Responsibility**: Convert SQL naming conventions to C# naming conventions.

**Key Methods**:

#### `ToPascalCase(string input)`
Converts SQL names to C# PascalCase.

**Algorithm**:
1. Split on delimiters: `_`, `-`, ` `
2. Capitalize first letter of each part
3. Preserve existing casing of remaining letters
4. Join parts
5. Sanitize result

**Examples**:
- `user_accounts` → `UserAccounts`
- `order-items` → `OrderItems`
- `Product Details` → `ProductDetails`

#### `SanitizeIdentifier(string input)`
Ensures valid C# identifier.

**Rules**:
1. Remove non-word characters (keep `a-z`, `A-Z`, `0-9`, `_`)
2. If starts with digit, prefix with `_`
3. If matches C# keyword, prefix with `@`
4. If empty after sanitization, use `_`

**C# Keywords Handled**: Complete list of 77 C# keywords checked:
- `abstract`, `as`, `base`, `bool`, `break`, `byte`, `case`, `catch`, `char`, `checked`, `class`, `const`, `continue`, `decimal`, `default`, `delegate`, `do`, `double`, `else`, `enum`, `event`, `explicit`, `extern`, `false`, `finally`, `fixed`, `float`, `for`, `foreach`, `goto`, `if`, `implicit`, `in`, `int`, `interface`, `internal`, `is`, `lock`, `long`, `namespace`, `new`, `null`, `object`, `operator`, `out`, `override`, `params`, `private`, `protected`, `public`, `readonly`, `ref`, `return`, `sbyte`, `sealed`, `short`, `sizeof`, `stackalloc`, `static`, `string`, `struct`, `switch`, `this`, `throw`, `true`, `try`, `typeof`, `uint`, `ulong`, `unchecked`, `unsafe`, `ushort`, `using`, `virtual`, `void`, `volatile`, `while`

**Examples**:
- `class` → `@class`
- `123Table` → `_123Table`
- `user@account` → `useraccount`

## Processing Pipeline

### Main Execution Flow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Initialize                                                │
│    - Determine workspace root                                │
│    - Set input/output directories                            │
│    - Purge output directory                                  │
│    - Initialize all services                                 │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 2. Load Excel Data                                           │
│    - Find Excel file in input directory                      │
│    - Read and parse all rows                                 │
│    - Apply filter criteria                                   │
│    - Group by Server and Database                            │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 3. Process Each Server/Database Combination                  │
│    ┌─────────────────────────────────────────┐              │
│    │ 3a. Validate DACPAC exists              │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3b. Extract model.xml from DACPAC       │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3c. Generate ElementDiscoveryReport     │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3d. Parse & generate all views          │              │
│    │     - ParseViews() → ViewDefinition[]  │              │
│    │     - GenerateViewClass() per view      │              │
│    │     - WriteViewFile() per view          │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3e. Group Excel rows by Schema/Table    │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3f. For Each Table:                     │              │
│    │     - ParseTable() from model.xml       │              │
│    │     - EnrichTableWithPrimaryKeys()      │              │
│    │     - ValidateEntityClass()             │              │
│    │     - GenerateEntityClass()             │              │
│    │     - WriteEntityFile()                 │              │
│    │     - Collect TableDefinition           │              │
│    └──────┬──────────────────────────────────┘              │
└───────────┼──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 4. Generate Per-Database Configuration Classes                │
│    - GenerateEntityConfiguration() per server/database        │
│    - Append GenerateViewConfiguration() for views             │
│    - WriteConfigurationFile() per server/database             │
└───────────┬──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 5. Generate SQLDbContext                                      │
│    - GenerateSQLDbContext() → all DbSets + OnModelCreating   │
│    - WriteDbContextFile()                                     │
└───────────┬──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 6. Write Discovery Reports                                    │
│    - WriteJsonReport() per server/database                    │
│    - WriteHtmlReport() per server/database                    │
│    - WriteIndexHtml()                                         │
└───────────┬──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 7. Display Summary                                            │
│    - Entities generated                                       │
│    - Views generated                                          │
│    - Tables skipped                                           │
│    - Errors encountered                                       │
└───────────────────────────────────────────────────────────────┘
```

### Detailed Step Descriptions

#### Step 1: Initialize

**Directory Resolution**:
```csharp
var currentDir = Directory.GetCurrentDirectory();
var workspaceRoot = currentDir;

// If running from bin directory, navigate up to workspace root
if (currentDir.Contains("bin"))
{
    workspaceRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
}

var projectDirectory = Path.Combine(workspaceRoot, "src\\_DacpacEntityGenerator");
var inputDirectory = Path.Combine(projectDirectory, "_input");
var outputDirectory = Path.Combine(projectDirectory, "_output");
```

**Note**: Current implementation has hardcoded directory name `src\_DacpacEntityGenerator`; the actual project directory is `src/DacpacEntityGenerator`. This path works when the executable is launched from the workspace root.

**Output Directory Purge**:
- Deletes all files
- Recursively deletes all subdirectories
- Ensures clean slate for each run

#### Step 2: Load Excel Data

**Excel Processing**:
1. Scan for `.xlsx` files
2. Select first file (warn if multiple)
3. Open first worksheet
4. Validate column headers
5. Parse each row
6. Apply filter: `(TableInDaoAnalysis OR AddedByAPI) AND PersistenceType = 'R'`
7. Log statistics

**Grouping**:
```csharp
Dictionary<Server, Dictionary<Database, List<ExcelRow>>>
```

**Purpose**: Enables batch processing by database while ensuring DACPAC is extracted only once per database.

#### Step 3: Process Each Server/Database

**For Each Server → For Each Database**:
    1. Check DACPAC file exists
    2. Extract `model.xml`
    3. Generate `ElementDiscoveryReport` (stored procedures, sequences, triggers, etc.)
    4. Parse and generate all **view entities** from model.xml
    5. Group Excel rows by `Schema` + `Table`
    6. **For Each Table**:
       - Get list of required columns from Excel
       - Parse table definition from XML (including FK/check/unique constraints)
       - Auto-add primary key columns not in Excel
       - Validate table definition
       - Generate entity class code
       - Write to file system
       - Add to collection for configuration/DbContext generation

**Error Handling**:
- Missing DACPAC: Log error, increment error count, skip database
- Parse failure: Log error, increment skip count, continue to next table
- Validation failure: Log error, increment skip and error counts, continue

#### Step 4: Generate Per-Database Configuration Classes

**Process** (per server/database combination):
1. Call `GenerateEntityConfiguration()` for all tables
2. Append `GenerateViewConfiguration()` output for any views
3. Write complete `{Database}EntityConfiguration.cs` via `WriteConfigurationFile()`

#### Step 5: Generate SQLDbContext

**Process**:
1. Call `GenerateSQLDbContext()` with all tables, views, and server/database pairs
2. Write `SQLDbContext.cs` via `WriteDbContextFile()`

#### Step 6: Write Discovery Reports

**Process** (once, after all databases are processed):
1. `WriteJsonReport()` → one `.json` per server/database in `DiscoveryReports/`
2. `WriteHtmlReport()` → one `.html` per server/database in `DiscoveryReports/`
3. `WriteIndexHtml()` → `DiscoveryReports/index.html`

#### Step 7: Display Summary

**Console Output**:
```
=== Generation Summary ===
[SUCCESS] Entities generated: 42
[SUCCESS] Views generated: 5
[WARNING] Tables skipped: 3
[ERROR] Errors encountered: 1

[SUCCESS] Entity generation completed!
```

## Code Generation Strategy

### Entity Class Template

The generator uses a StringBuilder-based template approach:

```csharp
var sb = new StringBuilder();

// 1. Usings
sb.AppendLine("using System;");
sb.AppendLine("using System.ComponentModel.DataAnnotations;");
sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
sb.AppendLine("using DataLayer.Core.Entities;");
sb.AppendLine();

// 2. Namespace
sb.AppendLine($"namespace {namespaceValue}");
sb.AppendLine("{");

// 3. Comment
sb.AppendLine("    // This entity was generated from:");
sb.AppendLine($"    // [{server}].[{database}].[{schema}].[{table}]");

// 4. Attributes
sb.AppendLine($"    [Table(\"{tableName}\", Schema = \"{database}\")]");

// 5. Class
sb.AppendLine($"    public class {className} : BaseEntity");
sb.AppendLine("    {");

// 6. Properties
foreach (var column in table.Columns)
{
    GenerateProperty(sb, column, ...);
}

// 7. Close
sb.AppendLine("    }");
sb.AppendLine("}");

return sb.ToString();
```

### Naming Strategy

**Namespace**:
```
DataLayer.Core.Entities.{PascalCaseServer}.{PascalCaseDatabase}
```

**Class Name**:
```
{PascalCaseTableName}[Entity]
```
(Appends "Entity" if property name conflict exists)

**Property Names**:
```
{PascalCaseColumnName}
```

### Attribute Strategy

**Always Applied**:
- `[Column]`: Maps to database column name (preserves original casing)

**Conditionally Applied**:
- `[Table]`: Applied to class (always)
- `[Required]`: Applied if `!IsNullable` AND no default value AND not computed
- `[MaxLength]`: Applied for string types with defined length
- `[Timestamp]`: Applied for row version / concurrency token columns
- `[DatabaseGenerated(DatabaseGeneratedOption.Computed)]`: Applied for computed columns

**Not Applied**:
- `[Key]`: Omitted in favor of EF Core configuration (`BaseEntity` provides the key)
- `[StringLength]`: Using `[MaxLength]` instead

### Type Nullability Handling

**C# 11 Approach**:
```csharp
// Non-nullable string (required property)
public required string Name { get; set; }

// Nullable string
public string? Name { get; set; }

// Non-nullable value type
public int Age { get; set; }

// Nullable value type
public int? Age { get; set; }
```

**Logic**:
1. For strings:
   - If not nullable: use `required` modifier (forces initialization)
   - If nullable: use `string?`
2. For value types:
   - If nullable: append `?` to type
   - If not nullable: use bare type

## File I/O Operations

### Input File Locations

**Excel File**:
- Path: `{workspace}/src/DacpacEntityGenerator/_input/*.xlsx`
- First `.xlsx` file found is used
- Multiple files generate warning

**DACPAC Files**:
- Path: `{workspace}/src/DacpacEntityGenerator/_input/dacpacs/{Server}_{Database}.dacpac`
- Naming convention is strict
- Missing files cause database to be skipped

### Output File Locations

**Entity Classes**:
- Path: `_output/{Server}/{Database}/{ClassName}.cs`

**View Entity Classes**:
- Path: `_output/{Server}/{Database}/Views/{ViewClassName}.cs`

**EF Core Configuration Classes**:
- Path: `_output/Configuration/{Server}/{Database}/{Database}EntityConfiguration.cs`

**SQLDbContext**:
- Path: `_output/SQLDbContext.cs`

**Discovery Reports**:
- Path: `_output/DiscoveryReports/{Server}_{Database}_Discovery.json`
- Path: `_output/DiscoveryReports/{Server}_{Database}_Discovery.html`
- Path: `_output/DiscoveryReports/index.html`

### File System Operations

**Read Operations**:
- Excel: ClosedXML library (in-memory)
- DACPAC: `ZipFile.OpenRead()` (streaming)
- No file locking issues (read-only)

**Write Operations**:
- All writes use `File.WriteAllText()`
- UTF-8 encoding specified explicitly
- Overwrites existing files without warning
- Directories created recursively as needed

## Error Handling

### Error Categories

1. **Fatal Errors** (exit immediately):
   - No Excel file found
   - Invalid Excel format

2. **Database Errors** (skip database, continue):
   - DACPAC file not found
   - Corrupted DACPAC file
   - model.xml extraction failed

3. **Table Errors** (skip table, continue):
   - Table not found in DACPAC
   - No columns after filtering
   - Invalid class/property names
   - File write failure

4. **Warnings** (logged, continue):
   - Column in Excel not found in table
   - Table has no primary key
   - Primary keys auto-added (not in Excel)

### Logging Strategy

**Context Prefix**: Most log messages include context:
```
[{Server}].[{Database}].[{Schema}].[{TableName}]
```

**Examples**:
```
[ProductionServer].[CustomerDB].[dbo].[Users] - Parsing table
[ProductionServer].[CustomerDB].[dbo].[Users] - Table has 5 columns (1 PK, 4 from Excel)
[ProductionServer].[CustomerDB].[dbo].[Orders] - Column from Excel not found in table: LegacyColumn
```

### Exception Handling

**Pattern Used**:
```csharp
try
{
    // Risky operation
}
catch (SpecificException ex)
{
    ConsoleLogger.LogError($"Context - Error: {ex.Message}");
    return null; // or false, or increment error counter
}
catch (Exception ex)
{
    ConsoleLogger.LogError($"Context - Unexpected error: {ex.Message}");
    return null;
}
```

**No Global Exception Handler**: `Program.cs` has a top-level try-catch that logs unhandled exceptions.

### Result Tracking

`GenerationResult` object accumulates statistics:
- `EntitiesGenerated`: Successful file writes
- `TablesSkipped`: Tables that couldn't be processed
- `ErrorsEncountered`: Critical errors

**Display at End**:
```
=== Generation Summary ===
[SUCCESS] Entities generated: 42
[WARNING] Tables skipped: 3
[ERROR] Errors encountered: 1
```

## Configuration

### Hardcoded Configuration

**Project Path**:
```csharp
var projectDirectory = Path.Combine(workspaceRoot, "src\\_DacpacEntityGenerator");
```
**Issue**: Hardcoded directory name does not match the actual project directory (`src/DacpacEntityGenerator`). Run the application from the workspace root for paths to resolve correctly.

**Namespace Prefix**:
```csharp
"DataLayer.Core.Entities.{Server}.{Database}"
```

**Base Class**:
```csharp
public class {ClassName} : BaseEntity
```
**Note**: `BaseEntity` must be defined in `DataLayer.Core.Entities` namespace.

**Table Schema Attribute**:
```csharp
[Table("{TableName}", Schema = "{Database}")]
```
**Note**: Uses Database name as schema (known limitation — uses Database instead of actual SQL schema `dbo`).

### Configurable Behavior

**Excel Filter Criteria**:
Defined in `ExcelReaderService.ReadAndFilterExcel()`:
```csharp
var filteredRows = allRows
    .Where(r => (r.TableInDaoAnalysis || r.AddedByAPI) &&
               r.PersistenceType.Equals("R", StringComparison.OrdinalIgnoreCase))
    .ToList();
```

**Output Directory Purging**:
Currently hardcoded in `Program.cs` but could be made optional:
```csharp
if (Directory.Exists(outputDirectory))
{
    foreach (var file in Directory.GetFiles(outputDirectory))
        File.Delete(file);
    foreach (var dir in Directory.GetDirectories(outputDirectory))
        Directory.Delete(dir, true);
}
```

### Future Configuration Opportunities

1. **appsettings.json**:
   - Input/output paths
   - Namespace templates
   - Base class name
   - Filter criteria

2. **Command-Line Arguments**:
   - Server/database selection
   - Output directory override
   - Verbosity level

3. **Excel Configuration Sheet**:
   - Generation options
   - Custom type mappings
   - Naming convention overrides

## Extension Points

### Adding New SQL Types

Edit `SqlTypeMapper.TypeMap`:
```csharp
private static readonly Dictionary<string, string> TypeMap = new()
{
    // ... existing mappings
    { "geography", "NetTopologySuite.Geometries.Geometry" },
    { "hierarchyid", "Microsoft.SqlServer.Types.SqlHierarchyId" }
};
```

### Custom Property Attributes

Modify `EntityClassGenerator.GenerateProperty()`:
```csharp
// Add custom attribute based on column metadata
if (column.Name.EndsWith("Json"))
{
    sb.AppendLine("        [JsonProperty]");
}
```

### Alternative Base Classes

Modify `EntityClassGenerator.GenerateEntityClass()`:
```csharp
var baseClass = table.Columns.Any(c => c.Name == "IsDeleted") 
    ? "SoftDeletableEntity" 
    : "BaseEntity";
sb.AppendLine($"    public class {className} : {baseClass}");
```

### Custom Naming Conventions

Create configuration-driven naming:
```csharp
public interface INamingStrategy
{
    string ConvertClassName(string tableName);
    string ConvertPropertyName(string columnName);
    string ConvertNamespace(string server, string database);
}
```

## Known Limitations

1. **Hardcoded Path**: Project directory name is hardcoded as `src\_DacpacEntityGenerator`; the project actually lives at `src/DacpacEntityGenerator`
2. **Schema Attribute Bug**: `[Table]` attribute uses `Database` name as schema (`Schema = "{Database}"`), not the actual SQL schema (`dbo`, etc.)
3. **No Navigation Properties**: Foreign keys are parsed and stored in the model but navigation properties are not generated in entity classes
4. **Single Excel Support**: Only the first `.xlsx` file found in `_input/` is processed
5. **No Rollback**: Partial failures leave partial output in `_output/`
6. **No Incremental Generation**: Always regenerates all entities (full output purge on each run)
7. **BaseEntity Assumption**: Assumes `BaseEntity` class exists in `DataLayer.Core.Entities` namespace in the consuming project
8. **DbContext Base Assumption**: Assumes `BaseDbContext` exists in `DataLayer.Infrastructure.Persistence.Contexts.Base`

## Performance Characteristics

### Memory Usage

- **Excel Files**: Loaded entirely into memory (ClosedXML requirement)
- **DACPAC Files**: `model.xml` loaded entirely into memory
- **Code Generation**: Uses `StringBuilder` for efficient string building
- **Batch Size**: No pagination; all rows processed at once

**Estimated Memory**:
- Small project (10 tables, 100 columns): ~50 MB
- Large project (500 tables, 5000 columns): ~500 MB

### Processing Time

**Bottlenecks**:
1. Excel parsing (I/O bound)
2. XML parsing (CPU bound)
3. File writing (I/O bound)

**Typical Performance**:
- 10 tables: ~2 seconds
- 100 tables: ~10 seconds
- 1000 tables: ~90 seconds

**Optimization Opportunities**:
- Parallel processing by database
- Streaming XML parsing
- Batch file writing

### Scalability

**Current Limits**:
- **Excel rows**: ClosedXML can handle ~1M rows (practical limit ~100K)
- **DACPAC size**: Limited by available memory (practical limit ~500 MB)
- **Entities**: No practical limit on number of generated files

**Scaling Strategy**:
- Split large Excel files by server/database
- Process multiple runs for different server groups
- Use faster XML parser (e.g., XmlReader) for huge DACPACs

## Testing Strategy

### Unit Testing Opportunities

1. **SqlTypeMapper**:
   - Test all type mappings
   - Test nullable handling
   - Test max length extraction

2. **NameConverter**:
   - Test PascalCase conversion
   - Test identifier sanitization
   - Test keyword handling

3. **EntityClassGenerator**:
   - Test single PK generation
   - Test composite PK generation
   - Test nullable property generation
   - Test attribute generation

### Integration Testing Opportunities

1. **End-to-End**:
   - Provide sample Excel + DACPAC
   - Verify generated entity matches expected output
   - Verify generated entities compile

2. **Error Scenarios**:
   - Missing DACPAC
   - Malformed Excel
   - Invalid XML in DACPAC
   - Duplicate table names

### Test Data Requirements

- Sample Excel file with known data
- Sample DACPAC files (various schemas)
- Expected entity outputs for comparison

## Dependencies and Versions

### NuGet Packages

```xml
<PackageReference Include="ClosedXML" Version="0.105.0" />
```

**ClosedXML**: Provides Excel reading/writing capabilities
- Mature library, actively maintained
- Depends on DocumentFormat.OpenXml
- Memory-intensive for large files

### Framework Dependencies

- **.NET 8.0**: Uses latest C# language features
- **System.IO.Compression**: Built-in ZIP handling
- **System.Xml.Linq**: LINQ to XML for schema parsing

### External Assumptions

- **BaseEntity**: Must exist in `DataLayer.Core.Entities` namespace
- Entity classes are assumed to be used with Entity Framework Core
- Database follows standard SQL Server conventions

## Maintenance Considerations

### Code Extensibility

**Well-Abstracted**:
- Service layer is modular
- Easy to add new type mappings
- Naming conversion is centralized

**Tightly Coupled**:
- Hardcoded namespace structure
- Hardcoded base class
- Schema assumption in Table attribute

### Documentation Needs

- XML documentation comments (currently absent)
- Sample usage examples
- Configuration guide
- Troubleshooting guide

### Future Enhancements

1. **Configuration File**: Support appsettings.json
2. **CLI Arguments**: Make paths configurable
3. **Relationship Detection**: Parse foreign keys
4. **Navigation Properties**: Generate related entity properties
5. **Incremental Generation**: Only regenerate changed entities
6. **Custom Templates**: Support Razor/T4 templates
7. **Multiple Excel Support**: Merge multiple Excel files
8. **Parallel Processing**: Speed up large projects
9. **Validation**: Validate generated code compiles
10. **Schema Correction**: Fix Schema attribute bug

---

## Conclusion

The DACPAC Entity Generator is a focused code generation tool that bridges SQL Server databases and C# entity classes. Its strength lies in its simplicity and directness: it does one thing (generate entities from DACPAC files) and does it well.

The architecture is clean and modular, making it relatively easy to extend and maintain. The primary areas for improvement are configuration flexibility and relationship handling.

This tool is best suited for projects that need to quickly generate entity classes from existing databases and that follow the expected conventions (namespace structure, base class, etc.).
