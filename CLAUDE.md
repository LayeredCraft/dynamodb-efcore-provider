# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Querying Microsoft Documentation

You have access to an MCP server called `microsoft.docs.mcp` - this tool allows you to search through Microsoft's latest official documentation, and that information might be more detailed or newer than what's in your training data set.

When handling questions around how to work with native Microsoft technologies, such as C#, F#, ASP.NET Core, Microsoft.Extensions, NuGet, Entity Framework, the `dotnet` runtime - please use this tool for research purposes when dealing with specific / narrowly defined questions that may occur.

## Project Overview

This is an Entity Framework Core database provider for AWS DynamoDB. It translates LINQ queries to PartiQL statements and executes them against DynamoDB using the AWS SDK.

**Current Status:** MVP (Minimal Viable Product) - Only supports basic `SELECT *` queries with entity materialization. Most LINQ operations (Where, Select, OrderBy, etc.) throw `NotImplementedException`.

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
- **Purpose:** Converts LINQ method calls (Where, Select, etc.) to `ShapedQueryExpression` objects
- **Current State:** Returns `NotImplementedException` for all advanced operations; only basic entity queries supported

**Stage 2: Shaped Query Compilation**
- **Component:** `DynamoShapedQueryCompilingExpressionVisitor`
- **Purpose:** Compiles abstract query shapes into executable code
- **Three-step transformation:**
  1. **Injection** (`DynamoInjectingExpressionVisitor`) - Prepares `Dictionary<string, AttributeValue>` parameter handling
  2. **Materialization** - Adds entity construction logic via `InjectStructuralTypeMaterializers()`
  3. **Binding Removal** (`DynamoProjectionBindingRemovingExpressionVisitor`) - Replaces abstract bindings with concrete dictionary access

**Stage 3: Query Execution**
- **Component:** `QueryingEnumerable<T>` and `DynamoClientWrapper`
- **Purpose:** Executes PartiQL against DynamoDB and materializes results
- **Key Characteristics:**
  - Async-only execution (sync enumeration throws `NotImplementedException`)
  - Handles pagination via NextToken
  - Returns `IAsyncEnumerable<T>`

### PartiQL Query Generation

**File:** `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/PartiQlQuery.cs`

Currently minimal: Generates `SELECT * FROM {tableName}` queries only. No WHERE clauses, ORDER BY, JOINs, or aggregations are supported yet.

### Type Conversion

**File:** `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingRemovingExpressionVisitor.cs` (lines 104-209)

The `GetValue<T>()` method handles conversion from DynamoDB's `AttributeValue` format:
- **String:** `attributeValue.S`
- **Boolean:** `attributeValue.BOOL`
- **Numeric types:** `attributeValue.N` (parsed to int, long, short, byte, double, float, decimal)
- **Guid:** Parsed from string
- **DateTime/DateTimeOffset:** ISO8601 string or Unix timestamp

Applies EF Core value converters via `typeMapping.Converter.ConvertFromProvider()`.

### Expression Visitors (Critical Pattern)

This provider uses multiple expression visitors to transform EF Core's abstract query model into executable code:

**DynamoInjectingExpressionVisitor**
- Adds `Dictionary<string, AttributeValue>` parameter handling
- Prepares expression tree for materialization

**DynamoProjectionBindingRemovingExpressionVisitor**
- Replaces `ProjectionBindingExpression` with concrete dictionary access
- Intercepts `ValueBufferTryReadValue<T>()` calls
- Bridges EF Core's abstract model and DynamoDB's data format

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

### Core Provider Implementation
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/` - Query compilation pipeline
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/` - AWS client wrapper and PartiQL generation
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/` - DI registration and DbContext extensions
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Infrastructure/Internal/` - Options and configuration

### Tests and Examples
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/` - Unit and integration tests
- `examples/Example.Simple/` - Simple console app demonstrating basic usage with DynamoDB Local

## Current Limitations (MVP Phase)

- **Query Translation:** Only `SELECT *` is supported; all LINQ methods (Where, Select, OrderBy, GroupBy, Join, etc.) throw `NotImplementedException`
- **Save Changes:** Not implemented
- **Type Mapping:** Only string explicitly mapped; other types use default EF Core behavior
- **Database Operations:** Query-only (no Create, Update, Delete)
- **Synchronous Operations:** All sync enumeration throws exceptions; must use async methods

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
