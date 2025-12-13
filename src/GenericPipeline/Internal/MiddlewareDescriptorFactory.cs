using System.Reflection;

namespace GenericPipeline.Internal;

internal static class MiddlewareDescriptorFactory
{
    public static MiddlewareDescriptor Create(Type type)
    {
        var order = 0;
        if (typeof(IOrderedMiddleware).IsAssignableFrom(type))
        {
            // Avoid Activator.CreateInstance requirements; read Order via reflection on an instance if possible,
            // else default to 0. Consumers should prefer parameterless constructors for ordered middleware,
            // or use Before/After constraints.
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor is not null)
            {
                var instance = (IOrderedMiddleware)Activator.CreateInstance(type)!;
                order = instance.Order;
            }
        }

        var before = type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRunBefore<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        var after = type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRunAfter<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        return new MiddlewareDescriptor(type, order, before, after);
    }
}
