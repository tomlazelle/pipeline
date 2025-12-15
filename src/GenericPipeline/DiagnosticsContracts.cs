namespace GenericPipeline;

/// <summary>
/// Diagnostics interface for async pipelines.
/// </summary>
public interface IPipelineDiagnostics<TContext>
    where TContext : PipelineContext
{
    void OnPipelineStart(TContext context);
    void OnPipelineEnd(TContext context);
    void OnMiddlewareStart(Type middleware, TContext context);
    void OnMiddlewareEnd(Type middleware, TContext context);
    void OnMiddlewareException(Type middleware, Exception ex, TContext context);
}

/// <summary>
/// Diagnostics interface for synchronous pipelines.
/// </summary>
public interface ISyncPipelineDiagnostics<TContext>
    where TContext : PipelineContext
{
    void OnPipelineStart(TContext context);
    void OnPipelineEnd(TContext context);
    void OnMiddlewareStart(Type middleware, TContext context);
    void OnMiddlewareEnd(Type middleware, TContext context);
    void OnMiddlewareException(Type middleware, Exception ex, TContext context);
}

public sealed class NullPipelineDiagnostics<TContext> : IPipelineDiagnostics<TContext>
    where TContext : PipelineContext
{
    public static readonly NullPipelineDiagnostics<TContext> Instance = new();

    private NullPipelineDiagnostics() { }

    public void OnPipelineStart(TContext context) { }
    public void OnPipelineEnd(TContext context) { }
    public void OnMiddlewareStart(Type middleware, TContext context) { }
    public void OnMiddlewareEnd(Type middleware, TContext context) { }
    public void OnMiddlewareException(Type middleware, Exception ex, TContext context) { }
}

public sealed class NullSyncPipelineDiagnostics<TContext> : ISyncPipelineDiagnostics<TContext>
    where TContext : PipelineContext
{
    public static readonly NullSyncPipelineDiagnostics<TContext> Instance = new();

    private NullSyncPipelineDiagnostics() { }

    public void OnPipelineStart(TContext context) { }
    public void OnPipelineEnd(TContext context) { }
    public void OnMiddlewareStart(Type middleware, TContext context) { }
    public void OnMiddlewareEnd(Type middleware, TContext context) { }
    public void OnMiddlewareException(Type middleware, Exception ex, TContext context) { }
}
