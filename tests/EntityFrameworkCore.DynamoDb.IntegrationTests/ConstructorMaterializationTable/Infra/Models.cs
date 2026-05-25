using System.ComponentModel.DataAnnotations.Schema;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ConstructorMaterializationTable;

public sealed class ConstructorBlog
{
    public ConstructorBlog(string pk, string title, int? monthlyRevenue = null)
    {
        Pk = pk;
        Title = title;
        MonthlyRevenue = monthlyRevenue;
        ConstructorUsed = true;
    }

    public string Pk { get; private set; }

    public string Title { get; private set; }

    public int? MonthlyRevenue { get; set; }

    public List<ConstructorPost> Posts { get; private set; } = [];

    [NotMapped]
    public bool ConstructorUsed { get; }
}

public sealed class ConstructorPost
{
    private ConstructorPost(string title, string content)
    {
        Title = title;
        Content = content;
        ConstructorUsed = true;
    }

    public static ConstructorPost Create(string title, string content) => new(title, content);

    public string Title { get; private set; }

    public string Content { get; set; }

    [NotMapped]
    public bool ConstructorUsed { get; }
}
