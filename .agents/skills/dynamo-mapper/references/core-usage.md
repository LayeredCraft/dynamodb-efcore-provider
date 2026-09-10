# Core Usage

## Minimal mapper pattern

```csharp
using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

public sealed class Order
{
    public string Id { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

[DynamoMapper]
public static partial class OrderMapper
{
    public static partial Dictionary<string, AttributeValue> ToItem(Order source);
    public static partial Order FromItem(Dictionary<string, AttributeValue> item);
}
```

## Mapper rules

- The mapper is a `static partial class` marked with `[DynamoMapper]`.
- `To*` methods take one model parameter and return `Dictionary<string, AttributeValue>`.
- `From*` methods take one `Dictionary<string, AttributeValue>` and return the model type.
- One-way mappers are valid.

## Defaults

- names default to camelCase
- requiredness defaults to nullability inference
- base-class properties are excluded by default
- `OmitNullValues` defaults to `false`
- deprecated `OmitNullStrings` still affects helper-backed nullable scalars and collections
- empty strings are kept by default
- format defaults are `DateTime = O`, `TimeSpan = c`, `Enum = G`, `Guid = D`

## Per-member control

Use `[DynamoField(memberName)]` on the mapper class for:

- `AttributeName`
- `Required`
- `Kind`
- `OmitIfNull`
- `OmitIfEmptyString`
- `ToMethod`
- `FromMethod`
- `Format`

Use `[DynamoIgnore(memberName)]` to skip one or both directions.

- `FromModel` skips model -> item
- `ToModel` skips item -> model

Dot notation works for nested members like `"ShippingAddress.Line1"` and for collection element
members like `"Contacts.VerifiedAt"` (where `Contacts` is `List<CustomerContact>`).

`OmitIfNull` applies at any depth, including nested object and nested collection properties.

## Constructors

- Put `[DynamoMapperConstructor]` on the model constructor, not the mapper.
- If exactly one constructor is marked, it wins.
- Otherwise DynamoMapper prefers a usable parameterless/property-init path and falls back to the
  constructor with the most parameters.
- Constructor parameters match .NET property names, not DynamoDB attribute names.

## Nested mapping

Supported nested shapes include:

- nested object properties
- lists and arrays of nested objects
- `Dictionary<string, NestedType>`

Selection order for a nested member:

1. dot-notation override
1. nested mapper
1. inline helper generation

Use `OmitNullValues` for mapper-level null omission that should also affect nested object and
nested collection containers. Treat `OmitNullStrings` as legacy compatibility.

## Custom conversion

Use mapper-class static methods through `[DynamoField(ToMethod = ..., FromMethod = ...)]`.

- one-sided overrides are supported
- stale docs describe the wrong converter signatures
- bad converter wiring may fail as normal C# compile errors instead of DynamoMapper diagnostics

## Lifecycle hooks

Hooks are optional extension points on the mapper class for pre/post mapping logic.

- `BeforeToItem(T source, Dictionary<string, AttributeValue> item)`
- `AfterToItem(T source, Dictionary<string, AttributeValue> item)`
- `BeforeFromItem(Dictionary<string, AttributeValue> item)`
- `AfterFromItem(Dictionary<string, AttributeValue> item, ref T entity)`

Rules:

- hooks must be `static partial void`
- hook names are exact (`BeforeToItem`, `AfterToItem`, `BeforeFromItem`, `AfterFromItem`)
- hooks can be declared/implemented in another part of the same partial mapper class
- one-way mappers only emit hooks for the generated direction
- no `To*` hooks means `To*` can stay expression-bodied

Order:

- `To*`: create item -> `BeforeToItem` -> property mapping -> `AfterToItem` -> return item
- `From*`: `BeforeFromItem` -> property mapping/object construction -> `AfterFromItem` -> return

## Requiredness and defaults

- missing required root scalar values throw at runtime
- nullable root scalar values read as `null` when absent
- optional/default-initialized members can keep their C# defaults when the attribute is missing

## Safe guidance

- Put configuration on the mapper, not the POCO.
- Use `[DynamoField]` before inventing extra mapping layers.
- Use `[DynamoMapperConstructor]` when constructor choice is ambiguous.
- Use lifecycle hooks for DynamoDB-specific concerns such as PK/SK composition, TTL, and metadata.
