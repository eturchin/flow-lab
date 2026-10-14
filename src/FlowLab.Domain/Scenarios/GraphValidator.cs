namespace FlowLab.Domain;

internal static class GraphValidator
{
    public static List<string> Validate(ScenarioSettings settings)
    {
        var errors = new List<string>();
        var connections = settings.Connections!;
        if (!Enum.IsDefined(settings.SourceRouting))
        {
            errors.Add("Неизвестная маршрутизация источника.");
        }

        if (connections.Count > 512 || connections.Any(e => e is null || string.IsNullOrWhiteSpace(e.From) || string.IsNullOrWhiteSpace(e.To)))
        {
            return ["Укажите корректные связи (не более 512)."];
        }

        var ids = settings.Nodes.Select(n => n.Id).ToHashSet();
        if (connections.Any(e => e.To == "source" || !ids.Contains(e.To) || e.From != "source" && !ids.Contains(e.From) || e.From == e.To))
        {
            return ["Связь должна соединять существующие разные узлы; источник не может быть целью."];
        }

        if (connections.Distinct().Count() != connections.Count)
        {
            errors.Add("Связи не должны повторяться.");
        }

        var indegree = ids.ToDictionary(id => id, id => connections.Count(e => e.To == id && e.From != "source"));
        var pending = new Queue<string>(ids.Where(id => indegree[id] == 0));
        var visited = 0;
        while (pending.TryDequeue(out var id))
        {
            visited++;
            foreach (var edge in connections.Where(e => e.From == id))
            {
                if (--indegree[edge.To] == 0)
                {
                    pending.Enqueue(edge.To);
                }
            }
        }
        if (visited != ids.Count)
        {
            errors.Add("Циклические связи не поддерживаются.");
        }

        var reachable = new HashSet<string> { "source" };
        var frontier = new Queue<string>();
        frontier.Enqueue("source");
        while (frontier.TryDequeue(out var id))
        {
            foreach (var edge in connections.Where(e => e.From == id))
            {
                if (reachable.Add(edge.To))
                {
                    frontier.Enqueue(edge.To);
                }
            }
        }

        if (ids.Any(id => !reachable.Contains(id)))
        {
            errors.Add("Каждый узел должен быть достижим от источника.");
        }

        foreach (var node in settings.Nodes.Where(n => n.Kind is NodeKind.Queue or NodeKind.Broker))
        {
            var targets = connections.Where(e => e.From == node.Id).Select(e => e.To).ToList();
            if (targets.Count == 0 || node.Kind == NodeKind.Queue && targets.Count != 1)
            {
                errors.Add($"{node.Name}: буферу нужен один сервис, брокеру — хотя бы один консьюмер.");
            }

            foreach (var target in settings.Nodes.Where(n => targets.Contains(n.Id)))
            {
                if (target.Kind is NodeKind.Queue or NodeKind.Broker || connections.Count(e => e.To == target.Id) != 1)
                {
                    errors.Add($"{node.Name}: подключайте отдельные сервисы-консьюмеры без других входящих связей.");
                }
            }
        }
        return errors;
    }
}
