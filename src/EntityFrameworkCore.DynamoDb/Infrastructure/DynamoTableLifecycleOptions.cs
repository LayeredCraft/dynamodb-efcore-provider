namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Configures DynamoDB table lifecycle management waits.</summary>
public sealed class DynamoTableLifecycleOptions
{
    /// <summary>Gets or sets whether lifecycle APIs wait for DynamoDB management operations to complete.</summary>
    public bool WaitForCompletion { get; set; } = true;

    /// <summary>Gets or sets the initial polling delay used while waiting for table lifecycle operations.</summary>
    public TimeSpan InitialPollingDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum polling delay used while waiting for table lifecycle operations.</summary>
    public TimeSpan MaxPollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the multiplier used to increase polling delay between attempts.</summary>
    public double BackoffMultiplier { get; set; } = 1.5;

    /// <summary>Gets or sets the maximum time to wait for each table lifecycle operation, or <see langword="null" /> for no timeout.</summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Creates a copy of this options instance.</summary>
    /// <returns>A copy containing the same option values.</returns>
    public DynamoTableLifecycleOptions Clone()
        => new()
        {
            WaitForCompletion = WaitForCompletion,
            InitialPollingDelay = InitialPollingDelay,
            MaxPollingDelay = MaxPollingDelay,
            BackoffMultiplier = BackoffMultiplier,
            Timeout = Timeout,
        };

    /// <summary>Validates option values.</summary>
    /// <exception cref="InvalidOperationException">Thrown when option values are invalid.</exception>
    public void Validate()
    {
        if (InitialPollingDelay <= TimeSpan.Zero)
            throw new InvalidOperationException(
                "DynamoDB table lifecycle initial polling delay must be greater than zero.");

        if (MaxPollingDelay < InitialPollingDelay)
            throw new InvalidOperationException(
                "DynamoDB table lifecycle maximum polling delay must be greater than or equal to the initial polling delay.");

        if (!double.IsFinite(BackoffMultiplier) || BackoffMultiplier < 1)
            throw new InvalidOperationException(
                "DynamoDB table lifecycle backoff multiplier must be finite and greater than or equal to 1.");

        if (Timeout <= TimeSpan.Zero)
            throw new InvalidOperationException(
                "DynamoDB table lifecycle timeout must be greater than zero when configured.");
    }
}
