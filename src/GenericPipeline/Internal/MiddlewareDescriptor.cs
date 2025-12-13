namespace GenericPipeline.Internal;

internal sealed record MiddlewareDescriptor(
    Type MiddlewareType,
    int Order,
    IReadOnlyList<Type> Before,
    IReadOnlyList<Type> After
);
