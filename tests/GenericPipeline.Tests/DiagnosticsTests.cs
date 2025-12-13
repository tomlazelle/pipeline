using GenericPipeline;
using Shouldly;
using Xunit;

public sealed class DiagnosticsTests
{
    [Fact]
    public void NullPipelineDiagnostics_Instance_Is_Singleton()
    {
        var instance1 = NullPipelineDiagnostics<MyContext>.Instance;
        var instance2 = NullPipelineDiagnostics<MyContext>.Instance;

        instance1.ShouldBeSameAs(instance2);
    }

    [Fact]
    public void NullPipelineDiagnostics_OnPipelineStart_Does_Not_Throw()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();

        Should.NotThrow(() => diagnostics.OnPipelineStart(context));
    }

    [Fact]
    public void NullPipelineDiagnostics_OnPipelineEnd_Does_Not_Throw()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();

        Should.NotThrow(() => diagnostics.OnPipelineEnd(context));
    }

    [Fact]
    public void NullPipelineDiagnostics_OnMiddlewareStart_Does_Not_Throw()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();

        Should.NotThrow(() => diagnostics.OnMiddlewareStart(typeof(LoggingMiddleware), context));
    }

    [Fact]
    public void NullPipelineDiagnostics_OnMiddlewareEnd_Does_Not_Throw()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();

        Should.NotThrow(() => diagnostics.OnMiddlewareEnd(typeof(LoggingMiddleware), context));
    }

    [Fact]
    public void NullPipelineDiagnostics_OnMiddlewareException_Does_Not_Throw()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();
        var exception = new InvalidOperationException("Test");

        Should.NotThrow(() => diagnostics.OnMiddlewareException(typeof(LoggingMiddleware), exception, context));
    }

    [Fact]
    public void NullPipelineDiagnostics_Performs_No_Side_Effects()
    {
        var diagnostics = NullPipelineDiagnostics<MyContext>.Instance;
        var context = new MyContext();
        var exception = new InvalidOperationException("Test");

        // Call all methods
        diagnostics.OnPipelineStart(context);
        diagnostics.OnMiddlewareStart(typeof(LoggingMiddleware), context);
        diagnostics.OnMiddlewareEnd(typeof(LoggingMiddleware), context);
        diagnostics.OnMiddlewareException(typeof(LoggingMiddleware), exception, context);
        diagnostics.OnPipelineEnd(context);

        // Context should be unmodified
        context.Trace.ShouldBeEmpty();
        context.Items.ShouldBeEmpty();
    }

    [Fact]
    public void RecordingDiagnostics_Captures_All_Events_In_Order()
    {
        var diagnostics = new RecordingDiagnostics();
        var context = new MyContext();
        var exception = new InvalidOperationException("Test exception");

        diagnostics.OnPipelineStart(context);
        diagnostics.OnMiddlewareStart(typeof(LoggingMiddleware), context);
        diagnostics.OnMiddlewareEnd(typeof(LoggingMiddleware), context);
        diagnostics.OnMiddlewareStart(typeof(AuthorizationMiddleware), context);
        diagnostics.OnMiddlewareException(typeof(AuthorizationMiddleware), exception, context);
        diagnostics.OnPipelineEnd(context);

        diagnostics.Events.Count.ShouldBe(6);
        diagnostics.Events[0].ShouldBe("pipeline:start");
        diagnostics.Events[1].ShouldBe("mw:start:LoggingMiddleware");
        diagnostics.Events[2].ShouldBe("mw:end:LoggingMiddleware");
        diagnostics.Events[3].ShouldBe("mw:start:AuthorizationMiddleware");
        diagnostics.Events[4].ShouldBe("mw:ex:AuthorizationMiddleware:Test exception");
        diagnostics.Events[5].ShouldBe("pipeline:end");
    }

    [Fact]
    public void RecordingDiagnostics_Events_List_Is_Mutable()
    {
        var diagnostics = new RecordingDiagnostics();
        var context = new MyContext();

        diagnostics.OnPipelineStart(context);
        diagnostics.Events.Count.ShouldBe(1);

        diagnostics.Events.Clear();
        diagnostics.Events.ShouldBeEmpty();

        diagnostics.OnPipelineEnd(context);
        diagnostics.Events.Count.ShouldBe(1);
        diagnostics.Events[0].ShouldBe("pipeline:end");
    }

    [Fact]
    public void RecordingDiagnostics_Handles_Multiple_Middleware_Types()
    {
        var diagnostics = new RecordingDiagnostics();
        var context = new MyContext();

        diagnostics.OnMiddlewareStart(typeof(LoggingMiddleware), context);
        diagnostics.OnMiddlewareStart(typeof(AuthorizationMiddleware), context);
        diagnostics.OnMiddlewareStart(typeof(TerminalMiddleware), context);
        diagnostics.OnMiddlewareEnd(typeof(TerminalMiddleware), context);
        diagnostics.OnMiddlewareEnd(typeof(AuthorizationMiddleware), context);
        diagnostics.OnMiddlewareEnd(typeof(LoggingMiddleware), context);

        diagnostics.Events.Count.ShouldBe(6);
        diagnostics.Events.ShouldContain("mw:start:LoggingMiddleware");
        diagnostics.Events.ShouldContain("mw:start:AuthorizationMiddleware");
        diagnostics.Events.ShouldContain("mw:start:TerminalMiddleware");
        diagnostics.Events.ShouldContain("mw:end:TerminalMiddleware");
        diagnostics.Events.ShouldContain("mw:end:AuthorizationMiddleware");
        diagnostics.Events.ShouldContain("mw:end:LoggingMiddleware");
    }

    [Fact]
    public void RecordingDiagnostics_Captures_Exception_Details()
    {
        var diagnostics = new RecordingDiagnostics();
        var context = new MyContext();
        var exception1 = new InvalidOperationException("First error");
        var exception2 = new ArgumentException("Second error");

        diagnostics.OnMiddlewareException(typeof(LoggingMiddleware), exception1, context);
        diagnostics.OnMiddlewareException(typeof(AuthorizationMiddleware), exception2, context);

        diagnostics.Events.Count.ShouldBe(2);
        diagnostics.Events[0].ShouldBe("mw:ex:LoggingMiddleware:First error");
        diagnostics.Events[1].ShouldBe("mw:ex:AuthorizationMiddleware:Second error");
    }
}
