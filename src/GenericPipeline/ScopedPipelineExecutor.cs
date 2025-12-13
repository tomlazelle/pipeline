using GenericPipeline.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace GenericPipeline;

/// <summary>
/// Executes a pipeline inside a fresh DI scope per invocation.
/// This ensures scoped services are correct for each execution.
/// </summary>
public sealed class ScopedPipelineExecutor<TContext>
    where TContext : PipelineContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PipelineDelegate<TContext> _pipeline;
    private readonly IPipelineDiagnostics<TContext> _diagnostics;

    public ScopedPipelineExecutor(
        IServiceScopeFactory scopeFactory,
        PipelineDelegate<TContext> pipeline,
        IPipelineDiagnostics<TContext>? diagnostics = null)
    {
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
        _diagnostics = diagnostics ?? NullPipelineDiagnostics<TContext>.Instance;
    }

    public async ValueTask ExecuteAsync(TContext context)
    {
        using var scope = _scopeFactory.CreateScope();

        context.Items[PipelineConstants.ServiceProviderItemKey] = scope.ServiceProvider;

        _diagnostics.OnPipelineStart(context);
        try
        {
            await _pipeline(context);
        }
        finally
        {
            _diagnostics.OnPipelineEnd(context);
        }
    }
}
