# Modeling DynamoDB Data

Start from access patterns, then choose the partition key, optional sort key, and secondary indexes.
An EF entity is stored as one DynamoDB item; it is not a relational row with joins available later.

## Keys and tables

- Every mapped entity needs a DynamoDB partition key. Add a sort key when the table's access
  pattern needs ordered or ranged items within a partition.
- Entity keys and DynamoDB table keys must be compatible. Do not assume a composite EF key maps to
  arbitrary DynamoDB attributes.
- Map property names deliberately when existing tables use a naming convention.
- Use GSIs and LSIs for known alternative access patterns. An index must exist before a query can
  use it.

## Shapes that work well

- Complex properties and supported collections are stored in the same item.
- Shared-table designs use compatible keys and provider discriminator behavior. Model the item
  layout, discriminator, and access patterns together.
- Value converters are useful for scalar representation changes, but can limit query translation.

## Do not model DynamoDB as relational EF Core

- There are no joins or foreign-key enforcement.
- A navigation graph is not a substitute for a DynamoDB access pattern.
- DynamoDB items have size limits. Keep large or independently accessed data in a separate item or
  storage service.
- Existing-schema validation is intentionally limited; provision critical production schema through
  infrastructure when stronger guarantees are required.
