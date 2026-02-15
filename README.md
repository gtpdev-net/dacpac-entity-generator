# DACPAC Entity Generator

A .NET 8 console application that automatically generates C# entity classes from SQL Server DACPAC files, using an Excel spreadsheet to filter and select specific tables and columns for entity generation.

## Overview

This tool streamlines the process of creating Entity Framework Core entities by extracting database schema information from SQL Server DACPAC files and generating properly-typed C# entity classes with Data Annotations.

## Key Features

- **Excel-Driven Selection**: Control which tables and columns to generate
- **DACPAC Schema Extraction**: Direct schema parsing from DACPAC files (SQL Server 2005-2022+)
- **Smart Type Mapping**: SQL Server types → C# types with proper nullability
- **Primary Key Detection**: Auto-detects single and composite keys
- **Multiple Database Support**: Process multiple servers/databases in one run
- **EF Core Configuration**: Generates `OnModelCreating` code

## Quick Start

1. Place your DACPAC files in `src/DacpacEntityGenerator/_input/dacpacs/`
2. Place your Excel file in `src/DacpacEntityGenerator/_input/`
3. Run: `dotnet run --project src/DacpacEntityGenerator/DacpacEntityGenerator.csproj`
4. Find generated entities in `src/DacpacEntityGenerator/_output/`

## Documentation

- **[User Guide](src/DacpacEntityGenerator/README.md)**: Complete usage instructions, troubleshooting, and examples
- **[Technical Specification](src/DacpacEntityGenerator/SPEC.md)**: Architecture, design patterns, and implementation details

## Requirements

- .NET 8.0 SDK
- SQL Server DACPAC files (any SQL Server version 2005+)
- Excel file (.xlsx) with column specifications

## DACPAC Compatibility

Supports DACPAC FileFormatVersion 1.2, SchemaVersion 3.5, and is compatible with:
- SQL Server 2005 (Sql90) through SQL Server 2022 (Sql160)
- Standard Microsoft Data Tools DACPAC format
- Both legacy and modern DACPAC XML structures

## Technology Stack

- **.NET 8.0** with C# 12
- **System.Xml.Linq** for XML parsing
- **ClosedXML** for Excel file processing
- **System.IO.Compression** for DACPAC extraction

## Project Structure

```
dacpac-entity-generator/
├── src/
│   └── DacpacEntityGenerator/
│       ├── Program.cs                    # Main orchestration
│       ├── Models/                       # Data models
│       ├── Services/                     # Business logic
│       ├── Utilities/                    # Helper classes
│       ├── _input/                       # Place input files here
│       │   └── dacpacs/                  # DACPAC files
│       └── _output/                      # Generated entities
└── README.md                             # This file
```

## Building

```bash
dotnet build
dotnet build -c Release
```

## License

[Specify your license here]

## Contributing

[Specify contribution guidelines here]