namespace GenericPipeline.Internal;

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

            ISyncPipelineMiddleware<TContext> syncMw =>
                ctx =>
                {
                    syncMw.Invoke(ctx, () => next(ctx).GetAwaiter().GetResult());
                    return ValueTask.CompletedTask;
                },

            _ => throw new InvalidOperationException(
                $"Middleware {middleware.GetType().FullName} does not implement {nameof(IPipelineMiddleware<TContext>)} or {nameof(ISyncPipelineMiddleware<TContext>)}.")
        };
    }
}
