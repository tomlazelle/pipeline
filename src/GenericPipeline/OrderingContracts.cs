namespace GenericPipeline;

/// <summary>
/// Coarse ordering. Lower runs earlier.
/// </summary>
public interface IOrderedMiddleware
{
    int Order { get; }
}

/// <summary>
/// Run this middleware before the specified middleware type.
/// </summary>
public interface IRunBefore<TMiddleware> { }

/// <summary>
/// Run this middleware after the specified middleware type.
/// </summary>
public interface IRunAfter<TMiddleware> { }
