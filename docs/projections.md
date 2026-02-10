# Projections

## Supported shapes
- Entity projections (`Select(x => x)`).
- Scalar projections (`Select(x => x.Property)`).
- DTO and anonymous type projections.

## Behavior
- The provider emits explicit projection columns.
- Duplicate selected attributes are de-duplicated.
- Some computed projection expressions are evaluated client-side.

## Notes
- Client-side computed projections follow normal .NET null behavior.
