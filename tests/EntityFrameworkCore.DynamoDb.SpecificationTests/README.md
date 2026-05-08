# DynamoDB specification tests

Northwind query specification coverage starts with small, passing `Where` cases against DynamoDB Local. Add suites in this order:

1. `Where` coverage: simple predicates, scan-safe queries, explicit unsupported overrides.
2. `Select` and aggregate coverage: projections and scalar results with PartiQL baselines.
3. Ad-hoc and non-query specs: only after query harness gaps are closed.

Mirror EF Core/Cosmos style for unsupported provider behavior: override individual cases with explicit assertions or skips instead of broad suppression. Keep table-per-type Northwind scans enabled through the fixture, not per test.
