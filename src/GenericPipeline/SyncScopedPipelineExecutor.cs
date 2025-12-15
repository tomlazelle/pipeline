using GenericPipeline.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace GenericPipeline;

/// <summary>
/// Executes a synchronous pipeline inside a fresh DI scope per invocation.
/// This ensures scoped services are correct for each execution.
/// </summary>
public sealed class SyncScopedPipelineExecutor<TContext>
    where TContext : PipelineContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncPipelineDelegate<TContext> _pipeline;
    private readonly ISyncPipelineDiagnostics<TContext> _diagnostics;

    public SyncScopedPipelineExecutor(
        IServiceScopeFactory scopeFactory,
        SyncPipelineDelegate<TContext> pipeline,
        ISyncPipelineDiagnostics<TContext>? diagnostics = null)
    {
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
        _diagnostics = diagnostics ?? NullSyncPipelineDiagnostics<TContext>.Instance;
    }

    public void Execute(TContext context)
    {
        using var scope = _scopeFactory.CreateScope();

        context.Items[PipelineConstants.ServiceProviderItemKey] = scope.ServiceProvider;

        _diagnostics.OnPipelineStart(context);
        try
        {
            _pipeline(context);
        }
        finally
        {
            _diagnostics.OnPipelineEnd(context);
        }
    }
}
