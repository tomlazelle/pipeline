namespace GenericPipeline;

public delegate void SyncPipelineDelegate<TContext>(TContext context)
    where TContext : PipelineContext;
