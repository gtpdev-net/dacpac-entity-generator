# DACPAC Entity Generator

A .NET 8 console application that automatically generates C# entity classes from SQL Server DACPAC files, using an Excel spreadsheet to filter and select specific tables and columns for entity generation.

## Overview

This tool streamlines the process of creating Entity Framework Core entities by:
- Reading database schema information from SQL Server DACPAC files
- Using an Excel spreadsheet to specify which tables and columns to include
- Generating properly-typed C# entity classes with Data Annotations
- Supporting multiple databases and servers in a single run
- Auto-detecting primary keys (including composite keys)
- Generating Entity Framework Core configuration code

## Features

- **Excel-Driven Generation**: Control which tables and columns to generate using a simple Excel spreadsheet
- **DACPAC Schema Extraction**: Extracts accurate schema information directly from DACPAC files
- **Smart Type Mapping**: Automatically maps SQL Server data types to appropriate C# types
- **Primary Key Detection**: Identifies and properly configures single and composite primary keys
- **Naming Conventions**: Converts SQL naming conventions to C# PascalCase
- **Multiple Database Support**: Process multiple servers and databases in a single execution
- **EF Core Configuration**: Generates `OnModelCreating` configuration for Entity Framework Core
- **Comprehensive Logging**: Color-coded console output tracks progress and issues

## Prerequisites

- .NET 8.0 SDK
- SQL Server DACPAC files for your databases
- Excel file (.xlsx) with table and column specifications

## Input Requirements

### Directory Structure

Place your input files in the `_input` directory:

```
_input/
├── dacpacs/
│   ├── Server1_Database1.dacpac
│   ├── Server1_Database2.dacpac
│   └── Server2_Database1.dacpac
└── YourSpreadsheet.xlsx
```

### Excel File Format

The Excel file must contain the following columns:

| Column Name | Description | Example |
|-------------|-------------|---------|
| Server | SQL Server instance name | `Server1` |
| Database | Database name | `MyDatabase` |
| Schema | Schema name | `dbo` |
| Table | Table name | `Users` |
| Column | Column name | `UserId` |
| Table in DAO Analysis | Boolean flag (TRUE/FALSE or 1/0) | `TRUE` |
| Persistence Type | Persistence type code | `R` |
| Added by API | Boolean flag (TRUE/FALSE or 1/0) | `FALSE` |

**Filter Criteria**: Only rows where:
- (`Table in DAO Analysis` = TRUE **OR** `Added by API` = TRUE) **AND**
- `Persistence Type` = "R"

will be processed.

### DACPAC File Naming

DACPAC files must follow the naming convention: `{Server}_{Database}.dacpac`

Examples:
- `ProductionServer_CustomerDB.dacpac`
- `DevServer_InventoryDB.dacpac`

## Usage

### Running the Application

1. Place your DACPAC files in `_input/dacpacs/`
2. Place your Excel file in `_input/`
3. Run the application:

```bash
dotnet run --project src/DacpacEntityGenerator/DacpacEntityGenerator.csproj
```

Or from the project directory:

```bash
cd src/DacpacEntityGenerator
dotnet run
```

### Output

Generated files are placed in the `_output` directory:

```
_output/
├── Server1/
│   ├── Database1/
│   │   ├── Users.cs
│   │   ├── Orders.cs
│   │   └── Products.cs
│   └── Database2/
│       └── Customers.cs
├── Server2/
│   └── Database1/
│       └── Inventory.cs
└── DbContext.onModelCreating
```

### Generated Entity Example

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DataLayer.Core.Entities;

namespace DataLayer.Core.Entities.ProductionServer.CustomerDB
{
    // This entity was generated from:
    // [ProductionServer].[CustomerDB].[dbo].[Users]
    [Table("Users", Schema = "CustomerDB")]
    public class Users : BaseEntity
    {
        [Column("UserId")]
        [Required]
        public int UserId { get; set; }

