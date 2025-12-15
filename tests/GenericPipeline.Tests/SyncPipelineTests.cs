using GenericPipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GenericPipeline.Tests
{
    public sealed class MySyncContext : PipelineContext
{
    public bool IsAuthorized { get; set; }
    public List<string> Trace { get; } = new();
    public bool ThrowException { get; set; }
}

public sealed class SyncLoggingMiddleware : ISyncPipelineMiddleware<MySyncContext>
{
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("logging:before");
        next();
        context.Trace.Add("logging:after");
    }
}

public sealed class SyncAuthorizationMiddleware :
    ISyncPipelineMiddleware<MySyncContext>,
    IRunBefore<SyncLoggingMiddleware>
{
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("auth");
        if (!context.IsAuthorized)
            return;

        next();
    }
}

public sealed class SyncTerminalMiddleware : ISyncPipelineMiddleware<MySyncContext>, IOrderedMiddleware
{
    public int Order => int.MaxValue; // Ensure this runs last
    
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("terminal");
        // Terminal middleware - doesn't call next
    }
}

public sealed class SyncErrorThrowingMiddleware : ISyncPipelineMiddleware<MySyncContext>
{
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("error:before");
        if (context.ThrowException)
            throw new InvalidOperationException("Test exception");

        next();
        context.Trace.Add("error:after");
    }
}

public sealed class SyncOrderedMiddleware1 : ISyncPipelineMiddleware<MySyncContext>, IOrderedMiddleware
{
    public int Order => -10;

    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("ordered1");
        next();
    }
}

public sealed class SyncOrderedMiddleware10 : ISyncPipelineMiddleware<MySyncContext>, IOrderedMiddleware
{
    public int Order => 10;

    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("ordered10");
        next();
    }
}

public sealed class SyncOrderedMiddleware5 : ISyncPipelineMiddleware<MySyncContext>, IOrderedMiddleware
{
    public int Order => 5;

    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("ordered5");
        next();
    }
}

public sealed class SyncRunAfterAuthMiddleware :
    ISyncPipelineMiddleware<MySyncContext>,
    IRunAfter<SyncAuthorizationMiddleware>
{
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("after-auth");
        next();
    }
}

public sealed class SyncMultiConstraintMiddleware :
    ISyncPipelineMiddleware<MySyncContext>,
    IRunAfter<SyncAuthorizationMiddleware>,
    IRunBefore<SyncLoggingMiddleware>
{
    public void Invoke(MySyncContext context, Action next)
    {
        context.Trace.Add("multi-constraint");
        next();
    }
}

public sealed class RecordingSyncDiagnostics : ISyncPipelineDiagnostics<MySyncContext>
{
    public List<string> Events { get; } = new();

    public void OnPipelineStart(MySyncContext context) => Events.Add("pipeline:start");
    public void OnPipelineEnd(MySyncContext context) => Events.Add("pipeline:end");
    public void OnMiddlewareStart(Type middleware, MySyncContext context) => Events.Add($"mw:start:{middleware.Name}");
    public void OnMiddlewareEnd(Type middleware, MySyncContext context) => Events.Add($"mw:end:{middleware.Name}");
    public void OnMiddlewareException(Type middleware, Exception ex, MySyncContext context) => Events.Add($"mw:ex:{middleware.Name}:{ex.Message}");
}

public sealed class SyncPipelineTests
{
    [Fact]
    public void Executes_In_Order_With_Before_Constraint()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncAuthorizationMiddleware>();
        services.AddScoped<SyncLoggingMiddleware>();
        services.AddScoped<SyncTerminalMiddleware>();
        services.AddSingleton<RecordingSyncDiagnostics>();
        services.AddSingleton<ISyncPipelineDiagnostics<MySyncContext>>(sp => sp.GetRequiredService<RecordingSyncDiagnostics>());

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            var diag = sp.GetRequiredService<ISyncPipelineDiagnostics<MySyncContext>>();
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncLoggingMiddleware>()
                .Use<SyncAuthorizationMiddleware>()
                .Use<SyncTerminalMiddleware>()
                .Build(diag);
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();
        var diagnostics = provider.GetRequiredService<RecordingSyncDiagnostics>();

        var context = new MySyncContext { IsAuthorized = true };

        executor.Execute(context);

        context.Trace.ShouldBe(new[] { "auth", "logging:before", "terminal", "logging:after" });

