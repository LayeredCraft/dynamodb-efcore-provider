# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Querying Microsoft Documentation

You have access to an MCP server called `microsoft.docs.mcp` - this tool allows you to search through Microsoft's latest official documentation, and that information might be more detailed or newer than what's in your training data set.

When handling questions around how to work with native Microsoft technologies, such as C#, F#, ASP.NET Core, Microsoft.Extensions, NuGet, Entity Framework, the `dotnet` runtime - please use this tool for research purposes when dealing with specific / narrowly defined questions that may occur.

## Project Overview

This is an Entity Framework Core database provider for AWS DynamoDB. It translates LINQ queries to PartiQL statements and executes them against DynamoDB using the AWS SDK.

**Current Status:** MVP (Minimal Viable Product) - Supports basic query operations with WHERE, ORDER BY, and explicit column projections. See [FEATURES.md](FEATURES.md) for complete feature support matrix.

**Key Features:**
- ✅ `.Where()` with parameter inlining (comparison, AND, OR)
- ✅ `.OrderBy()` / `.ThenBy()` (ascending/descending)
- ✅ Explicit column projections (auto-generated)
- ✅ Async query execution only
- ❌ No `.Select()` custom projections yet
- ❌ No CRUD operations (query-only)
- ❌ No aggregate functions (COUNT, SUM, etc.)

## Build and Test Commands

### Building the Solution
```bash
dotnet build
```

### Running Tests
```bash
# Set these for whatever test project/framework you're running
TEST_PROJECT="tests/<YourTestProject>/<YourTestProject>.csproj"
TFM="net10.0"

# list tests (copy fully-qualified name)
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --list-tests --no-progress --no-ansi

# run one test method
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-method "MyNamespace.MyTestClass.MyTestMethod" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi

# handy filters
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-class "MyNamespace.MyTestClass" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi

DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-namespace "MyNamespace.Tests" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi
```


Note: This project uses Microsoft.Testing.Platform (configured in `global.json`), not VSTest.

### Running the Example
```bash
# Navigate to example directory
cd examples/Example.Simple

# Start DynamoDB Local and seed data
docker-compose up -d

# Run the example
dotnet run

# Stop DynamoDB Local
docker-compose down
```

The example connects to DynamoDB Local on `http://localhost:8002` and queries a `SimpleItems` table.

## Project Configuration

- **.NET Version:** 10.0 (see `global.json`)
- **Central Package Management:** Enabled via `Directory.Packages.props`
- **LangVersion:** Latest C# (see `Directory.Build.props`)
- **TreatWarningsAsErrors:** True

## Architecture Overview

### Query Execution Pipeline (3-Stage Process)

The provider follows Entity Framework Core's standard query pipeline with DynamoDB-specific implementations:

**Stage 1: LINQ Method Translation**
- **Component:** `DynamoQueryableMethodTranslatingExpressionVisitor`
- **Purpose:** Converts LINQ method calls (Where, OrderBy, etc.) to `ShapedQueryExpression` objects
- **Supported Methods:** `Where()`, `OrderBy()`, `OrderByDescending()`, `ThenBy()`, `ThenByDescending()`
- **Auto-generated Projections:** All entity properties are automatically projected in `CreateShapedQueryExpression()`
- **SQL Translation:** `DynamoSqlTranslatingExpressionVisitor` converts C# expressions to `SqlExpression` trees

**Stage 2: Shaped Query Compilation**
- **Component:** `DynamoShapedQueryCompilingExpressionVisitor`
- **Purpose:** Compiles abstract query shapes into executable code
- **Key Innovation:** Defers SQL generation to runtime for parameter inlining
- **Shaper Transformation (3 steps):**
  1. **Injection** (`DynamoInjectingExpressionVisitor`) - Prepares `Dictionary<string, AttributeValue>` parameter handling
  2. **Materialization** - Adds entity construction logic via `InjectStructuralTypeMaterializers()`
  3. **Binding Removal** (`DynamoProjectionBindingRemovingExpressionVisitor`) - Replaces abstract bindings with concrete dictionary access

