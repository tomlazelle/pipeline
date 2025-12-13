namespace GenericPipeline.Internal;

internal static class MiddlewareOrdering
{
    public static IReadOnlyList<MiddlewareDescriptor> Order(
        IEnumerable<MiddlewareDescriptor> descriptors)
    {
        var list = descriptors.ToList();

        // 1) Coarse ordering
        list.Sort((a, b) => a.Order.CompareTo(b.Order));

        // 2) Build dependency graph for Before/After constraints
        // Edge A -> B means A must run before B.
        var graph = new Dictionary<Type, HashSet<Type>>();
        foreach (var d in list)
            graph[d.MiddlewareType] = new HashSet<Type>();

        foreach (var d in list)
        {
            foreach (var before in d.Before)
            {
                if (graph.ContainsKey(before))
                    graph[before].Add(d.MiddlewareType);
            }

            foreach (var after in d.After)
            {
                if (graph.ContainsKey(after))
                    graph[d.MiddlewareType].Add(after);
            }
        }

        var orderedTypes = TopologicalSort(graph, list.Select(d => d.MiddlewareType).ToList());
        var byType = list.ToDictionary(d => d.MiddlewareType, d => d);

        return orderedTypes.Select(t => byType[t]).ToList();
    }

    private static IReadOnlyList<Type> TopologicalSort(
        IDictionary<Type, HashSet<Type>> graph,
        IReadOnlyList<Type> initialOrder)
    {
        var result = new List<Type>(graph.Count);
        var visiting = new HashSet<Type>();
        var visited = new HashSet<Type>();

        void Visit(Type node)
        {
            if (visited.Contains(node))
                return;

            if (!visiting.Add(node))
                throw new InvalidOperationException(
                    $"Cycle detected in middleware ordering involving '{node.FullName}'.");

            foreach (var dep in graph[node])
                Visit(dep);

            visiting.Remove(node);
            visited.Add(node);
            result.Add(node);
        }

        foreach (var node in initialOrder)
            Visit(node);

        return result;
    }
}
