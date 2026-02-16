# Boolean Default Value Pattern - Implementation Example

## Overview
This document demonstrates how bool properties with database-generated default values are now generated to avoid EF Core warnings.

## Problem
EF Core generates warnings for bool properties with database defaults:
```
The 'bool' property 'Scrap' on entity type 'Item' is configured with a database-generated default, 
but has no configured sentinel value. The database-generated default will always be used for inserts 
when the property has the value 'False', since this is the CLR default for the 'bool' type.
```

## Solution
Use a nullable backing field pattern that allows EF Core to distinguish between "explicitly set to false" and "not set (use database default)".

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

## How It Works

1. **Nullable Backing Field**: The private backing field `_scrap` is nullable (`bool?`)
2. **Public Property**: The public property `Scrap` is non-nullable (`bool`)
3. **Getter Logic**: Returns the backing field value if set, otherwise returns the default value
4. **EF Core Detection**: When the backing field is null, EF Core knows to use the database default
5. **Explicit Values**: When explicitly set (true or false), EF Core uses that value

## Benefits

- ✅ No EF Core warnings about sentinel values
- ✅ Database defaults work correctly for INSERT operations
- ✅ Explicit false values are respected
- ✅ Clean API - consumers only see non-nullable bool properties
- ✅ Type-safe - no nullable bool exposed to consumers

## Implementation Details

The generator:
1. Detects bit columns with default values
2. Parses the SQL default to determine if it's true or false
3. Generates the property with the appropriate backing field pattern
4. Creates proper camelCase backing field names

**Supported Default Value Formats:**
- `((0))`, `((1))`
- `(0)`, `(1)`
- `'0'`, `'1'`
- `'true'`, `'false'`
