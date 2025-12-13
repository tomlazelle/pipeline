# GenericPipeline

A lightweight, DI-friendly, generic middleware pipeline for .NET.

## Features

- Generic `TContext` (mutable, reference type)
- Middleware composition by **type**
- Supports **async** and **sync** middleware
- Per-execution **DI scope** (scoped services supported)
- Deterministic **ordering** (`Order`, `RunBefore<T>`, `RunAfter<T>`)
- Built-in **diagnostics hooks**

## Quick Start

```csharp
using GenericPipeline;

var services = new ServiceCollection();
services.AddScoped<AuthorizationMiddleware>();
services.AddScoped(typeof(LoggingMiddleware<>));
services.AddSingleton<IPipelineDiagnostics<MyContext>, ConsolePipelineDiagnostics<MyContext>>();

services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
{
    return new PipelineBuilder<MyContext>()
        .Use<LoggingMiddleware<MyContext>>()
        .Use<AuthorizationMiddleware>()
        .Build();
});

services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

var provider = services.BuildServiceProvider();
var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

await executor.ExecuteAsync(new MyContext { IsAuthorized = true });
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
