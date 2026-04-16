using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;


[DynamoMapper]
[DynamoField(nameof(QuestionItem.Gs1Pk), AttributeName = "gs1-pk")]
[DynamoField(nameof(QuestionItem.Gs1Sk), AttributeName = "gs1-sk")]
[DynamoField(nameof(QuestionItem.Gs2Pk), AttributeName = "gs2-pk")]
[DynamoField(nameof(QuestionItem.Gs2Sk), AttributeName = "gs2-sk")]
[DynamoIgnore("IsQuestionRecordType")]
internal static partial class NamingConventionsItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(QuestionItem source);
    internal static partial QuestionItem FromItem(Dictionary<string, AttributeValue> item);
}
