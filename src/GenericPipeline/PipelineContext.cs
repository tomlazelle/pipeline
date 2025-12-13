namespace GenericPipeline;

public abstract class PipelineContext
{
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// A mutable bag for cross-middleware communication.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
}
