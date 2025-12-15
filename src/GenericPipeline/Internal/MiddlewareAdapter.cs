namespace GenericPipeline.Internal;

/// <summary>
/// Adapter for async middleware only. Sync middleware should use <see cref="SyncMiddlewareAdapter"/> 
/// with <see cref="SyncPipelineBuilder{TContext}"/>.
/// </summary>
internal static class MiddlewareAdapter
{
    public static PipelineDelegate<TContext> Adapt<TContext>(
        object middleware,
        PipelineDelegate<TContext> next)
        where TContext : PipelineContext
    {
        return middleware switch
        {
            IPipelineMiddleware<TContext> asyncMw =>
                ctx => asyncMw.InvokeAsync(ctx, next),

            _ => throw new InvalidOperationException(
                $"Middleware {middleware.GetType().FullName} does not implement {nameof(IPipelineMiddleware<TContext>)}. " +
                $"For synchronous middleware, use {nameof(SyncPipelineBuilder<TContext>)} instead.")
        };
    }
}
