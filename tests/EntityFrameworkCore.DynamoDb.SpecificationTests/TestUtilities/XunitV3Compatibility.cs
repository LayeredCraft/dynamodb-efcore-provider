#if NET11_0_OR_GREATER
// EF Core 11 Specification.Tests (xUnit v3) marks base tests with plain [Fact]/[Theory]. xUnit v3
// rejects a
// test whose override carries a *different* Fact-derived attribute ("multiple [Fact]-derived
// attributes"),
// so on EF11 the shared [ConditionalFact]/[ConditionalTheory] spellings must resolve to the exact
// same types.
global using ConditionalFactAttribute = Xunit.FactAttribute;
global using ConditionalTheoryAttribute = Xunit.TheoryAttribute;
#endif