        diagnostics.Events.ShouldContain("pipeline:start");
        diagnostics.Events.ShouldContain("pipeline:end");
        diagnostics.Events.ShouldContain("mw:start:SyncAuthorizationMiddleware");
        diagnostics.Events.ShouldContain("mw:end:SyncTerminalMiddleware");
    }

    [Fact]
    public void Short_Circuits_When_Not_Authorized()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncAuthorizationMiddleware>();
        services.AddScoped<SyncLoggingMiddleware>();
        services.AddScoped<SyncTerminalMiddleware>();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncAuthorizationMiddleware>()
                .Use<SyncLoggingMiddleware>()
                .Use<SyncTerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext { IsAuthorized = false };

        executor.Execute(context);

        context.Trace.ShouldBe(new[] { "auth" });
    }

    [Fact]
    public void Orders_By_Explicit_Order_Property()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncOrderedMiddleware10>();
        services.AddScoped<SyncOrderedMiddleware1>();
        services.AddScoped<SyncOrderedMiddleware5>();
        services.AddScoped<SyncTerminalMiddleware>();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncOrderedMiddleware10>()
                .Use<SyncOrderedMiddleware1>()
                .Use<SyncOrderedMiddleware5>()
                .Use<SyncTerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext { IsAuthorized = true };

        executor.Execute(context);

        context.Trace.ShouldBe(new[] { "ordered1", "ordered5", "ordered10", "terminal" });
    }

    [Fact]
    public void Respects_RunAfter_Constraint()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncRunAfterAuthMiddleware>();
        services.AddScoped<SyncAuthorizationMiddleware>();
        services.AddScoped<SyncTerminalMiddleware>();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncRunAfterAuthMiddleware>()
                .Use<SyncAuthorizationMiddleware>()
                .Use<SyncTerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext { IsAuthorized = true };

        executor.Execute(context);

        context.Trace.ShouldBe(new[] { "auth", "after-auth", "terminal" });
    }

    [Fact]
    public void Handles_Multiple_Ordering_Constraints()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncAuthorizationMiddleware>();
        services.AddScoped<SyncMultiConstraintMiddleware>();
        services.AddScoped<SyncLoggingMiddleware>();
        services.AddScoped<SyncTerminalMiddleware>();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncLoggingMiddleware>()
                .Use<SyncMultiConstraintMiddleware>()
                .Use<SyncAuthorizationMiddleware>()
                .Use<SyncTerminalMiddleware>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext { IsAuthorized = true };

        executor.Execute(context);

        context.Trace.ShouldBe(new[] { "auth", "multi-constraint", "logging:before", "terminal", "logging:after" });
    }

    [Fact]
    public void Diagnostics_Captures_Exception()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncErrorThrowingMiddleware>();
        services.AddSingleton<RecordingSyncDiagnostics>();
        services.AddSingleton<ISyncPipelineDiagnostics<MySyncContext>>(sp => sp.GetRequiredService<RecordingSyncDiagnostics>());

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            var diag = sp.GetRequiredService<ISyncPipelineDiagnostics<MySyncContext>>();
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncErrorThrowingMiddleware>()
                .Build(diag);
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();
        var diagnostics = provider.GetRequiredService<RecordingSyncDiagnostics>();

        var context = new MySyncContext { ThrowException = true };

        Should.Throw<InvalidOperationException>(() => executor.Execute(context));

        diagnostics.Events.ShouldContain("mw:ex:SyncErrorThrowingMiddleware:Test exception");
        diagnostics.Events.ShouldContain("pipeline:end");
    }

    [Fact]
    public void Can_Execute_Empty_Pipeline()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext();

        executor.Execute(context);

        context.Trace.ShouldBeEmpty();
    }

    [Fact]
    public void Middleware_Executes_In_Reverse_Registration_Order_Without_Constraints()
    {
        var services = new ServiceCollection();
        services.AddScoped<SyncTerminalMiddleware>();
        services.AddScoped<SyncLoggingMiddleware>();

        services.AddSingleton<SyncPipelineDelegate<MySyncContext>>(sp =>
        {
            return new SyncPipelineBuilder<MySyncContext>()
                .Use<SyncTerminalMiddleware>()
                .Use<SyncLoggingMiddleware>()
                .Build();
        });

        services.AddSingleton<SyncScopedPipelineExecutor<MySyncContext>>();

        var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<SyncScopedPipelineExecutor<MySyncContext>>();

        var context = new MySyncContext { IsAuthorized = true };

        executor.Execute(context);

        // Without constraints, later registered middleware executes first
        context.Trace.ShouldBe(new[] { "logging:before", "terminal", "logging:after" });
    }
}}