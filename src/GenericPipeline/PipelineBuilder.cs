using GenericPipeline.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace GenericPipeline;

public sealed class PipelineBuilder<TContext>
    where TContext : PipelineContext
{
    private readonly List<Type> _middlewareTypes = new();

    /// <summary>
    /// Adds a middleware type to the pipeline. Middleware will be resolved from the scoped ServiceProvider
    /// stored on the context (set by <see cref="ScopedPipelineExecutor{TContext}"/>).
    /// </summary>
    public PipelineBuilder<TContext> Use<TMiddleware>()
        where TMiddleware : class
    {
        _middlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    public PipelineDelegate<TContext> Build(IPipelineDiagnostics<TContext>? diagnostics = null)
    {
        var diag = diagnostics ?? NullPipelineDiagnostics<TContext>.Instance;

        var descriptors = _middlewareTypes
            .Select(MiddlewareDescriptorFactory.Create)
            .ToList();

        var ordered = MiddlewareOrdering.Order(descriptors);

        PipelineDelegate<TContext> pipeline = _ => ValueTask.CompletedTask;

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var descriptor = ordered[i];
            var next = pipeline;

            pipeline = async context =>
            {
                diag.OnMiddlewareStart(descriptor.MiddlewareType, context);

                try
                {
                    var sp = GetScopedProvider(context);
                    var middleware = sp.GetRequiredService(descriptor.MiddlewareType);

                    await MiddlewareAdapter.Adapt<TContext>(middleware, next)(context);

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
                $"Use {nameof(ScopedPipelineExecutor<TContext>)} to execute the pipeline, " +
                "or set context.Items[\"__GenericPipeline_ServiceProvider\"] to an IServiceProvider.");
        }

        return sp;
    }
}
