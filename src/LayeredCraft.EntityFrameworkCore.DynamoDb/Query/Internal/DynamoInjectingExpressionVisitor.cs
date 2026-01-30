using System.Collections.Generic;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Injects Dictionary&lt;string, AttributeValue&gt; parameter handling into the expression tree.
/// This visitor runs BEFORE InjectStructuralTypeMaterializers to prepare the expression tree
/// by adding null-checking and casting for structural types.
/// Similar to JObjectInjectingExpressionVisitor (Cosmos) and BsonDocumentInjectingExpressionVisitor (MongoDB).
/// </summary>
public class DynamoInjectingExpressionVisitor(ParameterExpression itemParameter) : ExpressionVisitor
{
    private readonly ParameterExpression _itemParameter = itemParameter;
}
