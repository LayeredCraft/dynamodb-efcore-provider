# Diagnostics

## Logging
- Logs PartiQL command execution.
- Logs `ExecuteStatement` request metadata (for example limit and pagination token presence).
- Logs `ExecuteStatement` response metadata (for example item count and token presence).

## Warnings
- Row-limiting query without configured page size logs a warning.

## Recommended practice
- Enable command logging in development and tests to verify translation behavior.
