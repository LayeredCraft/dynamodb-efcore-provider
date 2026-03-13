using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for <c>DynamoConstraintExtractionVisitor</c>.</summary>
public class DynamoConstraintExtractionVisitorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal substituted <c>IReadOnlyProperty</c> whose
    /// <c>IReadOnlyProperty.Name</c> returns <paramref name="attributeName"/>.
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
    /// Builds a minimal <c>DynamoIndexDescriptor</c> with the given partition- and
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
    /// Runs the visitor against a <c>SelectExpression</c> whose predicate is set to
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void UnsafeOr_MixedPkAndNonPk_SetsHasUnsafeOrTrue()
    {
        // WHERE PK = "x" OR NonKey = "y"
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = Or(BinEq(Prop("PK"), Const("x")), BinEq(Prop("NonKey"), Const("y")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void OrderingProperty_PopulatesOrderingPropertyNames()
    {
        // No predicate; ORDER BY SK ASC
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var orderings = new[] { new OrderingExpression(Prop("SK"), isAscending: true) };

        var result = Extract(null, candidates, orderings);

        result.OrderingPropertyNames.Should().Contain("SK");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ParenthesizedPkEquality_IsUnwrappedAndExtracted()
    {
        // ((PK = "foo"))
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = new SqlParenthesizedExpression(
            new SqlParenthesizedExpression(BinEq(Prop("PK"), Const("foo"))));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void OrBranch_WithPkBeginsWithAndNonPk_SetsHasUnsafeOrTrue()
    {
        // begins_with(PK, "prefix") OR NonKey = "z"
        // The first branch is a SqlFunctionExpression that touches PK — BranchTouchesPk must
        // detect it and mark the OR unsafe.
        var candidates = new[] { MakeDescriptor("PK") };
        var fn = new SqlFunctionExpression(
            "begins_with",
            [Prop("PK"), Const("prefix")],
            typeof(bool),
            null);
        var predicate = Or(fn, BinEq(Prop("NonKey"), Const("z")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void OrBranch_WithPkBetweenAndNonPk_SetsHasUnsafeOrTrue()
    {
        // (PK BETWEEN "a" AND "z") OR NonKey = "z"
        // SqlBetweenExpression with Subject=PK must be detected by BranchTouchesPk.
        var candidates = new[] { MakeDescriptor("PK") };
        var between = new SqlBetweenExpression(Prop("PK"), Const("a"), Const("z"));
        var predicate = Or(between, BinEq(Prop("NonKey"), Const("z")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void OrBranch_WithPkIsNullAndNonPk_SetsHasUnsafeOrTrue()
    {
        // PK IS NULL OR NonKey = "y"
        // SqlIsNullExpression still touches PK and must not be treated as a filter-only OR.
        var candidates = new[] { MakeDescriptor("PK") };
        var pkIsNull = new SqlIsNullExpression(Prop("PK"), IsNullOperator.IsNull);
        var predicate = Or(pkIsNull, BinEq(Prop("NonKey"), Const("y")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void OrBranch_WithNegatedPkEqualityAndNonPk_SetsHasUnsafeOrTrue()
    {
        // NOT(PK = "x") OR NonKey = "y"
        // A negated PK branch still touches PK and cannot participate in a safe PK-IN rewrite.
        var candidates = new[] { MakeDescriptor("PK") };
        var negatedPk = new SqlUnaryExpression(ExpressionType.Not, BinEq(Prop("PK"), Const("x")));
        var predicate = Or(negatedPk, BinEq(Prop("NonKey"), Const("y")));

        var result = Extract(predicate, candidates);

        result.HasUnsafeOr.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void AttributeToAttributeEquality_NotExtractedToEqualityConstraints()
    {
        // WHERE PK = OtherColumn  — both sides are properties: not a valid key condition.
        var candidates = new[] { MakeDescriptor("PK") };
        var predicate = BinEq(Prop("PK"), Prop("OtherColumn"));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().BeEmpty();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void AttributeToAttributeRange_NotExtractedToSkKeyConditions()
    {
        // WHERE PK = "x" AND SK > OtherColumn — value side is an attribute: not a valid key condition.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = And(
            BinEq(Prop("PK"), Const("x")),
            BinOp(ExpressionType.GreaterThan, Prop("SK"), Prop("OtherColumn")));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
        result.SkKeyConditions.Should().BeEmpty();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void BeginsWith_WithAttributePrefix_NotExtractedToSkKeyConditions()
    {
        // WHERE PK = "x" AND begins_with(SK, OtherColumn)
        // The prefix side is another attribute and must not be treated as a key condition value.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var beginsWith = new SqlFunctionExpression(
            "begins_with",
            [Prop("SK"), Prop("OtherColumn")],
            typeof(bool),
            null);
        var predicate = And(BinEq(Prop("PK"), Const("x")), beginsWith);

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
        result.SkKeyConditions.Should().BeEmpty();
    }

    // ── cross-role attribute tests (PK in one index, SK in another) ───────────

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void CrossRole_RangeOnDualRoleAttribute_RecordedAsSkKeyCondition()
    {
        // GSI-A: PK=Status, SK=CreatedAt
        // GSI-B: PK=CreatedAt, SK=Type
        // WHERE Status = "Open" AND CreatedAt > "2024-01-01"
        // CreatedAt is a PK on GSI-B but also an SK on GSI-A — the range condition must
        // be recorded so the index selector can match GSI-A.
        var candidates = new[]
        {
            MakeDescriptor("Status", "CreatedAt", "gsi-a"),
            MakeDescriptor("CreatedAt", "Type", "gsi-b"),
        };
        var predicate = And(
            BinEq(Prop("Status"), Const("Open")),
            BinOp(ExpressionType.GreaterThan, Prop("CreatedAt"), Const("2024-01-01")));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("Status");
        result.SkKeyConditions.Should().ContainKey("CreatedAt");
        result.SkKeyConditions["CreatedAt"].Operator.Should().Be(SkOperator.GreaterThan);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void CrossRole_EqualityOnDualRoleAttribute_RecordedInBothEqualityAndSk()
    {
        // GSI-A: PK=Status, SK=CreatedAt
        // GSI-B: PK=CreatedAt, SK=Type
        // WHERE CreatedAt = "2024-01-01"
        // CreatedAt equality must go to both EqualityConstraints (for GSI-B as PK) and
        // SkKeyConditions (for GSI-A as SK).
        var candidates = new[]
        {
            MakeDescriptor("Status", "CreatedAt", "gsi-a"),
            MakeDescriptor("CreatedAt", "Type", "gsi-b"),
        };
        var predicate = BinEq(Prop("CreatedAt"), Const("2024-01-01"));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("CreatedAt");
        result.SkKeyConditions.Should().ContainKey("CreatedAt");
        result.SkKeyConditions["CreatedAt"].Operator.Should().Be(SkOperator.Equal);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void CrossRole_BetweenOnDualRoleAttribute_RecordedAsSkKeyCondition()
    {
        // GSI-A: PK=Status, SK=CreatedAt
        // GSI-B: PK=CreatedAt, SK=Type
        // WHERE Status = "Open" AND CreatedAt BETWEEN "2024-01-01" AND "2024-12-31"
        var candidates = new[]
        {
            MakeDescriptor("Status", "CreatedAt", "gsi-a"),
            MakeDescriptor("CreatedAt", "Type", "gsi-b"),
        };
        var between =
            new SqlBetweenExpression(Prop("CreatedAt"), Const("2024-01-01"), Const("2024-12-31"));
        var predicate = And(BinEq(Prop("Status"), Const("Open")), between);

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("Status");
        result.SkKeyConditions.Should().ContainKey("CreatedAt");
        result.SkKeyConditions["CreatedAt"].Operator.Should().Be(SkOperator.Between);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void Between_WithAttributeBounds_NotExtractedToSkKeyConditions()
    {
        // WHERE PK = "x" AND SK BETWEEN OtherLow AND OtherHigh
        // Attribute bounds are not valid DynamoDB key-condition values and must stay filters.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var between = new SqlBetweenExpression(Prop("SK"), Prop("OtherLow"), Prop("OtherHigh"));
        var predicate = And(BinEq(Prop("PK"), Const("x")), between);

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("PK");
        result.SkKeyConditions.Should().BeEmpty();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void CrossRole_BeginsWithOnDualRoleAttribute_RecordedAsSkKeyCondition()
    {
        // GSI-A: PK=Status, SK=Prefix
        // GSI-B: PK=Prefix, SK=Type
        // WHERE Status = "Open" AND begins_with(Prefix, "ABC")
        var candidates = new[]
        {
            MakeDescriptor("Status", "Prefix", "gsi-a"),
            MakeDescriptor("Prefix", "Type", "gsi-b"),
        };
        var fn = new SqlFunctionExpression(
            "begins_with",
            [Prop("Prefix"), Const("ABC")],
            typeof(bool),
            null);
        var predicate = And(BinEq(Prop("Status"), Const("Open")), fn);

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().ContainKey("Status");
        result.SkKeyConditions.Should().ContainKey("Prefix");
        result.SkKeyConditions["Prefix"].Operator.Should().Be(SkOperator.BeginsWith);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void PurelyPkAttribute_RangeCondition_StillDropped()
    {
        // Regression: a pure-PK attribute (not an SK on any index) must still be blocked
        // from SK recording after the cross-role fix.
        // WHERE PK > "x"  — range on a PK that is never an SK → not a valid key condition.
        var candidates = new[] { MakeDescriptor("PK", "SK") };
        var predicate = BinOp(ExpressionType.GreaterThan, Prop("PK"), Const("x"));

        var result = Extract(predicate, candidates);

        result.EqualityConstraints.Should().BeEmpty();
        result.SkKeyConditions.Should().BeEmpty();
    }
}
