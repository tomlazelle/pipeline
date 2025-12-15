namespace GenericPipeline.Internal;

internal static class SyncMiddlewareAdapter
{
    public static SyncPipelineDelegate<TContext> Adapt<TContext>(
        object middleware,
        SyncPipelineDelegate<TContext> next)
        where TContext : PipelineContext
    {
        return middleware switch
        {
            ISyncPipelineMiddleware<TContext> syncMw =>
                ctx => syncMw.Invoke(ctx, () => next(ctx)),

            _ => throw new InvalidOperationException(
                $"Middleware {middleware.GetType().FullName} does not implement {nameof(ISyncPipelineMiddleware<TContext>)}.")
        };
    }
}
