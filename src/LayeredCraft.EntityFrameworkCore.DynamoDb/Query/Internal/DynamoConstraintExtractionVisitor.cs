using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Walks a finalized <see cref="SelectExpression"/> predicate and orderings to extract structural
/// key-condition constraints into a <see cref="DynamoQueryConstraints"/> snapshot.
/// </summary>
/// <remarks>
/// This visitor is single-use and stateful. Construct a new instance per query. It does NOT
/// extend <see cref="SqlExpressionVisitor"/> because that is a transform visitor; this visitor
/// is read-only and uses recursive private methods.
/// </remarks>
internal sealed class DynamoConstraintExtractionVisitor
{
    // The set of partition-key attribute names across all candidates. Used to separate
    // PK equality constraints (which belong in EqualityConstraints / InConstraints) from
    // SK candidates (SkKeyConditions). DynamoDB attribute names are case-sensitive, so
    // Ordinal comparison is required throughout.
    // An attribute that also appears in _skAttributeNames plays dual roles across different
    // GSIs (e.g. PK in one index, SK in another); such attributes can participate in both
    // equality constraints and SK key-condition candidates.
    private readonly HashSet<string> _pkAttributeNames;

    // The set of sort-key attribute names across all candidates. Only properties that appear
    // as an actual sort key on at least one descriptor are recorded as SK candidates. This
    // prevents filter-only predicates on unrelated attributes from polluting SkKeyConditions.
    // Note: an attribute may also appear in _pkAttributeNames when it is a PK on a different
    // candidate index.
    private readonly HashSet<string> _skAttributeNames;

    private readonly Dictionary<string, SqlExpression> _equalityConstraints =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<SqlExpression>> _inConstraints =
        new(StringComparer.Ordinal);

    // Tracks per-property SK candidates. A slot with Count > 1 has been "demoted" (multiple
    // conflicting conditions on the same SK column) and its Constraint is set to null.
    private readonly Dictionary<string, SkConditionSlot> _skCandidates =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> _orderingPropertyNames = new(StringComparer.Ordinal);

    private bool _hasUnsafeOr;

