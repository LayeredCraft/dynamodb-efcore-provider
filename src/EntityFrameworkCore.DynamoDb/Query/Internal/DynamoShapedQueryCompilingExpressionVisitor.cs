using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

#pragma warning disable EF9100

/// <summary>Represents the DynamoShapedQueryCompilingExpressionVisitor type.</summary>
public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory) : ShapedQueryCompilingExpressionVisitor(
    dependencies,
    dynamoQueryCompilationContext)
{
    private static readonly ValueTypeMemberAccessRewritingVisitor ValueTypeRewriter = new();

    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;

    private int _runtimeParameterIndex;

    private static readonly MethodInfo EnsurePositiveLimitMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(EnsurePositiveLimit))!;

    /// <summary>Builds the runtime querying enumerable and shaper for a translated DynamoDB query.</summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;
        var pagingExpression = shapedQueryExpression.ShaperExpression as DynamoPagingExpression;
        var itemShaperExpression =
            pagingExpression?.InnerShaper ?? shapedQueryExpression.ShaperExpression;

        // Discriminator-predicate finalisation, projection finalisation, and index selection are
        // performed earlier in the pipeline by DynamoQueryTranslationPostprocessor so the analyzer
        // sees the complete predicate tree and projection shape. By this point SelectExpression
        // already has IndexName set (or null for base-table queries).

        // Normalize parameterized Limit(n) expression for runtime evaluation.
        // Constant values are already inline; only runtime parameters need registration.
        if (selectExpression.LimitExpression is not null
            && selectExpression.LimitExpression is not ConstantExpression)
            selectExpression.ApplyUserLimitExpression(
                NormalizeLimitExpression(selectExpression.LimitExpression));

        var shaperBody = itemShaperExpression;

        // create shaper
        var itemParameter = Parameter(typeof(Dictionary<string, AttributeValue>), "item");

        // Step 1: Inject Dictionary<string, AttributeValue> variable handling
        // This adds null-checking and prepares the expression tree for materialization
        shaperBody = new DynamoInjectingExpressionVisitor().Visit(shaperBody);

        // Step 2: Inject EF Core's standard structural type materializers
        // This adds entity construction and property assignment logic
        shaperBody = InjectStructuralTypeMaterializers(shaperBody);

        // Step 3: Remove projection bindings and replace with actual dictionary access
        // This converts abstract ProjectionBindingExpression to concrete property access
        shaperBody = new DynamoProjectionBindingRemovingExpressionVisitor(
            itemParameter,
            selectExpression,
            dynamoQueryCompilationContext.IsPrecompiling,
            _dependencies.LiftableConstantFactory,
            QueryCompilationContext.Model).Visit(shaperBody);

        shaperBody = ValueTypeRewriter.Visit(shaperBody);

        var shaperLambda = Expression.Lambda(
            shaperBody,
            QueryCompilationContext.QueryContextParameter,
            itemParameter);

        var queryContextParameter = Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var standAloneStateManager = dynamoQueryCompilationContext.QueryTrackingBehavior
            == QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        if (pagingExpression is not null)
            return CreatePagingEnumerableExpression(
                shaperBody.Type,
                queryContextParameter,
                selectExpression,
                shaperLambda,
                standAloneStateManager);

        return dynamoQueryCompilationContext.IsPrecompiling
            ? Call(
                typeof(DynamoGeneratedQueryRuntime),
                nameof(DynamoGeneratedQueryRuntime.CreateQueryingEnumerable),
                [shaperBody.Type],
                QueryCompilationContext.QueryContextParameter,
                CreateQueryTemplateConstant(selectExpression),
                shaperLambda,
                Constant(standAloneStateManager),
                Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled))
            : New(
                typeof(QueryingEnumerable<>)
                    .MakeGenericType(shaperBody.Type)
                    .GetConstructors(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(c => c.GetParameters().Length == 6),
                queryContextParameter,
                Constant(selectExpression),
                Constant(sqlGeneratorFactory),
                shaperLambda,
                Constant(standAloneStateManager),
                Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));
    }

    private Expression
        CreatePagingEnumerableExpression(
            Type shaperType,
            UnaryExpression queryContextParameter,
            SelectExpression selectExpression,
            LambdaExpression shaperLambda,
            bool standAloneStateManager)
        => dynamoQueryCompilationContext.IsPrecompiling
            ? Call(
                typeof(DynamoGeneratedQueryRuntime),
                nameof(DynamoGeneratedQueryRuntime.CreatePagingQueryingEnumerable),
                [shaperType],
                QueryCompilationContext.QueryContextParameter,
                CreateQueryTemplateConstant(selectExpression),
                shaperLambda,
                Constant(standAloneStateManager),
                Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled))
            : New(
                typeof(PagingQueryingEnumerable<>)
                    .MakeGenericType(shaperType)
                    .GetConstructors(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(c => c.GetParameters().Length == 6),
                queryContextParameter,
                Constant(selectExpression),
                Constant(sqlGeneratorFactory),
                shaperLambda,
                Constant(standAloneStateManager),
                Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled));

