using System.Reflection;
using System.Runtime.ExceptionServices;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Unit tests for ORDER BY partition-key and sort-key validation in
///     <c>DynamoQueryTranslationPostprocessor.ValidateOrderByConstraints</c>.
/// </summary>
public class OrderByPartitionKeyValidationTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static SqlConstantExpression Const(object value) => new(value, value.GetType(), null);

    /// <summary>
    ///     Builds a <c>SelectExpression</c> with a single ascending ordering on
    ///     <paramref name="orderingAttr" /> and <paramref name="pkAttr" /> as the effective partition key.
    /// </summary>
    private static SelectExpression MakeSelectWithOrdering(string pkAttr, string orderingAttr)
    {
        var sel = new SelectExpression("TestTable");
        sel.ApplyEffectivePartitionKeyPropertyNames(
            new HashSet<string>(StringComparer.Ordinal) { pkAttr });
        sel.ApplyOrdering(
            new OrderingExpression(
                new SqlPropertyExpression(orderingAttr, typeof(string), null),
                true));
        return sel;
    }

    /// <summary>
    ///     Builds a <c>SelectExpression</c> with multiple orderings and <paramref name="pkAttr" /> as
    ///     the effective partition key.  The first entry in <paramref name="orderings" /> is applied via
    ///     <c>ApplyOrdering</c>; subsequent entries are appended via <c>AppendOrdering</c>.
    /// </summary>
    private static SelectExpression MakeSelectWithMultipleOrderings(
        string pkAttr,
        params (string attr, bool asc)[] orderings)
    {
        var sel = new SelectExpression("TestTable");
        sel.ApplyEffectivePartitionKeyPropertyNames(
            new HashSet<string>(StringComparer.Ordinal) { pkAttr });

        if (orderings.Length == 0)
            return sel;

        sel.ApplyOrdering(
            new OrderingExpression(
                new SqlPropertyExpression(orderings[0].attr, typeof(string), null),
                orderings[0].asc));

        for (var i = 1; i < orderings.Length; i++)
            sel.AppendOrdering(
                new OrderingExpression(
                    new SqlPropertyExpression(orderings[i].attr, typeof(string), null),
                    orderings[i].asc));

        return sel;
    }

    /// <summary>
    ///     Builds a <c>DynamoQueryConstraints</c> with optional equality and IN constraints on PK
    ///     attributes.
    /// </summary>
    private static DynamoQueryConstraints MakeConstraints(
        string[]? equalityPks = null,
        string[]? inPks = null)
        => new(
            (equalityPks ?? []).ToDictionary(k => k, _ => (SqlExpression)Const("v")),
            (inPks ?? []).ToDictionary(k => k, _ => (IReadOnlyList<SqlExpression>)[Const("v")]),
            new Dictionary<string, SkConstraint>(),
            false,
            new HashSet<string>());

    // ── pass cases ────────────────────────────────────────────────────────────

    /// <summary>No ORDER BY — validation is skipped entirely.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void NoOrderBy_DoesNotThrow()
    {
        var sel = new SelectExpression("TestTable");
        sel.ApplyEffectivePartitionKeyPropertyNames(
            new HashSet<string>(StringComparer.Ordinal) { "PK" });

        var act = () => InvokeValidate(sel, MakeConstraints(), "SK");

        act.Should().NotThrow();
    }

    /// <summary>Design-time path: null constraints means runtime model is unavailable — skip.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_WhenQueryConstraintsNull_DoesNotThrow()
    {
        var sel = MakeSelectWithOrdering("PK", "SK");

        var act = () => InvokeValidate(sel, null, "SK");

        act.Should().NotThrow();
    }

    /// <summary>Empty effective PK set (no descriptor resolved) — skip silently.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_WhenEffectivePkNamesEmpty_DoesNotThrow()
    {
        var sel = new SelectExpression("TestTable");
        // EffectivePartitionKeyPropertyNames is empty by default
        sel.ApplyOrdering(
            new OrderingExpression(new SqlPropertyExpression("SK", typeof(string), null), true));

        var act = () => InvokeValidate(sel, MakeConstraints(), "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY sort key with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithOrdering("PK", "SK");
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>
    ///     ORDER BY sort key with IN PK constraint on a multi-partition query — must throw. PK must
    ///     lead the ORDER BY chain when the query spans multiple partitions.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_WithInPkConstraint_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "SK");
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*");
    }

    /// <summary>
    ///     PK-only source (no sort key): ordering on the partition key with an IN constraint is
    ///     valid. This is the DynamoDB shape for multi-partition ordering: WHERE PK IN (...) ORDER BY PK.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_OnPkAttr_WhenNoSortKey_AndInConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithOrdering("PK", "PK");
        var constraints = MakeConstraints(inPks: ["PK"]);

        // effectiveSortKey = null signals a PK-only source
        var act = () => InvokeValidate(sel, constraints, null);

        act.Should().NotThrow();
    }

    // ── fail cases ────────────────────────────────────────────────────────────

    /// <summary>ORDER BY on the sort key but no PK constraint in WHERE — must throw.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_WithNoPkConstraint_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "SK");
        var constraints = MakeConstraints(); // no PK constraint

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*");
    }

    /// <summary>ORDER BY on a non-key attribute when a sort key exists — must throw.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_OnNonSortKeyAttribute_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "OtherAttr");
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*key attribute*");
    }

    /// <summary>PK-only source: ORDER BY on a non-PK attribute — must throw.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_OnNonPkAttribute_WhenNoSortKey_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "OtherAttr");
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*'PK'*");
    }

    // ── new pass cases: PK and PK+SK orderings ────────────────────────────────

    /// <summary>ORDER BY on PK ASC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_OnPkAttr_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithOrdering("PK", "PK");
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY on PK DESC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderByDescending_OnPkAttr_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = new SelectExpression("TestTable");
        sel.ApplyEffectivePartitionKeyPropertyNames(
            new HashSet<string>(StringComparer.Ordinal) { "PK" });
        sel.ApplyOrdering(
            new OrderingExpression(new SqlPropertyExpression("PK", typeof(string), null), false));
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK ASC, SK ASC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_PkThenSk_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", true), ("SK", true));
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK DESC, SK DESC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderByDescending_PkThenByDescendingSk_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", false), ("SK", false));
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK ASC, SK DESC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_PkThenByDescendingSk_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", true), ("SK", false));
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK DESC, SK ASC with equality PK constraint — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderByDescending_PkThenBySk_WithEqualityPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", false), ("SK", true));
        var constraints = MakeConstraints(["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK ASC, SK ASC with IN PK constraint (multi-partition) — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_PkThenSk_WithInPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", true), ("SK", true));
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    /// <summary>ORDER BY PK DESC, SK DESC with IN PK constraint (multi-partition) — valid.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderByDescending_PkThenByDescendingSk_WithInPkConstraint_DoesNotThrow()
    {
        var sel = MakeSelectWithMultipleOrderings("PK", ("PK", false), ("SK", false));
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().NotThrow();
    }

    // ── new fail cases: multi-partition ordering violations ───────────────────

    /// <summary>ORDER BY SK (not PK) on a multi-partition query — must throw with partition key message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_OnSk_WhenMultiPartition_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "SK");
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*");
    }

    /// <summary>
    ///     ORDER BY SK DESC (not PK) on a multi-partition query — must throw with partition key
    ///     message.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderByDescending_OnSk_WhenMultiPartition_Throws()
    {
        var sel = new SelectExpression("TestTable");
        sel.ApplyEffectivePartitionKeyPropertyNames(
            new HashSet<string>(StringComparer.Ordinal) { "PK" });
        sel.ApplyOrdering(
            new OrderingExpression(new SqlPropertyExpression("SK", typeof(string), null), false));
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*");
    }

    /// <summary>
    ///     ORDER BY non-key attribute first on a multi-partition query — must throw with key
    ///     attribute message.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OrderBy_NonKeyFirst_WhenMultiPartition_Throws()
    {
        var sel = MakeSelectWithOrdering("PK", "NonKey");
        var constraints = MakeConstraints(inPks: ["PK"]);

        var act = () => InvokeValidate(sel, constraints, "SK");

        act.Should().Throw<InvalidOperationException>().WithMessage("*key attribute*");
    }

    // ── reflection shim ───────────────────────────────────────────────────────

    /// <summary>
    ///     Calls the private static <c>ValidateOrderByConstraints</c> method on
    ///     <c>DynamoQueryTranslationPostprocessor</c> via reflection so unit tests can exercise it
    ///     directly without spinning up the full EF Core query pipeline.
    /// </summary>
    private static void InvokeValidate(
        SelectExpression selectExpression,
        DynamoQueryConstraints? queryConstraints,
        string? effectiveSortKey)
    {
        var method =
            typeof(DynamoQueryTranslationPostprocessor).GetMethod(
                "ValidateOrderByConstraints",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "ValidateOrderByConstraints not found on DynamoQueryTranslationPostprocessor.");

        try
        {
            method.Invoke(null, [selectExpression, queryConstraints, effectiveSortKey]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Unwrap so FluentAssertions sees the real exception type.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