        [Column("Username")]
        [Required]
        [MaxLength(50)]
        public required string Username { get; set; }

        [Column("Email")]
        [MaxLength(255)]
        public string? Email { get; set; }

        [Column("CreatedDate")]
        [Required]
        public DateTime CreatedDate { get; set; }
    }
}
```

### DbContext Configuration

The generator creates a `DbContext.onModelCreating` file containing Entity Framework Core configuration:

```csharp
modelBuilder.Entity<Core.Entities.ProductionServer.CustomerDB.Users>();
modelBuilder.Entity<Core.Entities.ProductionServer.CustomerDB.Orders>().HasKey(e => new { e.OrderId, e.LineNumber });
modelBuilder.Entity<Core.Entities.ProductionServer.CustomerDB.AuditLog>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
```

This code should be inserted into your DbContext's `OnModelCreating` method.

## Features in Detail

### Type Mapping

The tool maps SQL Server types to C# types:

| SQL Server Type | C# Type |
|----------------|---------|
| bit | bool |
| tinyint | byte |
| smallint | short |
| int | int |
| bigint | long |
| decimal, numeric, money | decimal |
| float | double |
| real | float |
| char, varchar, nchar, nvarchar, text | string |
| date | DateOnly |
| time | TimeOnly |
| datetime, datetime2, smalldatetime | DateTime |
| datetimeoffset | DateTimeOffset |
| uniqueidentifier | Guid |
| binary, varbinary, image | byte[] |

### Nullable Handling

- Nullable SQL columns generate nullable C# types (`int?`, `DateTime?`, `string?`)
- Non-nullable SQL columns generate required properties (with `required` modifier for strings)
- Primary key columns are always marked as `[Required]`

### Primary Key Handling

- **Single Primary Key**: Automatically included in entity (no explicit `[Key]` attribute needed when using EF Core configuration)
- **Composite Primary Keys**: All key columns included, configuration generated in `DbContext.onModelCreating`
- **Auto-Added Keys**: Primary key columns not listed in Excel are automatically included

### Name Conversion

- **Tables**: Converted to PascalCase class names (e.g., `user_accounts` → `UserAccounts`)
- **Columns**: Converted to PascalCase properties (e.g., `user_id` → `UserId`)
- **Keyword Conflicts**: C# keywords prefixed with `@` (e.g., `class` → `@class`)
- **Class Name Conflicts**: If a property name matches the class name, the class is suffixed with `Entity`

### Logging

The console output uses color-coded logging:

- **Green [SUCCESS]**: Successful operations and generated files
- **Yellow [WARNING]**: Non-critical issues (e.g., missing columns, no primary key)
- **Red [ERROR]**: Critical errors (e.g., missing DACPAC files, parsing failures)
- **White [INFO]**: General information and progress updates

## Troubleshooting

### "No Excel file found"
Ensure your `.xlsx` file is in the `_input/` directory.

### "DACPAC file not found"
Check that:
- DACPAC files are in `_input/dacpacs/`
- File names follow the pattern: `{Server}_{Database}.dacpac`
- Server and Database names in Excel match DACPAC file names

### "No rows matched the filter criteria"
Verify that your Excel file has rows where:
- `Table in DAO Analysis` or `Added by API` = TRUE
- `Persistence Type` = 'R'

### "Table not found in DACPAC"
The table specified in Excel doesn't exist in the DACPAC. Check spelling and schema names.

### "Column from Excel not found in table"
A column listed in Excel doesn't exist in the DACPAC table definition. This generates a warning but doesn't stop processing.

## Building the Project

```bash
# Build
dotnet build

# Run tests (if available)
dotnet test

# Create release build
dotnet build -c Release
```

## Dependencies

- **ClosedXML** (0.105.0): Excel file reading and parsing
- **.NET 8.0**: Target framework

## License

[Specify your license here]

## Contributing

[Specify contribution guidelines here]