#pragma warning disable EF9100
    private Expression CreateQueryTemplateConstant(SelectExpression selectExpression)
    {
        var template = sqlGeneratorFactory.Create().GeneratePrecompiledTemplate(selectExpression);
        var context = Parameter(typeof(MaterializerLiftableConstantContext), "context");
        var resolver = Lambda<Func<MaterializerLiftableConstantContext, object>>(
            Convert(CreateQueryTemplateExpression(template, context), typeof(object)),
            context);

        return _dependencies.LiftableConstantFactory.CreateLiftableConstant(
            template,
            resolver,
            "dynamoQueryTemplate",
            typeof(DynamoGeneratedQueryRuntime.QueryTemplate));
    }

    private Expression CreateQueryTemplateExpression(
        DynamoGeneratedQueryRuntime.QueryTemplate template,
        ParameterExpression context)
    {
        var segments = template.Segments.Select(segment => segment.Kind switch
        {
            DynamoGeneratedQueryRuntime.SegmentKind.Text => Call(
                typeof(DynamoGeneratedQueryRuntime.CommandSegment),
                nameof(DynamoGeneratedQueryRuntime.CommandSegment.TextSegment),
                Type.EmptyTypes,
                Constant(segment.Text!)),
            DynamoGeneratedQueryRuntime.SegmentKind.Parameter => Call(
                typeof(DynamoGeneratedQueryRuntime.CommandSegment),
                nameof(DynamoGeneratedQueryRuntime.CommandSegment.Parameter),
                Type.EmptyTypes,
                Constant(segment.ParameterName!),
                Constant(segment.SourceType!, typeof(Type)),
                CreateTypeMappingExpression(segment.TypeMapping!, context)),
            DynamoGeneratedQueryRuntime.SegmentKind.Constant => Call(
                typeof(DynamoGeneratedQueryRuntime.CommandSegment),
                nameof(DynamoGeneratedQueryRuntime.CommandSegment.Constant),
                Type.EmptyTypes,
                Convert(Constant(segment.ConstantValue, segment.SourceType!), typeof(object)),
                Constant(segment.SourceType!, typeof(Type)),
                CreateTypeMappingExpression(segment.TypeMapping!, context)),
            DynamoGeneratedQueryRuntime.SegmentKind.Collection => Call(
                typeof(DynamoGeneratedQueryRuntime.CommandSegment),
                nameof(DynamoGeneratedQueryRuntime.CommandSegment.Collection),
                Type.EmptyTypes,
                Constant(segment.Text!),
                Constant(segment.ParameterName!),
                Constant(segment.SourceType!, typeof(Type)),
                CreateTypeMappingExpression(segment.TypeMapping!, context),
                Constant(segment.MaximumValueCount)),
            _ => throw new UnreachableException()
        });

        return Call(
            typeof(DynamoGeneratedQueryRuntime),
            nameof(DynamoGeneratedQueryRuntime.CreateQueryTemplate),
            Type.EmptyTypes,
            NewArrayInit(typeof(DynamoGeneratedQueryRuntime.CommandSegment), segments),
            Constant(template.TableName),
            Constant(template.IndexName, typeof(string)),
            Constant(template.IsGlobalSecondaryIndex),
            Constant(template.IsScanLike),
            Constant(template.ScanMessage, typeof(string)),
            Constant(template.ScanAllowed),
            Constant(template.Limit, typeof(int?)),
            Constant(template.LimitParameterName, typeof(string)),
            Constant(template.SeedNextToken, typeof(string)),
            Constant(template.SeedNextTokenParameterName, typeof(string)),
            Constant(template.ConsistentRead, typeof(bool?)),
            Constant(template.ConsistentReadParameterName, typeof(string)),
            Constant(template.HasUserLimit),
            Constant(template.IsFirstTerminal),
            Constant(template.IsSingleTerminal));
    }

    private Expression CreateTypeMappingExpression(
        DynamoTypeMapping typeMapping,
        ParameterExpression context)
    {
        var property =
            QueryCompilationContext
                .Model
                .GetEntityTypes()
                .SelectMany(static entityType => entityType.GetFlattenedProperties())
                .FirstOrDefault(property
                    => ReferenceEquals(property.GetTypeMapping(), typeMapping));

        return Call(
            typeof(DynamoGeneratedQueryRuntime),
            nameof(DynamoGeneratedQueryRuntime.ResolveTypeMapping),
            Type.EmptyTypes,
            context,
            Constant(typeMapping.ClrType, typeof(Type)),
            Constant(property?.DeclaringType.Name, typeof(string)),
            Constant(property?.Name, typeof(string)));
    }
