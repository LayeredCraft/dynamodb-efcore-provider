// Intentionally in the global namespace. EF Core 10's precompiled no-tracking queries name a generated
// local after the entity's full CLR name, so an entity in a dotted namespace produces invalid C#
// (`var Some.Namespace.EntityPrimaryKeyProperties = ...`). EF Core 11 does not emit that local.

public sealed class AotRuntimeQuestion
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public AotRuntimeDetails Details { get; set; } = new();
    public List<AotRuntimeAnswer> Answers { get; set; } = [];
}

public sealed class AotRuntimeDetails
{
    public string Summary { get; set; } = null!;
    public int Level { get; set; }
}

public sealed class AotRuntimeAnswer
{
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
}
