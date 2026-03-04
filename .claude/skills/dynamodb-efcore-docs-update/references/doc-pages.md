## Docs Page Impact Matrix

Use this quick map to decide which docs to update for a behavior change.

- Operator translation changed -> `docs/operators.md`, `docs/limitations.md` (if partially supported)
- Pagination/result limits/tokens changed -> `docs/pagination.md`, `docs/operators.md` (if operator-specific)
- Projection/materialization behavior changed -> `docs/projections.md`, `docs/operators.md` (if query-shape specific)
- Provider configuration/option changed -> `docs/configuration.md`
- New warnings/logs/diagnostics changed -> `docs/diagnostics.md`
- Support/limitations changed -> `docs/limitations.md`, `docs/operators.md`
- End-to-end query pipeline changed -> `docs/architecture.md`
- Navigation/site structure changed -> `zensical.toml`

## Operator Entry Template

Use this structure when adding or updating an operator in `docs/operators.md`.

```
### <Operator or LINQ shape>

Supported: <Yes|No|Partial>

LINQ example:
<short snippet that is currently supported>

PartiQL shape:
<representative PartiQL pattern or statement>

Notes:
- <behavior caveat or translation boundary>
- <client/server evaluation note, if relevant>

AWS semantic reference (when needed):
- <link to ExecuteStatement / PartiQL operators / AttributeValue docs>
```

## Quality Checks Before Build

- Examples compile mentally against current supported translation surface
- No internal code/test links in user-facing docs
- Language is concise and behavior-focused
- AWS semantic references included where behavior depends on DynamoDB/PartiQL rules