#pragma warning restore EF9100

    /// <summary>
    ///     Normalizes a parameterized <c>Limit(n)</c> expression for runtime evaluation. Constants
    ///     are returned as-is; all other forms are registered as runtime parameters so EF Core can cache
    ///     and bind query delegates consistently across executions.
    /// </summary>
    private Expression NormalizeLimitExpression(Expression expression)
    {
        if (expression is ConstantExpression { Value: int })
            return expression;

        if (expression is QueryParameterExpression)
            return expression;

        var parameterName = $"__dynamo_limit_{_runtimeParameterIndex++}";
        var injectedExpression = new DynamoInjectingExpressionVisitor().Visit(expression)
            ?? throw new InvalidOperationException("Unable to normalize Limit expression.");

        // Runtime parameters must be int-valued before registration.
        var convertedExpression = Convert(injectedExpression, typeof(int));

        // Validate at runtime: Limit must be positive.
        var body = Call(EnsurePositiveLimitMethodInfo, convertedExpression);
        var valueExtractor = Lambda(body, QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(parameterName, valueExtractor);
    }

    /// <summary>
    ///     Emits complex property and complex collection initialization markers for each
    ///     complex property on the structural type being materialized.
    /// </summary>
    /// <remarks>
    ///     Called by <see cref="DynamoStructuralTypeMaterializerSource" /> for each structural type when
    ///     <c>ReadComplexTypeDirectly</c> returns <see langword="false" />.
    ///     The markers are later processed by
    ///     <see cref="DynamoProjectionBindingRemovingExpressionVisitor" />, which pushes the correct
    ///     nested <c>Dictionary&lt;string, AttributeValue&gt;</c> context onto the attribute stack
    ///     before visiting the injected scalar materializer.
    /// </remarks>
    public override void AddStructuralTypeInitialization(
        StructuralTypeShaperExpression shaper,
        ParameterExpression instanceVariable,
        List<ParameterExpression> variables,
        List<Expression> expressions)
    {
        foreach (var complexProperty in shaper.StructuralType.GetComplexProperties())
        {
            var member = MakeMemberAccess(
                instanceVariable,
                complexProperty.GetMemberInfo(true, true));

            if (complexProperty.IsCollection)
            {
                // Inject per-element materializer for the complex element type.
                var elementShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Constant(ValueBuffer.Empty),
                    false);
                var elementMaterializer = InjectStructuralTypeMaterializers(elementShaper);
                expressions.Add(
                    new DynamoComplexCollectionInitializationExpression(
                        complexProperty,
                        elementMaterializer,
                        member));
            }
            else
            {
                // Inject the scalar materializer for the complex type's own properties.
                // Nested complex properties within this type will recursively emit further markers
                // via AddStructuralTypeInitialization calls during injection.
                var complexShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Constant(ValueBuffer.Empty),
                    complexProperty.IsNullable);
                var injectedMaterializer = InjectStructuralTypeMaterializers(complexShaper);
                expressions.Add(
                    new DynamoComplexPropertyInitializationExpression(
                        complexProperty,
                        injectedMaterializer,
                        member));
            }
        }
    }

    /// <summary>
    ///     Rewrites member access over non-trivial value-type expressions into a temporary assignment.
    ///     Expression tree compilation rejects direct member access on some value-type expression
    ///     nodes, such as <see cref="TryExpression" /> emitted by complex struct materialization.
    /// </summary>
    private sealed class ValueTypeMemberAccessRewritingVisitor : ExpressionVisitor
    {
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (node.Test is TypeBinaryExpression
                {
                    NodeType: ExpressionType.TypeIs, Expression.Type.IsSealed: true
                } typeTest
                && !typeTest.TypeOperand.IsAssignableFrom(typeTest.Expression.Type))
                return Visit(node.IfFalse);

            return base.VisitConditional(node);
        }

        protected override Expression VisitSwitch(SwitchExpression node)
        {
            if (node.Comparison is null)
                return base.VisitSwitch(node);

            var switchValue = Visit(node.SwitchValue);
            var switchValueVariable = Variable(switchValue.Type, "switchValue");
            var defaultBody = Visit(node.DefaultBody) ?? Default(node.Type);
            var result = defaultBody;

            for (var caseIndex = node.Cases.Count - 1; caseIndex >= 0; caseIndex--)
            {
                var @case = node.Cases[caseIndex];
                var test =
                    @case
                        .TestValues
                        .Select(testValue => Equal(
                            switchValueVariable,
                            Visit(testValue),
                            false,
                            node.Comparison))
                        .Aggregate(OrElse);
                result = Condition(test, Visit(@case.Body), result);
            }

            return Block([switchValueVariable], Assign(switchValueVariable, switchValue), result);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is not { } instance)
                return base.VisitMember(node);

            var visitedInstance = Visit(instance);
            if (RequiresValueTypeInstanceMaterialization(visitedInstance))
            {
                var instanceVariable = Variable(
                    visitedInstance.Type,
                    $"valueTypeInstance_{node.Member.Name}");
                return Block(
                    [instanceVariable],
                    Assign(instanceVariable, visitedInstance),
                    MakeMemberAccess(instanceVariable, node.Member));
            }

            return visitedInstance == instance ? node : node.Update(visitedInstance);
        }

        private static bool RequiresValueTypeInstanceMaterialization(Expression instanceExpression)
            => instanceExpression.Type.IsValueType
                && instanceExpression is not ParameterExpression
                && instanceExpression is not MemberExpression
                && instanceExpression is not ConstantExpression;
    }

    /// <summary>Validates that the runtime Limit value is positive.</summary>
    private static int EnsurePositiveLimit(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException("limit", "Limit must be a positive integer.");

        return value;
    }
}
