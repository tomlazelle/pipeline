using GenericPipeline.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace GenericPipeline;

/// <summary>
/// Builds a synchronous (non-async) middleware pipeline.
/// All middleware must implement <see cref="ISyncPipelineMiddleware{TContext}"/>.
/// </summary>
public sealed class SyncPipelineBuilder<TContext>
    where TContext : PipelineContext
{
    private readonly List<Type> _middlewareTypes = new();

    /// <summary>
    /// Adds a synchronous middleware type to the pipeline. Middleware will be resolved from the scoped ServiceProvider
    /// stored on the context (set by <see cref="SyncScopedPipelineExecutor{TContext}"/>).
    /// </summary>
    public SyncPipelineBuilder<TContext> Use<TMiddleware>()
        where TMiddleware : class
    {
        _middlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    public SyncPipelineDelegate<TContext> Build(ISyncPipelineDiagnostics<TContext>? diagnostics = null)
    {
        var diag = diagnostics ?? NullSyncPipelineDiagnostics<TContext>.Instance;

        var descriptors = _middlewareTypes
            .Select(MiddlewareDescriptorFactory.Create)
            .ToList();

        var ordered = MiddlewareOrdering.Order(descriptors);

        SyncPipelineDelegate<TContext> pipeline = _ => { };

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var descriptor = ordered[i];
            var next = pipeline;

            pipeline = context =>
            {
                diag.OnMiddlewareStart(descriptor.MiddlewareType, context);

                try
                {
                    var sp = GetScopedProvider(context);
                    var middleware = sp.GetRequiredService(descriptor.MiddlewareType);

                    SyncMiddlewareAdapter.Adapt<TContext>(middleware, next)(context);

                    diag.OnMiddlewareEnd(descriptor.MiddlewareType, context);
                }
                catch (Exception ex)
                {
                    diag.OnMiddlewareException(descriptor.MiddlewareType, ex, context);
                    throw;
                }
            };
        }

        return pipeline;
    }

    private static IServiceProvider GetScopedProvider(TContext context)
    {
        if (!context.Items.TryGetValue(PipelineConstants.ServiceProviderItemKey, out var spObj) ||
            spObj is not IServiceProvider sp)
        {
            throw new InvalidOperationException(
                "No scoped IServiceProvider found on context. " +
                $"Use {nameof(SyncScopedPipelineExecutor<TContext>)} to execute the pipeline, " +
                "or set context.Items[\"__GenericPipeline_ServiceProvider\"] to an IServiceProvider.");
        }

        return sp;
    }
}
