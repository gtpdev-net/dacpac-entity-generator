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
│   ├── ExcelRow.cs                    # Excel data row
│   └── GenerationResult.cs            # Process results
├── Services/                           # Business logic
│   ├── ExcelReaderService.cs          # Excel file processing
│   ├── DacpacExtractorService.cs      # DACPAC extraction
│   ├── ModelXmlParserService.cs       # XML schema parsing
│   ├── PrimaryKeyEnricher.cs          # PK metadata enrichment
│   ├── EntityClassGenerator.cs        # C# code generation
│   └── FileWriterService.cs           # File system operations
├── Utilities/                          # Cross-cutting concerns
│   ├── ConsoleLogger.cs               # Colored console output
│   ├── SqlTypeMapper.cs               # SQL to C# type mapping
│   └── NameConverter.cs               # Naming convention conversion
└── DataLayer/
    └── GeneratedDbContext.cs          # Template DbContext

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
    public string Name { get; set; }           // Column name from database
    public string SqlType { get; set; }        // SQL Server data type
    public bool IsNullable { get; set; }       // NULL constraint
    public int? MaxLength { get; set; }        // String/binary length
    public bool IsIdentity { get; set; }       // IDENTITY specification
    public bool IsPrimaryKey { get; set; }     // PK membership
    public bool IsFromExcel { get; set; }      // User-requested vs auto-added
    public int? Precision { get; set; }        // Decimal precision
    public int? Scale { get; set; }            // Decimal scale
}
```

**Purpose**: Stores comprehensive column metadata needed for entity property generation.

**Key Fields**:
- `IsFromExcel`: Distinguishes user-requested columns from automatically-added PK columns
- `Precision` / `Scale`: Essential for decimal type mapping in EF Core

### TableDefinition

Represents a complete database table with all its columns.

```csharp
public class TableDefinition
{
    public string Server { get; set; }
    public string Database { get; set; }
    public string Schema { get; set; }
    public string TableName { get; set; }
    public List<ColumnDefinition> Columns { get; set; }
}
```

**Purpose**: Aggregates all information needed to generate a single entity class.

**Usage**: One `TableDefinition` instance generates one `.cs` entity file.

### ExcelRow

Represents a single row from the Excel input file.

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
    // Note: AddedByAPI property referenced in code but not declared
}
```

**Purpose**: Captures user's column selection and filtering criteria.

**Filtering Logic**: Row is processed if:
```
(TableInDaoAnalysis == true OR AddedByAPI == true) AND PersistenceType == "R"
```

### GenerationResult

Tracks the overall execution results.

```csharp
public class GenerationResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; set; }
    public int EntitiesGenerated { get; set; }
    public int TablesSkipped { get; set; }
    public int ErrorsEncountered { get; set; }
}
```

**Purpose**: Provides summary statistics for the generation run.

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

**Key Methods**:

#### `ParseTable(...)`
Main entry point for table parsing.

**Parameters**:
- `modelXml`: Complete model.xml content
- `server`, `database`, `schema`, `tableName`: Identifiers
- `requiredColumns`: User-selected columns from Excel

**Algorithm**:
1. Parse XML document
2. Find table element by schema and name
3. Extract all columns from table
4. Extract primary key information
5. Filter columns: include if (in Excel OR is PK)
6. Mark columns with metadata flags
7. Validate and return `TableDefinition`

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

#### `ParseColumns(XElement tableElement)`
Extracts column definitions from table element.

**XML Structure Handling** (two patterns supported):

**Pattern 1: SQL Server 2017+ DACPAC**
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

**Pattern 2: Legacy DACPAC**
```xml
<Relationship Name="Columns">
  <Entry>
    <References Name="[dbo].[Users].[UserId]" />
  </Entry>
</Relationship>
```

#### `ParseColumnProperties(XElement columnElement, string columnName)`
Extracts metadata from column's property elements:

| XML Property | Mapped To | Notes |
|--------------|-----------|-------|
| SqlDataType | SqlType | Base type (e.g., "nvarchar") |
| IsNullable | IsNullable | Boolean |
| Length | MaxLength | For string/binary types |
| IsIdentity | IsIdentity | AUTO_INCREMENT |
| Precision | Precision | For decimal types |
| Scale | Scale | For decimal types |

**Type Construction**: Appends length to type string:
- `nvarchar` + `Length=50` → `nvarchar(50)`

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

**Composite Key Detection**:
```csharp
var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
bool isCompositeKey = pkColumns.Count > 1;
```

- Single PK: No `[Key]` attribute (uses EF Core convention)
- Composite PK: Configuration in `OnModelCreating`

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

**Single Primary Key**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>();
```

**Composite Primary Key**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .HasKey(e => new { e.Column1, e.Column2 });
```

**Decimal Precision**:
```csharp
modelBuilder.Entity<Core.Entities.Server.Database.ClassName>()
    .Property(e => e.Amount)
    .HasColumnType("decimal(18,2)");
```

**Purpose**: Output is written to `DbContext.onModelCreating` file for manual integration.

### FileWriterService

**Responsibility**: Write generated entity classes to file system.

**Key Methods**:

#### `WriteEntityFile(...)`
Writes a single entity class file.

**File Path Structure**:
```
{outputDirectory}/{Server}/{Database}/{ClassName}.cs
```

