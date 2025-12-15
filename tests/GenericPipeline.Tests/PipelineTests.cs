using GenericPipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GenericPipeline.Tests
{
    public sealed class MyContext : PipelineContext
{
    public bool IsAuthorized { get; set; }
    public List<string> Trace { get; } = new();
    public bool ThrowException { get; set; }
}

public sealed class LoggingMiddleware : IPipelineMiddleware<MyContext>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("logging:before");
        return InvokeCore(context, next);

        static async ValueTask InvokeCore(MyContext context, PipelineDelegate<MyContext> next)
        {
            await next(context);
            context.Trace.Add("logging:after");
        }
    }
}

public sealed class AuthorizationMiddleware :
    IPipelineMiddleware<MyContext>,
    IRunBefore<LoggingMiddleware>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("auth");
        if (!context.IsAuthorized)
            return ValueTask.CompletedTask;

        return next(context);
    }
}

public sealed class TerminalMiddleware : IPipelineMiddleware<MyContext>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("terminal");
        return ValueTask.CompletedTask;
    }
}

public sealed class ErrorThrowingMiddleware : IPipelineMiddleware<MyContext>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("error:before");
        if (context.ThrowException)
            throw new InvalidOperationException("Test exception");
        
        return InvokeCore(context, next);
        
        static async ValueTask InvokeCore(MyContext context, PipelineDelegate<MyContext> next)
        {
            await next(context);
            context.Trace.Add("error:after");
        }
    }
}

public sealed class OrderedMiddleware1 : IPipelineMiddleware<MyContext>, IOrderedMiddleware
{
    public int Order => -10;
    
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("ordered1");
        return next(context);
    }
}

public sealed class OrderedMiddleware10 : IPipelineMiddleware<MyContext>, IOrderedMiddleware
{
    public int Order => 10;
    
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("ordered10");
        return next(context);
    }
}

public sealed class OrderedMiddleware5 : IPipelineMiddleware<MyContext>, IOrderedMiddleware
{
    public int Order => 5;
    
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("ordered5");
        return next(context);
    }
}

public sealed class RunAfterAuthMiddleware : 
    IPipelineMiddleware<MyContext>, 
    IRunAfter<AuthorizationMiddleware>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("after-auth");
        return next(context);
    }
}

public sealed class MultiConstraintMiddleware :
    IPipelineMiddleware<MyContext>,
    IRunAfter<AuthorizationMiddleware>,
    IRunBefore<LoggingMiddleware>
{
    public ValueTask InvokeAsync(MyContext context, PipelineDelegate<MyContext> next)
    {
        context.Trace.Add("multi-constraint");
        return next(context);
    }
}

public sealed class RecordingDiagnostics : IPipelineDiagnostics<MyContext>
{
    public List<string> Events { get; } = new();

    public void OnPipelineStart(MyContext context) => Events.Add("pipeline:start");
    public void OnPipelineEnd(MyContext context) => Events.Add("pipeline:end");
    public void OnMiddlewareStart(Type middleware, MyContext context) => Events.Add($"mw:start:{middleware.Name}");
    public void OnMiddlewareEnd(Type middleware, MyContext context) => Events.Add($"mw:end:{middleware.Name}");
    public void OnMiddlewareException(Type middleware, Exception ex, MyContext context) => Events.Add($"mw:ex:{middleware.Name}:{ex.Message}");
}

