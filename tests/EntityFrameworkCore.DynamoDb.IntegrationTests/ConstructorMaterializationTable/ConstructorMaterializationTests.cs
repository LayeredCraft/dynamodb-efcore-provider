using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ConstructorMaterializationTable;

public sealed class ConstructorMaterializationTests(DynamoContainerFixture fixture)
    : ConstructorMaterializationTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SaveChanges_and_query_materialize_root_and_complex_collection_through_constructors()
    {
        var pk = $"blog#{Guid.NewGuid():N}";
        var blog = new ConstructorBlog(pk, "Puppies", 100);
        blog.Posts.Add(ConstructorPost.Create("Baxter is not a dog", "He is a cat."));
        blog.Posts.Add(ConstructorPost.Create("Golden Toasters Rock", "Smaller and more chewy."));

        await Db.Blogs.AddAsync(blog, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);
        SqlCapture.Clear();

        Db.ChangeTracker.Clear();
        var materialized =
            (await Db.Blogs.Where(x => x.Pk == pk).ToListAsync(CancellationToken)).Single();

        materialized.ConstructorUsed.Should().BeTrue();
        materialized.Title.Should().Be("Puppies");
        materialized.MonthlyRevenue.Should().Be(100);
        materialized.Posts.Should().HaveCount(2);
        materialized.Posts.Should().OnlyContain(post => post.ConstructorUsed);
        materialized
            .Posts
            .Select(post => post.Title)
            .Should()
            .ContainInOrder("Baxter is not a dog", "Golden Toasters Rock");

        var item = await GetBlogItemAsync(pk, CancellationToken);
        item["pk"].S.Should().Be(pk);
        item["title"].S.Should().Be("Puppies");
        item["monthlyRevenue"].N.Should().Be("100");
        item["posts"].L.Should().HaveCount(2);
        item["posts"].L[0].M["title"].S.Should().Be("Baxter is not a dog");
        item["posts"].L[0].M["content"].S.Should().Be("He is a cat.");

        AssertSql(
            """
            SELECT "pk", "monthlyRevenue", "title", "posts"
            FROM "ConstructorBlogs"
            WHERE "pk" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_persists_scalar_and_complex_collection_updates()
    {
        var pk = $"blog#{Guid.NewGuid():N}";
        var blog = new ConstructorBlog(pk, "Kittens", 10);
        blog.Posts.Add(ConstructorPost.Create("First", "Original"));

        await Db.Blogs.AddAsync(blog, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);
        Db.ChangeTracker.Clear();

        var materialized =
            (await Db.Blogs.Where(x => x.Pk == pk).ToListAsync(CancellationToken)).Single();

        materialized.MonthlyRevenue = 200;
        materialized.Posts.Clear();
        materialized.Posts.Add(ConstructorPost.Create("Replacement", "New content"));
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetBlogItemAsync(pk, CancellationToken);
        item["monthlyRevenue"].N.Should().Be("200");
        item["posts"].L.Should().ContainSingle();
        item["posts"].L[0].M["title"].S.Should().Be("Replacement");
        item["posts"].L[0].M["content"].S.Should().Be("New content");
    }
}
