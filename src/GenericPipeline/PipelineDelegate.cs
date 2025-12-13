namespace GenericPipeline;

public delegate ValueTask PipelineDelegate<TContext>(TContext context)
    where TContext : PipelineContext;