public sealed class PipelineTests
{
    [Fact]
    public async Task Executes_In_Order_With_Before_Constraint()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>();
        services.AddScoped<LoggingMiddleware>();
        services.AddScoped<TerminalMiddleware>();
        services.AddSingleton<RecordingDiagnostics>();
        services.AddSingleton<IPipelineDiagnostics<MyContext>>(sp => sp.GetRequiredService<RecordingDiagnostics>());

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            var diag = sp.GetRequiredService<IPipelineDiagnostics<MyContext>>();
            return new PipelineBuilder<MyContext>()
                .Use<LoggingMiddleware>()
                .Use<AuthorizationMiddleware>()
                .Use<TerminalMiddleware>()
                .Build(diag);
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        // AuthorizationMiddleware declares RunBefore<LoggingMiddleware>, so auth should appear before logging.
        ctx.Trace.ShouldBe(new[] { "auth", "logging:before", "terminal", "logging:after" });
    }

    [Fact]
    public async Task Can_Short_Circuit()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>();
        services.AddScoped<LoggingMiddleware>();
        services.AddScoped<TerminalMiddleware>();

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<LoggingMiddleware>()
                .Use<AuthorizationMiddleware>()
                .Use<TerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = false };
        await executor.ExecuteAsync(ctx);

        // auth ran, but pipeline short-circuited before logging/terminal.
        ctx.Trace.ShouldBe(new[] { "auth" });
    }

    [Fact]
    public async Task Emits_Diagnostics()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>();
        services.AddScoped<LoggingMiddleware>();
        services.AddScoped<TerminalMiddleware>();
        services.AddSingleton<RecordingDiagnostics>();
        services.AddSingleton<IPipelineDiagnostics<MyContext>>(sp => sp.GetRequiredService<RecordingDiagnostics>());

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            var diag = sp.GetRequiredService<IPipelineDiagnostics<MyContext>>();
            return new PipelineBuilder<MyContext>()
                .Use<LoggingMiddleware>()
                .Use<AuthorizationMiddleware>()
                .Use<TerminalMiddleware>()
                .Build(diag);
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();
        var diagRec = provider.GetRequiredService<RecordingDiagnostics>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        diagRec.Events.ShouldContain("pipeline:start");
        diagRec.Events.ShouldContain("pipeline:end");
        diagRec.Events.ShouldContain("mw:start:AuthorizationMiddleware");
        diagRec.Events.ShouldContain("mw:end:AuthorizationMiddleware");
    }

    [Fact]
    public async Task Exception_Propagates_And_Diagnostics_Captured()
    {
        var services = new ServiceCollection();
        services.AddScoped<ErrorThrowingMiddleware>();
        services.AddScoped<TerminalMiddleware>();
        services.AddSingleton<RecordingDiagnostics>();
        services.AddSingleton<IPipelineDiagnostics<MyContext>>(sp => sp.GetRequiredService<RecordingDiagnostics>());

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            var diag = sp.GetRequiredService<IPipelineDiagnostics<MyContext>>();
            return new PipelineBuilder<MyContext>()
                .Use<ErrorThrowingMiddleware>()
                .Use<TerminalMiddleware>()
                .Build(diag);
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();
        var diagRec = provider.GetRequiredService<RecordingDiagnostics>();

        var ctx = new MyContext { ThrowException = true };
        
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.ExecuteAsync(ctx));
        
        ex.Message.ShouldBe("Test exception");
        diagRec.Events.ShouldContain("mw:ex:ErrorThrowingMiddleware:Test exception");
        diagRec.Events.ShouldContain("pipeline:end"); // Pipeline end should still be called
        ctx.Trace.ShouldHaveSingleItem();
        ctx.Trace[0].ShouldBe("error:before"); // Terminal should not have run
    }

    [Fact]
    public async Task Ordered_Middleware_Respects_Order_Property()
    {
        var services = new ServiceCollection();
        services.AddScoped<OrderedMiddleware10>();
        services.AddScoped<OrderedMiddleware1>();
        services.AddScoped<OrderedMiddleware5>();
        services.AddScoped<LoggingMiddleware>(); // Use logging instead of terminal so pipeline completes

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<OrderedMiddleware10>()
                .Use<OrderedMiddleware1>()
                .Use<OrderedMiddleware5>()
                .Use<LoggingMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        // Ordered by Order property: -10 (ordered1), 0 (logging default), 5 (ordered5), 10 (ordered10). Lower order runs earlier.
        ctx.Trace.ShouldBe(new[] { "ordered1", "logging:before", "ordered5", "ordered10", "logging:after" });
    }

    [Fact]
    public async Task RunAfter_Constraint_Works()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>();
        services.AddScoped<RunAfterAuthMiddleware>();
        services.AddScoped<TerminalMiddleware>();

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<RunAfterAuthMiddleware>()
                .Use<TerminalMiddleware>()
                .Use<AuthorizationMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        // RunAfterAuthMiddleware should come after AuthorizationMiddleware
        var authIndex = ctx.Trace.IndexOf("auth");
        var afterAuthIndex = ctx.Trace.IndexOf("after-auth");
        afterAuthIndex.ShouldBeGreaterThan(authIndex, "RunAfterAuthMiddleware should run after AuthorizationMiddleware");
    }

    [Fact]
    public async Task Multiple_Constraints_Are_Respected()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>();
        services.AddScoped<LoggingMiddleware>();
        services.AddScoped<MultiConstraintMiddleware>();
        services.AddScoped<TerminalMiddleware>();

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<LoggingMiddleware>()
                .Use<MultiConstraintMiddleware>()
                .Use<TerminalMiddleware>()
                .Use<AuthorizationMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        // multi-constraint should be after auth and before logging
        var authIndex = ctx.Trace.IndexOf("auth");
        var multiIndex = ctx.Trace.IndexOf("multi-constraint");
        var loggingIndex = ctx.Trace.IndexOf("logging:before");
        
        multiIndex.ShouldBeGreaterThan(authIndex, "MultiConstraintMiddleware should run after AuthorizationMiddleware");
        loggingIndex.ShouldBeGreaterThan(multiIndex, "LoggingMiddleware should run after MultiConstraintMiddleware");
    }

    [Fact]
    public async Task Empty_Pipeline_Completes_Successfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RecordingDiagnostics>();
        services.AddSingleton<IPipelineDiagnostics<MyContext>>(sp => sp.GetRequiredService<RecordingDiagnostics>());

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            var diag = sp.GetRequiredService<IPipelineDiagnostics<MyContext>>();
            return new PipelineBuilder<MyContext>().Build(diag);
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();
        var diagRec = provider.GetRequiredService<RecordingDiagnostics>();

        var ctx = new MyContext();
        await executor.ExecuteAsync(ctx);

        ctx.Trace.ShouldBeEmpty();
        diagRec.Events.ShouldContain("pipeline:start");
        diagRec.Events.ShouldContain("pipeline:end");
    }

    [Fact]
    public async Task Pipeline_Without_Diagnostics_Works()
    {
        var services = new ServiceCollection();
        services.AddScoped<LoggingMiddleware>();
        services.AddScoped<TerminalMiddleware>();

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<LoggingMiddleware>()
                .Use<TerminalMiddleware>()
                .Build(); // No diagnostics
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        ctx.Trace.ShouldBe(new[] { "logging:before", "terminal", "logging:after" });
    }

    [Fact]
    public async Task Async_Pipeline_With_Multiple_Middlewares()
    {
        var services = new ServiceCollection();
        services.AddScoped<AuthorizationMiddleware>(); // Sync
        services.AddScoped<LoggingMiddleware>(); // Async
        services.AddScoped<TerminalMiddleware>(); // Async

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<AuthorizationMiddleware>()
                .Use<LoggingMiddleware>()
                .Use<TerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext { IsAuthorized = true };
        await executor.ExecuteAsync(ctx);

        ctx.Trace.ShouldBe(new[] { "auth", "logging:before", "terminal", "logging:after" });
    }

    [Fact]
    public async Task Context_Items_Are_Preserved_Throughout_Pipeline()
    {
        var services = new ServiceCollection();
        services.AddScoped<TerminalMiddleware>();

        services.AddSingleton<PipelineDelegate<MyContext>>(sp =>
        {
            return new PipelineBuilder<MyContext>()
                .Use<TerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<ScopedPipelineExecutor<MyContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<ScopedPipelineExecutor<MyContext>>();

        var ctx = new MyContext();
        ctx.Items["test-key"] = "test-value";
        
        await executor.ExecuteAsync(ctx);

        ctx.Items["test-key"].ShouldBe("test-value");
        ctx.Items.Keys.ShouldContain("__GenericPipeline_ServiceProvider");
    }
}}