    /// <summary>
    /// Initializes a new instance of <see cref="DynamoConstraintExtractionVisitor"/> scoped to
    /// the given candidate descriptors.
    /// </summary>
    /// <param name="candidates">
    /// The runtime index candidates for the query. Used to build the set of partition-key
    /// attribute names so the visitor can distinguish PK constraints from SK conditions.
    /// </param>
    public DynamoConstraintExtractionVisitor(IReadOnlyList<DynamoIndexDescriptor> candidates)
    {
        _pkAttributeNames = new HashSet<string>(StringComparer.Ordinal);
        _skAttributeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var descriptor in candidates)
        {
            _pkAttributeNames.Add(descriptor.PartitionKeyProperty.GetAttributeName());
            if (descriptor.SortKeyProperty is { } sk)
                _skAttributeNames.Add(sk.GetAttributeName());
        }
    }

    /// <summary>
    /// Extracts key-condition constraints from the <paramref name="selectExpression"/> predicate
    /// and orderings.
    /// </summary>
    /// <param name="selectExpression">The finalized SELECT expression to analyze.</param>
    /// <returns>
    /// A <see cref="DynamoQueryConstraints"/> snapshot containing all extracted constraints.
    /// </returns>
    public DynamoQueryConstraints Extract(SelectExpression selectExpression)
    {
        if (selectExpression.Predicate is not null)
            WalkConjunct(selectExpression.Predicate);

        foreach (var ordering in selectExpression.Orderings)
            if (ordering.Expression is SqlPropertyExpression prop)
                _orderingPropertyNames.Add(prop.PropertyName);

        // Build SkKeyConditions: only slots with exactly one constraint survive (Count == 1).
        // Multiple constraints on the same SK column demote the slot (Count > 1, Constraint null).
        var skKeyConditions = new Dictionary<string, SkConstraint>(StringComparer.Ordinal);
        foreach (var (propName, slot) in _skCandidates)
            if (slot is { Count: 1, Constraint: not null })
                skKeyConditions[propName] = slot.Constraint;

        // Wrap mutable list values as IReadOnlyList<SqlExpression>
        var inConstraints = new Dictionary<string, IReadOnlyList<SqlExpression>>(
            StringComparer.Ordinal);
        foreach (var (propName, values) in _inConstraints)
            inConstraints[propName] = values;

        return new DynamoQueryConstraints(
            EqualityConstraints: _equalityConstraints,
            InConstraints: inConstraints,
            SkKeyConditions: skKeyConditions,
            HasUnsafeOr: _hasUnsafeOr,
            OrderingPropertyNames: _orderingPropertyNames);
    }

    // ── recursive conjunct walker ──────────────────────────────────────────────

    /// <summary>
    /// Recursively walks a predicate node, dispatching to specialized extraction methods
    /// for each supported expression shape.
    /// </summary>
    private void WalkConjunct(SqlExpression node)
    {
        switch (node)
        {
            case SqlParenthesizedExpression { Operand: var operand }:
                WalkConjunct(operand);
                break;

            case SqlBinaryExpression { OperatorType: ExpressionType.AndAlso } and:
                WalkConjunct(and.Left);
                WalkConjunct(and.Right);
                break;

            case SqlBinaryExpression { OperatorType: ExpressionType.OrElse } or:
                ClassifyOr(or);
                break;

            case SqlBinaryExpression { OperatorType: ExpressionType.Equal } eq:
                TryExtractEquality(eq);
                break;

            case SqlBinaryExpression range when IsRangeOperator(range.OperatorType):
                TryExtractRange(range);
                break;

            case SqlBetweenExpression between:
                TryExtractBetween(between);
                break;

            case SqlFunctionExpression { Name: "begins_with" } fn:
                TryExtractBeginsWith(fn);
                break;

            case SqlInExpression inExpr:
                TryExtractIn(inExpr);
                break;

            // All other nodes (NOT, IS NULL, custom functions, etc.) are filter predicates
            // that do not contribute to key-condition constraints — safely ignored.
        }
    }

    // ── OR classification ──────────────────────────────────────────────────────

    /// <summary>
    /// Classifies an OR expression as either a safe multi-value PK expansion (converted to an
    /// <c>IN</c> constraint) or an unsafe OR that prevents key-condition queries.
    /// </summary>
    /// <remarks>
    /// An OR is considered safe only when every branch is a <em>plain</em> PK equality on the
    /// same attribute (<c>PK = "a" OR PK = "b"</c> → <c>IN</c> constraint). A filter-only OR
    /// where no branch touches the PK at all is also safe. All other shapes — including
    /// conjunctive branches such as <c>(PK = "a" AND SK &gt; "1") OR (PK = "b" AND SK &gt; "2")</c>
    /// — set <see cref="_hasUnsafeOr"/> because the per-branch SK conditions cannot be reduced
    /// to a single key-condition query against one index.
    /// </remarks>
    private void ClassifyOr(SqlBinaryExpression orExpr)
    {
        var branches = FlattenOrChain(orExpr);

        // Collect the plain PK equality attribute name for each branch.
        // Returns null when the branch is not a plain PK equality (e.g. a conjunction or
        // non-PK predicate).
        var pkNames = branches
            .Select(GetSinglePkEqualityPropertyName)
            .ToList();

        // A branch "touches PK" if it has a plain PK equality, OR if it is a conjunctive
        // expression that references a PK attribute somewhere inside it.
        var anyHasPk = pkNames.Any(name => name is not null)
            || branches.Any(BranchTouchesPk);

        // Filter-only OR (no branch touches PK in any form): safe, no action needed.
        if (!anyHasPk)
            return;

        // allSamePk requires every branch to be a plain PK equality on the same attribute.
        // Conjunctive branches (pkName == null even though BranchTouchesPk is true) make this
        // false, correctly routing them to the unsafe path.
        var firstPk = pkNames.FirstOrDefault(name => name is not null);
        var allSamePk = firstPk is not null && pkNames.All(name => name == firstPk);

        if (allSamePk)
        {
            // All branches are PK equalities on the same attribute — safe multi-value IN shape.
            foreach (var branch in branches)
            {
                var value = ExtractEqualityValue(branch, firstPk!);
                if (value is not null)
                    AddToInConstraints(firstPk!, value);
            }
        }
        else
        {
            // Conjunctive branches, mixed PK/non-PK branches, or different PK attributes — unsafe.
            _hasUnsafeOr = true;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="branch"/> contains a reference to any PK
    /// attribute anywhere in its subtree, regardless of expression shape.
    /// </summary>
    /// <remarks>
    /// Used to detect conjunctive OR branches such as <c>(PK = "a" AND SK &gt; "1")</c> that
    /// touch the PK but are not plain PK equalities. This also unwraps unary NOT nodes so
    /// negated PK predicates (for example <c>NOT(PK = "x")</c>) are treated as PK-touching.
    /// </remarks>
    private bool BranchTouchesPk(SqlExpression branch)
    {
        while (branch is SqlParenthesizedExpression paren)
            branch = paren.Operand;

        return branch switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.AndAlso } and =>
                BranchTouchesPk(and.Left) || BranchTouchesPk(and.Right),
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } or =>
                BranchTouchesPk(or.Left) || BranchTouchesPk(or.Right),
            SqlUnaryExpression unary => BranchTouchesPk(unary.Operand),
            // IS NULL / IS MISSING predicates still reference the tested attribute, so OR
            // classification must treat them as PK-touching when the operand is a PK property.
            SqlIsNullExpression { Operand: SqlPropertyExpression operand } => _pkAttributeNames
                .Contains(operand.PropertyName),
            SqlBinaryExpression bin =>
                (bin.Left is SqlPropertyExpression lp && _pkAttributeNames.Contains(lp.PropertyName))
                || (bin.Right is SqlPropertyExpression rp && _pkAttributeNames.Contains(rp.PropertyName)),
            SqlInExpression inExpr =>
                inExpr.Item is SqlPropertyExpression ip && _pkAttributeNames.Contains(ip.PropertyName),
            // begins_with(PK, ...) — the first argument is the attribute being tested.
            SqlFunctionExpression fn =>
                fn.Arguments.Count > 0
                && fn.Arguments[0] is SqlPropertyExpression fp
                && _pkAttributeNames.Contains(fp.PropertyName),
            // PK BETWEEN low AND high — Subject is the attribute.
            SqlBetweenExpression between =>
                between.Subject is SqlPropertyExpression sp && _pkAttributeNames.Contains(sp.PropertyName),
            _ => false,
        };
    }

    /// <summary>
    /// Flattens a binary OR chain into a flat list of branches, unwrapping parenthesized
    /// expressions at each level.
    /// </summary>
    private static List<SqlExpression> FlattenOrChain(SqlBinaryExpression orExpr)
    {
        var branches = new List<SqlExpression>();
        var stack = new Stack<SqlExpression>();
        stack.Push(orExpr);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            // Unwrap parentheses before checking for further OR nesting.
            while (current is SqlParenthesizedExpression paren)
                current = paren.Operand;

            if (current is SqlBinaryExpression { OperatorType: ExpressionType.OrElse } nested)
            {
                stack.Push(nested.Right);
                stack.Push(nested.Left);
            }
            else
            {
                branches.Add(current);
            }
        }

        return branches;
    }

    /// <summary>
    /// Returns the PK attribute name if <paramref name="branch"/> is a plain equality with a
    /// <see cref="SqlPropertyExpression"/> on either side and that property is a PK attribute;
    /// otherwise returns <c>null</c>.
    /// </summary>
    private string? GetSinglePkEqualityPropertyName(SqlExpression branch)
    {
        // Unwrap parentheses
        while (branch is SqlParenthesizedExpression paren)
            branch = paren.Operand;

        if (branch is not SqlBinaryExpression { OperatorType: ExpressionType.Equal } eq)
            return null;

        // Reject attribute-to-attribute comparisons: DynamoDB key conditions require a
        // constant or parameter on the non-key side, never another attribute column.
        if (eq.Left is SqlPropertyExpression leftProp
            && eq.Right is not SqlPropertyExpression
            && _pkAttributeNames.Contains(leftProp.PropertyName))
            return leftProp.PropertyName;

        if (eq.Right is SqlPropertyExpression rightProp
            && eq.Left is not SqlPropertyExpression
            && _pkAttributeNames.Contains(rightProp.PropertyName))
            return rightProp.PropertyName;

        return null;
    }

    /// <summary>
    /// Extracts the non-property side of an equality binary expression for the given
    /// <paramref name="propName"/>, or <c>null</c> if the expression shape does not match.
    /// </summary>
    private static SqlExpression? ExtractEqualityValue(SqlExpression branch, string propName)
    {
        while (branch is SqlParenthesizedExpression paren)
            branch = paren.Operand;

        if (branch is not SqlBinaryExpression { OperatorType: ExpressionType.Equal } eq)
            return null;

        // Reject attribute-to-attribute comparisons — the value side must not itself be a
        // property reference (DynamoDB key conditions compare against constants/parameters only).
        if (eq.Left is SqlPropertyExpression lp
            && lp.PropertyName == propName
            && eq.Right is not SqlPropertyExpression)
            return eq.Right;

        if (eq.Right is SqlPropertyExpression rp
            && rp.PropertyName == propName
            && eq.Left is not SqlPropertyExpression)
            return eq.Left;

        return null;
    }

    // ── individual constraint extractors ──────────────────────────────────────

    /// <summary>
    /// Extracts a PK equality into <see cref="_equalityConstraints"/> or an SK equality into
    /// <see cref="_skCandidates"/>.
    /// </summary>
    private void TryExtractEquality(SqlBinaryExpression eq)
    {
        if (!TryGetPropertyAndValue(eq.Left, eq.Right, out var propName, out var value))
            return;

        if (_pkAttributeNames.Contains(propName))
        {
            // Last-wins for duplicate PK equality conjuncts (logical contradiction at user level).
            _equalityConstraints[propName] = value;

            // If this attribute is also an SK on at least one candidate index, record it as an
            // SK candidate too. An attribute can play different roles across GSIs (e.g. PK in
            // one, SK in another), and the index selector needs an SK constraint to match the
            // index where it acts as a sort key.
            if (_skAttributeNames.Contains(propName))
                RecordSkCandidate(propName, new SkConstraint(SkOperator.Equal, value));
        }
        else
        {
            RecordSkCandidate(propName, new SkConstraint(SkOperator.Equal, value));
        }
    }

    /// <summary>
    /// Extracts a range comparison (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>) as an SK
    /// candidate. Range conditions on PK attributes are not valid key conditions and are ignored.
    /// </summary>
    private void TryExtractRange(SqlBinaryExpression range)
    {
        SqlExpression propExpr;
        SqlExpression valueExpr;
        ExpressionType op;

        if (range.Left is SqlPropertyExpression && range.Right is not SqlPropertyExpression)
        {
            propExpr = range.Left;
            valueExpr = range.Right;
            op = range.OperatorType;
        }
        else if (range.Right is SqlPropertyExpression && range.Left is not SqlPropertyExpression)
        {
            // Property is on the right: flip the operator direction.
            propExpr = range.Right;
            valueExpr = range.Left;
            op = FlipOperator(range.OperatorType);
        }
        else
        {
            // Both sides are properties (attribute-to-attribute) or neither is: not a valid
            // DynamoDB key condition.
            return;
        }

        var propName = ((SqlPropertyExpression)propExpr).PropertyName;

        // Range conditions on PK are not valid key conditions (DynamoDB only supports equality
        // on PK); treat as filter predicates. Exception: if this attribute is also an SK on
        // another candidate index, it can carry a valid range condition for that index.
        if (_pkAttributeNames.Contains(propName) && !_skAttributeNames.Contains(propName))
            return;

        var skOp = MapToSkOperator(op);
        RecordSkCandidate(propName, new SkConstraint(skOp, valueExpr));
    }

    /// <summary>
    /// Extracts a <c>BETWEEN</c> expression as an SK candidate when the subject is a property.
    /// </summary>
    private void TryExtractBetween(SqlBetweenExpression between)
    {
        if (between.Subject is not SqlPropertyExpression prop)
            return;

        // DynamoDB key conditions compare the key attribute against constants/parameters only.
        // Attribute bounds such as `SK BETWEEN OtherLow AND OtherHigh` are filter predicates.
        if (between.Low is SqlPropertyExpression || between.High is SqlPropertyExpression)
            return;

        // BETWEEN on PK is not a valid DynamoDB key condition — treat as filter. Exception:
        // if this attribute is also an SK on another candidate index, allow it through.
        if (_pkAttributeNames.Contains(prop.PropertyName)
            && !_skAttributeNames.Contains(prop.PropertyName))
            return;

        RecordSkCandidate(
            prop.PropertyName,
            new SkConstraint(SkOperator.Between, between.Low, between.High));
    }

    /// <summary>
    /// Extracts a <c>begins_with(prop, prefix)</c> function call as an SK candidate.
    /// </summary>
    private void TryExtractBeginsWith(SqlFunctionExpression fn)
    {
        if (fn.Arguments.Count < 2 || fn.Arguments[0] is not SqlPropertyExpression prop)
            return;

        // begins_with on PK is not a valid DynamoDB key condition. Exception: if this attribute
        // is also an SK on another candidate index, allow it through.
        if (_pkAttributeNames.Contains(prop.PropertyName)
            && !_skAttributeNames.Contains(prop.PropertyName))
            return;

        // DynamoDB key conditions do not allow attribute-to-attribute comparison; a prefix
        // expression that is another property must be treated as a filter predicate.
        if (fn.Arguments[1] is SqlPropertyExpression)
            return;

        RecordSkCandidate(
            prop.PropertyName,
            new SkConstraint(SkOperator.BeginsWith, fn.Arguments[1]));
    }

    /// <summary>
    /// Extracts an <c>IN</c> expression on a PK attribute into <see cref="_inConstraints"/>.
    /// IN on non-PK attributes is a filter predicate and is ignored.
    /// </summary>
    private void TryExtractIn(SqlInExpression inExpr)
    {
        if (inExpr.Item is not SqlPropertyExpression prop)
            return;

        if (!_pkAttributeNames.Contains(prop.PropertyName))
            return;

        if (inExpr.Values is { } values)
        {
            foreach (var value in values)
                AddToInConstraints(prop.PropertyName, value);
        }
        else if (inExpr.ValuesParameter is { } valuesParameter)
        {
            // Runtime collection parameter: the analyzer knows an IN exists even without
            // static values. Store the parameter expression as the single entry.
            AddToInConstraints(prop.PropertyName, valuesParameter);
        }
    }

    // ── SK candidate recording ─────────────────────────────────────────────────

    /// <summary>
    /// Records or demotes an SK candidate constraint for the given property. A property that
    /// receives more than one constraint is demoted (slot Count incremented, Constraint nulled)
    /// so it cannot satisfy a single key-condition requirement.
    /// </summary>
    private void RecordSkCandidate(string propName, SkConstraint constraint)
    {
        // An attribute that is a PK on every candidate but never an SK cannot be an SK candidate.
        // If it is also an SK on at least one candidate index, allow it through — it plays dual
        // roles across different GSIs.
        if (_pkAttributeNames.Contains(propName) && !_skAttributeNames.Contains(propName))
            return;

        // Only properties that appear as an actual sort key on at least one candidate descriptor
        // are eligible for key-condition classification. Predicates on unrelated attributes are
        // filter conditions and must not appear in SkKeyConditions.
        if (!_skAttributeNames.Contains(propName))
            return;

        if (!_skCandidates.TryGetValue(propName, out var existing))
        {
            _skCandidates[propName] = new SkConditionSlot(1, constraint);
        }
        else
        {
            // More than one constraint on the same SK property — demote.
            _skCandidates[propName] = existing with { Count = existing.Count + 1, Constraint = null };
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="value"/> to the in-constraint list for <paramref name="propName"/>,
    /// creating the list on first use.
    /// </summary>
    private void AddToInConstraints(string propName, SqlExpression value)
    {
        if (!_inConstraints.TryGetValue(propName, out var list))
        {
            list = [];
            _inConstraints[propName] = list;
        }

        list.Add(value);
    }

    /// <summary>
    /// Tries to extract a <c>(propertyName, valueExpression)</c> pair from either operand
    /// order of a binary expression.
    /// </summary>
    /// <remarks>
    /// Returns <c>false</c> when both sides are <see cref="SqlPropertyExpression"/>. DynamoDB
    /// key conditions require a constant or parameter on the value side; attribute-to-attribute
    /// comparisons such as <c>TenantId = CustomerId</c> are not valid key conditions and must
    /// not be recorded as constraints.
    /// </remarks>
    private static bool TryGetPropertyAndValue(
        SqlExpression left,
        SqlExpression right,
        out string propName,
        out SqlExpression value)
    {
        if (left is SqlPropertyExpression lp && right is not SqlPropertyExpression)
        {
            propName = lp.PropertyName;
            value = right;
            return true;
        }

        if (right is SqlPropertyExpression rp && left is not SqlPropertyExpression)
        {
            propName = rp.PropertyName;
            value = left;
            return true;
        }

        propName = default!;
        value = default!;
        return false;
    }

    /// <summary>Returns <c>true</c> when <paramref name="op"/> is a relational range operator.</summary>
    private static bool IsRangeOperator(ExpressionType op)
        => op is ExpressionType.LessThan
            or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan
            or ExpressionType.GreaterThanOrEqual;

    /// <summary>
    /// Flips the direction of a relational operator. Used when the property expression is on
    /// the right side of the binary node (e.g. <c>value &gt; prop</c> → <c>prop &lt; value</c>).
    /// </summary>
    private static ExpressionType FlipOperator(ExpressionType op)
        => op switch
        {
            ExpressionType.LessThan           => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual    => ExpressionType.GreaterThanOrEqual,
            ExpressionType.GreaterThan        => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };

    /// <summary>Maps a <see cref="ExpressionType"/> range operator to the corresponding <see cref="SkOperator"/>.</summary>
    private static SkOperator MapToSkOperator(ExpressionType op)
        => op switch
        {
            ExpressionType.Equal              => SkOperator.Equal,
            ExpressionType.LessThan           => SkOperator.LessThan,
            ExpressionType.LessThanOrEqual    => SkOperator.LessThanOrEqual,
            ExpressionType.GreaterThan        => SkOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => SkOperator.GreaterThanOrEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };

    // ── nested types ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks the number of times a sort-key property has been seen and the single constraint
    /// associated with it. When <c>Count &gt; 1</c>, the constraint is <c>null</c> (demoted).
    /// </summary>
    private sealed record SkConditionSlot(int Count, SkConstraint? Constraint);
}
