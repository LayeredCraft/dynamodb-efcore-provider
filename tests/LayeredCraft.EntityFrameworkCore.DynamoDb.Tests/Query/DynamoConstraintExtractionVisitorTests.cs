using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for <see cref="DynamoConstraintExtractionVisitor"/>.</summary>
public class DynamoConstraintExtractionVisitorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal substituted <see cref="IReadOnlyProperty"/> whose
    /// <see cref="IReadOnlyProperty.Name"/> returns <paramref name="attributeName"/>.
    /// <c>GetAttributeName()</c> falls back to <c>property.Name</c> when the annotation is null,
    /// and NSubstitute returns null by default for un-configured interface members.
    /// </summary>
    private static IReadOnlyProperty MakeProp(string attributeName)
    {
        var prop = Substitute.For<IReadOnlyProperty>();
        prop.Name.Returns(attributeName);
        return prop;
    }

    /// <summary>
    /// Builds a minimal <see cref="DynamoIndexDescriptor"/> with the given partition- and
    /// optional sort-key attribute names.
    /// </summary>
    private static DynamoIndexDescriptor MakeDescriptor(
        string pkAttr,
        string? skAttr = null,
        string? indexName = null)
        => new(
            IndexName: indexName,
            Kind: indexName is null
                ? DynamoIndexSourceKind.Table
                : DynamoIndexSourceKind.GlobalSecondaryIndex,
            ModelIndex: null,
            PartitionKeyProperty: MakeProp(pkAttr),
            SortKeyProperty: skAttr is null ? null : MakeProp(skAttr),
            ProjectionType: DynamoSecondaryIndexProjectionType.All);

    private static SqlPropertyExpression Prop(string name)
        => new(name, typeof(string), null);

    private static SqlConstantExpression Const(object value)
        => new(value, value.GetType(), null);

    private static SqlBinaryExpression BinEq(SqlExpression left, SqlExpression right)
        => new(ExpressionType.Equal, left, right, typeof(bool), null);

    private static SqlBinaryExpression And(SqlExpression left, SqlExpression right)
        => new(ExpressionType.AndAlso, left, right, typeof(bool), null);

    private static SqlBinaryExpression Or(SqlExpression left, SqlExpression right)
        => new(ExpressionType.OrElse, left, right, typeof(bool), null);

    private static SqlBinaryExpression BinOp(
        ExpressionType op,
        SqlExpression left,
        SqlExpression right)
        => new(op, left, right, typeof(bool), null);

    /// <summary>
    /// Runs the visitor against a <see cref="SelectExpression"/> whose predicate is set to
    /// <paramref name="predicate"/>.
    /// </summary>
    private static DynamoQueryConstraints Extract(
        SqlExpression? predicate,
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        IEnumerable<OrderingExpression>? orderings = null)
    {
        var select = new SelectExpression("Table");
        if (predicate is not null)
            select.ApplyPredicate(predicate);

        if (orderings is not null)
            foreach (var ordering in orderings)
                select.AppendOrdering(ordering);

        return new DynamoConstraintExtractionVisitor(candidates).Extract(select);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NoPredicate_ReturnsEmptyConstraints()
    {
        var candidates = new[] { MakeDescriptor("PK") };

        var result = Extract(null, candidates);

        result.EqualityConstraints.Should().BeEmpty();
        result.InConstraints.Should().BeEmpty();
        result.SkKeyConditions.Should().BeEmpty();
        result.HasUnsafeOr.Should().BeFalse();
        result.OrderingPropertyNames.Should().BeEmpty();
    }

    [Fact]
    public void PkEquality_IsExtractedToEqualityConstraints()
    {
        // WHERE PK = "foo"
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = BinEq(Prop("PK"), Const("foo"));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
        result.EqualityConstraints["PK"].Should().BeOfType<SqlConstantExpression>()
            .Which.Value.Should().Be("foo");
        result.InConstraints.Should().BeEmpty();
        result.HasUnsafeOr.Should().BeFalse();
    }

    [Fact]
    public void PkIn_IsExtractedToInConstraints()
    {
        // PK IN ["a", "b"]
        var candidates = new[] { MakeDescriptor("PK") };
        var inExpr = new SqlInExpression(
            Prop("PK"),
            [Const("a"), Const("b")],
            null,
            isPartitionKeyComparison: true,
            null);

        var result = Extract(inExpr, candidates);

        result.InConstraints.Should().ContainKey("PK");
        result.InConstraints["PK"].Should().HaveCount(2);
        result.EqualityConstraints.Should().BeEmpty();
    }

    [Fact]
    public void PkAndSkEquality_BothCaptured_SkInKeyConditionsAsEqual()
    {
        // WHERE PK = "x" AND SK = "y"
        // Descriptor: PK="PK", SK="SK"
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = And(BinEq(Prop("PK"), Const("x")), BinEq(Prop("SK"), Const("y")));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
        result.SkKeyConditions.Should().ContainKey("SK");
        result.SkKeyConditions["SK"].Operator.Should().Be(SkOperator.Equal);
        result.SkKeyConditions["SK"].Low.Should().BeOfType<SqlConstantExpression>()
            .Which.Value.Should().Be("y");
    }

    [Fact]
    public void PkAndSkGreaterThan_SkInKeyConditionsAsGreaterThan()
    {
        // WHERE PK = "x" AND SK > "y"
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = And(
            BinEq(Prop("PK"), Const("x")),
            BinOp(ExpressionType.GreaterThan, Prop("SK"), Const("y")));

        var result = Extract(predicate, candidates);

        result.SkKeyConditions.Should().ContainKey("SK");
        result.SkKeyConditions["SK"].Operator.Should().Be(SkOperator.GreaterThan);
    }

    [Fact]
    public void PkAndSkBetween_SkInKeyConditionsAsBetweenWithBothBounds()
    {
        // WHERE PK = "x" AND SK BETWEEN "a" AND "z"
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var between = new SqlBetweenExpression(Prop("SK"), Const("a"), Const("z"));
        var predicate = And(BinEq(Prop("PK"), Const("x")), between);

        var result = Extract(predicate, candidates);

        result.SkKeyConditions.Should().ContainKey("SK");
        result.SkKeyConditions["SK"].Operator.Should().Be(SkOperator.Between);
        result.SkKeyConditions["SK"].Low.Should().BeOfType<SqlConstantExpression>()
            .Which.Value.Should().Be("a");
        result.SkKeyConditions["SK"].High.Should().BeOfType<SqlConstantExpression>()
            .Which.Value.Should().Be("z");
    }

    [Fact]
    public void PkAndSkBeginsWith_SkInKeyConditionsAsBeginsWith()
    {
        // WHERE PK = "x" AND begins_with(SK, "pre")
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var fn = new SqlFunctionExpression(
            "begins_with",
            [Prop("SK"), Const("pre")],
            typeof(bool),
            null);
        var predicate = And(BinEq(Prop("PK"), Const("x")), fn);

        var result = Extract(predicate, candidates);

        result.SkKeyConditions.Should().ContainKey("SK");
        result.SkKeyConditions["SK"].Operator.Should().Be(SkOperator.BeginsWith);
        result.SkKeyConditions["SK"].Low.Should().BeOfType<SqlConstantExpression>()
            .Which.Value.Should().Be("pre");
        result.SkKeyConditions["SK"].High.Should().BeNull();
    }

    [Fact]
    public void MultipleSkConditionsOnSameProperty_SkDemotedFromKeyConditions()
    {
        // WHERE PK = "x" AND SK > "a" AND SK < "z"
        // Two range conditions on SK → SK demoted from SkKeyConditions.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = And(
            And(
                BinEq(Prop("PK"), Const("x")),
                BinOp(ExpressionType.GreaterThan, Prop("SK"), Const("a"))),
            BinOp(ExpressionType.LessThan, Prop("SK"), Const("z")));

        var result = Extract(predicate, candidates);

        result.SkKeyConditions.Should().NotContainKey("SK");
        result.EqualityConstraints.Should().ContainKey("PK");
    }

    [Fact]
    public void SafeFilterOr_NonPkAttributes_HasUnsafeOrFalse_PkStillCaptured()
    {
        // WHERE PK = "x" AND (A = 1 OR B = 2)  — A and B are not PK or SK
        var candidates = new[] { MakeDescriptor("PK") };
        var orPredicate = new SqlParenthesizedExpression(
            Or(BinEq(Prop("A"), Const(1)), BinEq(Prop("B"), Const(2))));
        var predicate = And(BinEq(Prop("PK"), Const("x")), orPredicate);

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeFalse();
        result.EqualityConstraints.Should().ContainKey("PK");
    }

    [Fact]
    public void SafePkOr_AllBranchesSamePk_PopulatesInConstraints_HasUnsafeOrFalse()
    {
        // WHERE PK = "a" OR PK = "b"
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = Or(BinEq(Prop("PK"), Const("a")), BinEq(Prop("PK"), Const("b")));

        var result = Extract(predicate, candidates);

        result.InConstraints.Should().ContainKey("PK");
        result.InConstraints["PK"].Should().HaveCount(2);
        result.HasUnsafeOr.Should().BeFalse();
        result.EqualityConstraints.Should().NotContainKey("PK");
    }

    [Fact]
    public void UnsafeOr_MixedPkAndNonPk_SetsHasUnsafeOrTrue()
    {
        // WHERE PK = "x" OR NonKey = "y"
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = Or(BinEq(Prop("PK"), Const("x")), BinEq(Prop("NonKey"), Const("y")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    [Fact]
    public void NonKeyPredicateOnly_NoPkOrSkConstraints()
    {
        // WHERE NonKey = "z"
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = BinEq(Prop("NonKey"), Const("z"));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().BeEmpty();
        result.SkKeyConditions.Should().BeEmpty();
        result.HasUnsafeOr.Should().BeFalse();
    }

    [Fact]
    public void OrderingProperty_PopulatesOrderingPropertyNames()
    {
        // No predicate; ORDER BY SK ASC
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var orderings = new[] { new OrderingExpression(Prop("SK"), isAscending: true) };

        var result = Extract(null, candidates, orderings);

        result.OrderingPropertyNames.Should().Contain("SK");
    }

    [Fact]
    public void ParenthesizedPkEquality_IsUnwrappedAndExtracted()
    {
        // ((PK = "foo"))
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = new SqlParenthesizedExpression(
            new SqlParenthesizedExpression(BinEq(Prop("PK"), Const("foo"))));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
    }

    [Fact]
    public void ConjunctiveOrBranches_ContainingPk_SetsHasUnsafeOrTrue()
    {
        // (PK = "a" AND SK > "1") OR (PK = "b" AND SK > "2")
        // Each OR branch is a conjunction that touches PK, but is not a plain PK equality.
        // The SK conditions differ per branch, so this cannot be reduced to a key-condition
        // query against a single index — must be marked unsafe.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var branch1 = new SqlParenthesizedExpression(
            And(BinEq(Prop("PK"), Const("a")),
                BinOp(ExpressionType.GreaterThan, Prop("SK"), Const("1"))));
        var branch2 = new SqlParenthesizedExpression(
            And(BinEq(Prop("PK"), Const("b")),
                BinOp(ExpressionType.GreaterThan, Prop("SK"), Const("2"))));
        var predicate = Or(branch1, branch2);

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    [Fact]
    public void ReversedRangeComparison_FlipsOperatorCorrectly()
    {
        // BinOp(GreaterThan, Const("y"), Prop("SK")) → value > SK → SK < value → LessThan
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = And(
            BinEq(Prop("PK"), Const("x")),
            BinOp(ExpressionType.GreaterThan, Const("y"), Prop("SK")));

        var result = Extract(predicate, candidates);

        result.SkKeyConditions.Should().ContainKey("SK");
        result.SkKeyConditions["SK"].Operator.Should().Be(SkOperator.LessThan);
    }
}
