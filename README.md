# GenericPipeline

A lightweight, DI-friendly, generic middleware pipeline for .NET.

## Features

- Generic `TContext` (mutable, reference type)
- Middleware composition by **type**
- **Separate sync and async pipelines** - pure implementations without blocking
- Per-execution **DI scope** (scoped services supported)
- Deterministic **ordering** (`Order`, `RunBefore<T>`, `RunAfter<T>`)
- Built-in **diagnostics hooks**

## Architecture

This library provides **two independent pipeline implementations**:

### Async Pipeline
- Uses `ValueTask` for optimal async performance
- Middleware implements `IPipelineMiddleware<TContext>`
- Built with `PipelineBuilder<TContext>`
- Executed with `ScopedPipelineExecutor<TContext>.ExecuteAsync()`

### Sync Pipeline
- Pure synchronous execution (no `ValueTask` overhead)
- Middleware implements `ISyncPipelineMiddleware<TContext>`
- Built with `SyncPipelineBuilder<TContext>`
- Executed with `SyncScopedPipelineExecutor<TContext>.Execute()`

**Important**: The two pipelines cannot be mixed. Choose sync or async based on your use case.

## Quick Start - Async Pipeline

```csharp
using GenericPipeline;

// Define your context
public class MyContext : PipelineContext
{
    public bool IsAuthorized { get; set; }
}

// Define async middleware
public class LoggingMiddleware : IPipelineMiddleware<MyContext>
{
    public async ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        Console.WriteLine("Before");
        await next(context);
        Console.WriteLine("After");
    }
}

public class AuthorizationMiddleware : 
    IPipelineMiddleware<MyContext>,
    IRunBefore<LoggingMiddleware>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        if (!context.IsAuthorized)
            return ValueTask.CompletedTask; // Short-circuit
        
        return next(context);
    }
}

// Configure DI
var services = new ServiceCollection();
services.AddScoped<AuthorizationMiddleware>();
services.AddScoped<LoggingMiddleware>();
// Optional: diagnostics
services.AddSingleton<IPipelineDiagnostics<MyContext>, ConsolePipelineDiagnostics<MyContext>>();

services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
{
    var diag = sp.GetService<IPipelineDiagnostics<MyContext>>();
    return new PipelineBuilder<MyContext>()
        .Use<AuthorizationMiddleware>()
        .Use<LoggingMiddleware>()
        .Build(diag);
});

services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

// Execute
var provider = services.BuildServiceProvider();
var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

await executor.ExecuteAsync(new MyContext { IsAuthorized = true });
```

## Quick Start - Sync Pipeline

```csharp
using GenericPipeline;

// Define your context
public class MyContext : PipelineContext
{
    public bool IsAuthorized { get; set; }
}

// Define sync middleware
public class LoggingMiddleware : ISyncPipelineMiddleware<MyContext>
{
    public void Invoke(MyContext context, Action next)
    {
        Console.WriteLine("Before");
        next();
        Console.WriteLine("After");
    }
}

public class AuthorizationMiddleware : 
    ISyncPipelineMiddleware<MyContext>,
    IRunBefore<LoggingMiddleware>
{
    public void Invoke(MyContext context, Action next)
    {
        if (!context.IsAuthorized)
            return; // Short-circuit
        
        next();
    }
}

// Configure DI
var services = new ServiceCollection();
services.AddScoped<AuthorizationMiddleware>();
services.AddScoped<LoggingMiddleware>();
// Optional: diagnostics
services.AddSingleton<ISyncPipelineDiagnostics<MyContext>, ConsolePipelineDiagnostics<MyContext>>();

services.AddSingleton<SyncPipelineDelegate<MyContext>>(sp =>
{
    var diag = sp.GetService<ISyncPipelineDiagnostics<MyContext>>();
    return new SyncPipelineBuilder<MyContext>()
        .Use<AuthorizationMiddleware>()
        .Use<LoggingMiddleware>()
        .Build(diag);
});

services.AddSingleton<SyncScopedPipelineExecutor<MyContext>>();

// Execute
var provider = services.BuildServiceProvider();
var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MyContext>>();

executor.Execute(new MyContext { IsAuthorized = true });
```

## Middleware Ordering

Control execution order using:

- **`IOrderedMiddleware`**: Set explicit `Order` property (lower runs first)
- **`IRunBefore<TMiddleware>`**: Ensures this middleware runs before `TMiddleware`
- **`IRunAfter<TMiddleware>`**: Ensures this middleware runs after `TMiddleware`

```csharp
public class EarlyMiddleware : ISyncPipelineMiddleware<MyContext>, IOrderedMiddleware
{
    public int Order => -100; // Runs early
    public void Invoke(MyContext context, Action next) => next();
}

public class ConstrainedMiddleware : 
    ISyncPipelineMiddleware<MyContext>,
    IRunAfter<AuthMiddleware>,
    IRunBefore<LoggingMiddleware>
{
    public void Invoke(MyContext context, Action next) => next();
}
```

## Repository Layout

- `src/GenericPipeline` - library
- `tests/GenericPipeline.Tests` - tests

## Build

```bash
dotnet build
dotnet test
dotnet pack -c Release
```

## License

MIT