**Stage 3: Query Execution (Runtime)**
- **Component:** `QueryingEnumerable<T>` and `DynamoClientWrapper`
- **Purpose:** Executes PartiQL against DynamoDB and materializes results
- **Runtime SQL Generation:**
  1. **Parameter Inlining** (`ParameterInliner`) - Replaces `SqlParameterExpression` with `SqlConstantExpression` using runtime values
  2. **SQL Generation** (`DynamoQuerySqlGenerator`) - Converts SQL expression tree to PartiQL string
  3. **Execution** - Sends PartiQL + AttributeValue parameters to DynamoDB
- **Key Characteristics:**
  - Async-only execution (sync enumeration throws `NotImplementedException`)
  - Handles pagination via NextToken
  - Returns `IAsyncEnumerable<T>`

### PartiQL Query Generation

**Files:**
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs` - SQL generation
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoPartiQlQuery.cs` - Query wrapper

**Capabilities:**
- ✅ Explicit column projections: `SELECT col1, col2, ... FROM table`
- ✅ WHERE clauses with binary operators: `=`, `<>`, `<`, `>`, `<=`, `>=`
- ✅ Logical operators: `AND`, `OR`
- ✅ Arithmetic operators: `+`, `-` (PartiQL doesn't support `*`, `/`, `%`)
- ✅ ORDER BY with multiple columns: `ASC`, `DESC`
- ✅ Parameter inlining: All runtime parameters converted to AttributeValue constants
- ✅ Reserved word quoting: ~10 common keywords (should expand to 573-word list)
- ❌ SELECT * explicitly disabled (throws exception)
- ❌ No functions (SIZE, EXISTS, BEGINS_WITH, etc.)
- ❌ No IN, BETWEEN, NOT, IS NULL, IS MISSING

### Type Conversion

**File:** `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingRemovingExpressionVisitor.cs` (lines 104-209)

The `GetValue<T>()` method handles conversion from DynamoDB's `AttributeValue` format:
- **String:** `attributeValue.S`
- **Boolean:** `attributeValue.BOOL`
- **Numeric types:** `attributeValue.N` (parsed to int, long, short, byte, double, float, decimal)
- **Guid:** Parsed from string
- **DateTime/DateTimeOffset:** ISO8601 string or Unix timestamp

Applies EF Core value converters via `typeMapping.Converter.ConvertFromProvider()`.

### SQL Expression Infrastructure

The provider uses a custom SQL expression tree to represent DynamoDB PartiQL queries:

**Expression Types** (`Query/Internal/Expressions/`):
- `SqlExpression` - Base class for all SQL expressions
- `SqlBinaryExpression` - Binary operators (=, <, >, AND, OR, +, -)
- `SqlConstantExpression` - Constant values with type mappings
- `SqlParameterExpression` - Runtime parameters (inlined before SQL generation)
- `SqlPropertyExpression` - Column/property references
- `ProjectionExpression` - Projected columns in SELECT clause
- `SelectExpression` - Complete SELECT query (table, projections, predicate, orderings)
- `OrderingExpression` - ORDER BY clause component

**Expression Factory** (`ISqlExpressionFactory`, `SqlExpressionFactory`):
- Creates SQL expressions with proper type mappings
- `Binary()` - Creates binary expressions with type inference
- `Constant()` - Creates constants with type mappings
- `Parameter()` - Creates parameters with type mappings
- `Property()` - Creates property references
- `ApplyTypeMapping()` - Applies type mappings to existing expressions

**Type Mapping** (`DynamoTypeMappingSource`):
- Maps CLR types to DynamoDB AttributeValue types
- Supports: string, bool, numeric types, Guid, DateTime, DateTimeOffset
- Applies value converters when configured

### Expression Visitors (Critical Pattern)

This provider uses multiple expression visitors to transform EF Core's abstract query model into executable code:

**DynamoSqlTranslatingExpressionVisitor**
- Translates C# expression trees to SQL expression trees
- Handles `VisitBinary()`: Converts binary operators to `SqlBinaryExpression`
- Handles `VisitConstant()`: Creates `SqlConstantExpression`
- Handles `VisitMember()`: Converts property access to `SqlPropertyExpression`
- Handles `VisitParameter()`: Creates `SqlParameterExpression`

**ParameterInliner** (nested in `DynamoShapedQueryCompilingExpressionVisitor`)
- Extends `SqlExpressionVisitor`
- Replaces `SqlParameterExpression` with `SqlConstantExpression` at runtime
- Looks up actual parameter values from `QueryContext.Parameters`
- Applies type mappings via `ISqlExpressionFactory`
- **Critical:** Prevents NULL placeholders by using actual runtime values

**DynamoInjectingExpressionVisitor**
- Adds `Dictionary<string, AttributeValue>` parameter handling
- Prepares expression tree for materialization

**DynamoProjectionBindingRemovingExpressionVisitor**
- Replaces `ProjectionBindingExpression` with concrete dictionary access
- Intercepts `ValueBufferTryReadValue<T>()` calls
- Bridges EF Core's abstract model and DynamoDB's data format

**DynamoQuerySqlGenerator**
- Extends `SqlExpressionVisitor`
- Visits SQL expression tree and generates PartiQL string
- Handles all SQL expression types: Binary, Constant, Property, Projection, Select
- Converts `SqlExpression` → PartiQL string
- Builds `AttributeValue` parameter list

### AWS Client Integration

**File:** `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`

Wraps `AmazonDynamoDBClient` and provides:
- PartiQL execution via `ExecuteStatementRequest`
- Pagination with `NextToken` tracking
- Async enumeration of results
- Concurrent access safety via execution strategy

### Configuration

**Options Extension:** `DynamoDbOptionsExtension`
- Supports `AuthenticationRegion` and `ServiceUrl` configuration

**Extension Methods:** `DynamoDbContextOptionsExtensions`
```csharp
optionsBuilder.UseDynamo(options =>
    options.ServiceUrl("http://localhost:8002"));
```

## Key Files and Directories

### Documentation
- **[FEATURES.md](FEATURES.md)** - Complete feature support matrix (EF Core LINQ methods, PartiQL features, type mappings, limitations)
- **[CLAUDE.md](CLAUDE.md)** - This file; architecture and development guide

### Core Provider Implementation
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/` - Query compilation pipeline
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/` - AWS client wrapper and PartiQL generation
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/` - DI registration and DbContext extensions
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Infrastructure/Internal/` - Options and configuration

### Tests and Examples
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/` - Unit and integration tests
- `examples/Example.Simple/` - Simple console app demonstrating basic usage with DynamoDB Local

## Current Limitations (MVP Phase)

**See [FEATURES.md](FEATURES.md) for complete feature support matrix.**

### ✅ Implemented
- WHERE clauses with comparison and logical operators
- ORDER BY with multiple columns (ASC/DESC)
- Explicit column projections (auto-generated)
- Parameter inlining with runtime values
- All numeric, string, boolean, DateTime, Guid types

### ❌ Not Yet Implemented
- **Custom `.Select()` projections:** All entity properties always projected
- **CRUD operations:** Query-only (no Insert, Update, Delete)
- **Aggregate functions:** COUNT, SUM, AVG, etc. (PartiQL limitation)
- **Advanced operators:** IN, BETWEEN, NOT, IS NULL, IS MISSING
- **Functions:** SIZE, EXISTS, BEGINS_WITH, CONTAINS
- **JOINs:** Not supported by PartiQL
- **GROUP BY:** Not supported by PartiQL
- **DISTINCT:** Not supported by PartiQL
- **SKIP/TAKE:** Not supported by PartiQL (use NextToken pagination)
- **Synchronous queries:** Must use async methods

## Development Patterns

### Expression Tree Transformation
When modifying query compilation logic, follow the three-stage pattern:
1. Inject parameter handling
2. Add materialization logic
3. Remove abstract bindings and replace with concrete data access

### Adding LINQ Method Support
To add support for a new LINQ method (e.g., `Where`):
1. Implement the method in `DynamoQueryableMethodTranslatingExpressionVisitor`
2. Update `PartiQlQuery` generation to handle the new query structure
3. Test with DynamoDB Local using the example project

### Type Mapping
To add support for a new type:
1. Add mapping in `DynamoTypeMappingSource`
2. Add conversion logic in `DynamoProjectionBindingRemovingExpressionVisitor.GetValue<T>()`
3. Handle both read and write scenarios

## Testing with DynamoDB Local

The example project includes a Docker Compose setup for local testing:

```bash
cd examples/Example.Simple
docker-compose up -d  # Start DynamoDB Local on port 8002
dotnet run            # Run the example
docker-compose down   # Stop DynamoDB Local
```

The seeder script (`scripts/seed-database.sh`) automatically creates the `SimpleItems` table and populates it with test data.
