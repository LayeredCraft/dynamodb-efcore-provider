namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Configures DynamoDB table lifecycle management waits.</summary>
public sealed class DynamoTableLifecycleOptions
{
    /// <summary>
    ///     Gets or sets whether lifecycle APIs wait for DynamoDB management operations to complete.
    /// </summary>
    /// <remarks>
    ///     When disabled, multiple missing global secondary indexes on one table are still serialized
    ///     with an <c>ACTIVE</c> wait between update requests.
    ///     <para>
    ///         When disabled for existing tables, <c>EnsureCreatedAsync</c> does not wait for the
    ///         table to reach <c>ACTIVE</c> before checking for missing GSIs. If the table is
    ///         currently in an <c>UPDATING</c> state, the subsequent <c>UpdateTable</c> call will
    ///         fail with <c>ResourceInUseException</c>.
    ///     </para>
    /// </remarks>
    public bool WaitForCompletion { get; set; } = true;

    /// <summary>
    ///     Gets or sets the initial polling delay used while waiting for table lifecycle operations.
    /// </summary>
    /// <remarks>The value must be greater than <see cref="TimeSpan.Zero" />.</remarks>
    public TimeSpan InitialPollingDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets the maximum polling delay used while waiting for table lifecycle operations.
    /// </summary>
    /// <remarks>
    ///     The value must be greater than or equal to <see cref="InitialPollingDelay" />.
    /// </remarks>
    public TimeSpan MaxPollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the multiplier used to increase polling delay between attempts.</summary>
    /// <remarks>The value must be finite and greater than or equal to 1.</remarks>
    public double BackoffMultiplier { get; set; } = 1.5;

    /// <summary>
    ///     Gets or sets the maximum time to wait for each table lifecycle operation, or
    ///     <see langword="null" /> for no timeout.
    /// </summary>
    /// <remarks>When configured, the value must be greater than <see cref="TimeSpan.Zero" />.</remarks>
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
            Timeout = Timeout
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
