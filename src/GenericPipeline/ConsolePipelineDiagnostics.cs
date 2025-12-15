using System;

namespace GenericPipeline;

/// <summary>
/// Simple console-based diagnostics for demo and troubleshooting purposes.
/// Implements both async and sync diagnostics contracts.
/// </summary>
public sealed class ConsolePipelineDiagnostics<TContext> :
    IPipelineDiagnostics<TContext>,
    ISyncPipelineDiagnostics<TContext>
    where TContext : PipelineContext
{
    public void OnPipelineStart(TContext context)
        => Console.WriteLine($"[pipeline:start] {typeof(TContext).Name}");

    public void OnPipelineEnd(TContext context)
        => Console.WriteLine($"[pipeline:end] {typeof(TContext).Name}");

    public void OnMiddlewareStart(Type middleware, TContext context)
        => Console.WriteLine($"[mw:start] {middleware.Name}");

    public void OnMiddlewareEnd(Type middleware, TContext context)
        => Console.WriteLine($"[mw:end] {middleware.Name}");

    public void OnMiddlewareException(Type middleware, Exception ex, TContext context)
        => Console.WriteLine($"[mw:ex] {middleware.Name}: {ex.Message}");
}
