namespace GenericPipeline;

/// <summary>
/// Async middleware contract.
/// </summary>
public interface IPipelineMiddleware<TContext>
    where TContext : PipelineContext
{
    ValueTask InvokeAsync(TContext context, PipelineDelegate<TContext> next);
}

/// <summary>
/// Sync middleware contract.
/// </summary>
public interface ISyncPipelineMiddleware<TContext>
    where TContext : PipelineContext
{
    void Invoke(TContext context, Action next);
}