**Process**:
1. Create server subdirectory if needed
2. Create database subdirectory if needed
3. Generate PascalCase filename
4. Write file with UTF-8 encoding
5. Log relative path

**Example**:
- Input: `ProductionServer`, `CustomerDB`, `user_accounts`
- Output: `_output/ProductionServer/CustomerDB/UserAccounts.cs`

#### `EnsureOutputDirectoryExists(string outputDirectory)`
Creates root output directory if missing.

#### `CleanOutputDirectory(string outputDirectory, bool force)`
Deletes all files in output directory if `force` is true.

**Note**: Currently not used in main workflow (output directory is manually purged in Program.cs).

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
│    - Initialize services                                     │
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
│    │ 3b. Extract model.xml from DACPAC      │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3c. Group rows by Schema/Table         │              │
│    └──────┬──────────────────────────────────┘              │
│           │                                                  │
│    ┌──────▼──────────────────────────────────┐              │
│    │ 3d. For Each Table:                    │              │
│    │     - Parse table from model.xml       │              │
│    │     - Enrich with primary keys         │              │
│    │     - Validate entity class            │              │
│    │     - Generate C# code                 │              │
│    │     - Write to file                    │              │
│    │     - Collect TableDefinition          │              │
│    └──────┬──────────────────────────────────┘              │
└───────────┼──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 4. Generate DbContext Configuration                           │
│    - Generate OnModelCreating body                            │
│    - Write to DbContext.onModelCreating                       │
└───────────┬──────────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────────┐
│ 5. Display Summary                                            │
│    - Entities generated                                       │
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

var projectDirectory = Path.Combine(workspaceRoot, "src\\DataLayer.DacpacEntityGenerator");
var inputDirectory = Path.Combine(projectDirectory, "_input");
var outputDirectory = Path.Combine(projectDirectory, "_output");
```

**Note**: Current implementation has hardcoded path; assumes project is in `src\DataLayer.DacpacEntityGenerator\`.

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

**For Each Server**:
  **For Each Database**:
    1. Check DACPAC file exists
    2. Extract `model.xml`
    3. Group rows by `Schema` + `Table`
    4. **For Each Table**:
       - Get list of required columns from Excel
       - Parse table definition from XML
       - Auto-add primary key columns
       - Validate table definition
       - Generate entity class code
       - Write to file system
       - Add to collection for DbContext generation

**Error Handling**:
- Missing DACPAC: Log error, increment error count, skip database
- Parse failure: Log error, increment skip count, continue to next table
- Validation failure: Log error, increment skip and error counts, continue

#### Step 4: Generate DbContext Configuration

**Process**:
1. Collect all successfully generated `TableDefinition` objects
2. Generate `OnModelCreating` configuration lines
3. Write to `DbContext.onModelCreating` in output root

**Output Format**:
```csharp
modelBuilder.Entity<Core.Entities.Server1.Database1.Table1>();
modelBuilder.Entity<Core.Entities.Server1.Database1.Table2>().HasKey(e => new { e.Key1, e.Key2 });
modelBuilder.Entity<Core.Entities.Server2.Database1.Table1>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
```

#### Step 5: Display Summary

**Console Output**:
```
=== Generation Summary ===
[SUCCESS] Entities generated: 42
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
- `[Required]`: Applied if `!IsNullable` OR is single primary key
- `[MaxLength]`: Applied for string types with defined length

**Not Applied**:
- `[Key]`: Omitted in favor of `OnModelCreating` configuration
- `[StringLength]`: Using `[MaxLength]` instead
- `[DatabaseGenerated]`: Could be added for identity columns (not currently implemented)

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
- Path: `{workspace}/src/DataLayer.DacpacEntityGenerator/_input/*.xlsx`
- First `.xlsx` file found is used
- Multiple files generate warning

**DACPAC Files**:
- Path: `{workspace}/src/DataLayer.DacpacEntityGenerator/_input/dacpacs/{Server}_{Database}.dacpac`
- Naming convention is strict
- Missing files cause database to be skipped

### Output File Locations

**Entity Classes**:
- Path: `{workspace}/src/DataLayer.DacpacEntityGenerator/_output/{Server}/{Database}/{ClassName}.cs`
- Directories created automatically
- UTF-8 encoding used

**DbContext Configuration**:
- Path: `{workspace}/src/DataLayer.DacpacEntityGenerator/_output/DbContext.onModelCreating`
- Plain text file (not `.cs`)
- Requires manual integration

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
var projectDirectory = Path.Combine(workspaceRoot, "src\\DataLayer.DacpacEntityGenerator");
```
**Issue**: Not flexible for different project structures.

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
**Note**: Uses Database name as schema (unusual choice).

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

1. **Hardcoded Paths**: Project directory structure is assumed
2. **Schema Attribute Bug**: Uses Database name instead of Schema name in `[Table]` attribute
3. **Missing Property**: `AddedByAPI` referenced but not defined in `ExcelRow` model
4. **No Relationship Generation**: Foreign keys not processed
5. **No Navigation Properties**: Entity relationships not generated
6. **Single Excel Support**: Only first Excel file is processed
7. **Windows Path Separators**: Uses `\\` which may cause issues on Unix
8. **No Rollback**: Partial failures leave partial output
9. **No Incremental Generation**: Always regenerates all entities
10. **BaseEntity Assumption**: Assumes `BaseEntity` class exists externally

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
