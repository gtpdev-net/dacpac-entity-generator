# Value Type Default Value Pattern - Implementation Example

## Overview
This document demonstrates how bool and int properties with database-generated default values are now generated to avoid EF Core warnings.

## Problem
EF Core generates warnings for value type properties with database defaults:
```
The 'bool' property 'Scrap' on entity type 'Item' is configured with a database-generated default, 
but has no configured sentinel value. The database-generated default will always be used for inserts 
when the property has the value 'False', since this is the CLR default for the 'bool' type.
```

The same issue occurs with int properties where the default is 0 (the CLR default for int).

## Solution
Use a nullable backing field pattern that allows EF Core to distinguish between "explicitly set to a value" and "not set (use database default)".

## Generated Code Examples

### Example 1: Boolean with Default FALSE
**SQL Column Definition:**
```sql
CREATE TABLE Items (
    ItemID INT PRIMARY KEY,
    Scrap BIT NOT NULL DEFAULT ((0))
)
```

**Generated Entity Code:**
```csharp
// SQL Default: ((0))
[Column("Scrap")]
public bool Scrap
{
    get => _scrap ?? false;   // Returns FALSE when null (database default)
    set => _scrap = value;
}
private bool? _scrap;
```

### Example 2: Boolean with Default TRUE
**SQL Column Definition:**
```sql
CREATE TABLE Users (
    UserID INT PRIMARY KEY,
    IsActive BIT NOT NULL DEFAULT ((1))
)
```

**Generated Entity Code:**
```csharp
// SQL Default: ((1))
[Column("IsActive")]
public bool IsActive
{
    get => _isActive ?? true;   // Returns TRUE when null (database default)
    set => _isActive = value;
}
private bool? _isActive;
```

### Example 3: Integer with Default 0
**SQL Column Definition:**
```sql
CREATE TABLE Products (
    ProductID INT PRIMARY KEY,
    Stabilizer INT NOT NULL DEFAULT ((0))
)
```

**Generated Entity Code:**
```csharp
// SQL Default: ((0))
[Column("Stabilizer")]
public int Stabilizer
{
    get => _stabilizer ?? 0;   // Returns 0 when null (database default)
    set => _stabilizer = value;
}
private int? _stabilizer;
```

### Example 4: Integer with Non-Zero Default
**SQL Column Definition:**
```sql
CREATE TABLE Tasks (
    TaskID INT PRIMARY KEY,
    Priority INT NOT NULL DEFAULT ((5))
)
```

**Generated Entity Code:**
```csharp
// SQL Default: ((5))
[Column("Priority")]
public int Priority
{
    get => _priority ?? 5;   // Returns 5 when null (database default)
    set => _priority = value;
}
private int? _priority;
```

## How It Works

1. **Nullable Backing Field**: The private backing field (e.g., `_scrap`, `_stabilizer`) is nullable (`bool?`, `int?`)
2. **Public Property**: The public property (e.g., `Scrap`, `Stabilizer`) is non-nullable (`bool`, `int`)
3. **Getter Logic**: Returns the backing field value if set, otherwise returns the database default value
4. **EF Core Detection**: When the backing field is null, EF Core knows to use the database default
5. **Explicit Values**: When explicitly set, EF Core uses that value

## Benefits

- ✅ No EF Core warnings about sentinel values
- ✅ Database defaults work correctly for INSERT operations
- ✅ Explicit values (including CLR defaults like `false` or `0`) are respected
- ✅ Clean API - consumers only see non-nullable properties
- ✅ Type-safe - no nullable types exposed to consumers

## Implementation Details

The generator:
1. Detects `bit`, `int`, `smallint`, `tinyint`, and `bigint` columns with default values
2. Parses the SQL default to extract the appropriate value
3. Generates the property with the appropriate backing field pattern
4. Creates proper camelCase backing field names

**Supported Types:**
- `bit` (bool)
- `tinyint` (byte)
- `smallint` (short)
- `int` (int)
- `bigint` (long)

**Supported Default Value Formats:**
- `((0))`, `((1))`, `((5))`, etc.
- `(0)`, `(1)`, `(5)`, etc.
- `'0'`, `'1'`, `'5'`, etc.
- `'true'`, `'false'` (for bit columns)
