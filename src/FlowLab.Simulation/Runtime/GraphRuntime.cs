using FlowLab.Domain;

namespace FlowLab.Simulation;

internal sealed partial class SimulationSession
{
    private readonly Dictionary<string, int> graphStations = [];
    private readonly Dictionary<int, int[]> brokerConsumers = [];
    private readonly Dictionary<int, int> consumerBrokers = [];
    private readonly Dictionary<string, int> routingCursor = [];
    private readonly Dictionary<string, string[]> outgoing = [];
    private readonly Dictionary<string, int> incomingCount = [];
    private readonly Dictionary<string, NodeSettings> graphNodes = [];
    private readonly Dictionary<string, int> graphOrdinals = [];
    private int[] graphParentCounts = [];

    private void InitializeGraph()
    {
        var edges = settings.Connections!;
        foreach (var id in settings.Nodes.Select(n => n.Id).Append("source"))
        {
            outgoing[id] = edges.Where(e => e.From == id).Select(e => e.To).ToArray();
        }

        foreach (var node in settings.Nodes)
        {
            graphOrdinals[node.Id] = graphNodes.Count;
            graphNodes[node.Id] = node;
            incomingCount[node.Id] = edges.Count(e => e.To == node.Id);
            if (node.Kind == NodeKind.Queue)
            {
                continue;
            }

            var bufferId = edges.FirstOrDefault(e => e.To == node.Id)?.From;
            var buffer = settings.Nodes.FirstOrDefault(n => n.Id == bufferId && n.Kind == NodeKind.Queue);
            var effective = node.Kind == NodeKind.Broker
                ? node with { Workers = settings.Nodes.Where(n => outgoing[node.Id].Contains(n.Id)).Sum(n => n.Workers) }
                : node;
            graphStations[node.Id] = stations.Count;
            stations.Add(new(effective, buffer?.QueueCapacity ?? node.QueueCapacity, Resilience, buffer?.QueueDiscipline ?? node.QueueDiscipline));
        }
        graphParentCounts = settings.Nodes.Select(n => incomingCount[n.Id]).ToArray();
        foreach (var broker in settings.Nodes.Where(n => n.Kind == NodeKind.Broker))
        {
            var index = graphStations[broker.Id];
            brokerConsumers[index] = outgoing[broker.Id].Select(id => graphStations[id]).ToArray();
            foreach (var consumer in brokerConsumers[index])
            {
                consumerBrokers[consumer] = index;
            }
        }
    }

    private int Choose(string id, int count)
    {
        var cursor = routingCursor.GetValueOrDefault(id);
        routingCursor[id] = (cursor + 1) % count;
        return cursor;
    }

    private void BeginGraph(SimulatedRequest request)
    {
        var version = request.Version;
        var remainingParents = (int[])graphParentCounts.Clone();
        var enabled = new bool[settings.Nodes.Count];
        var remainingNodes = settings.Nodes.Count;
        bool Alive() => !request.IsCompleted && request.Version == version;

        void Finish(string id, bool executed, string? chosen = null)
        {
            if (!Alive())
            {
                return;
            }

            remainingNodes--;
            var targets = outgoing[id];
            var node = graphNodes[id];
            if (executed && chosen is null && node.Routing == RoutingMode.RoundRobin && targets.Length > 0)
            {
                chosen = targets[Choose(id, targets.Length)];
            }

            foreach (var target in targets)
            {
                if (!Alive())
                {
                    return;
                }

                Resolve(target, executed && (chosen is null || chosen == target));
            }
            if (remainingNodes == 0 && Alive())
            {
                CompleteRequest(request, null);
            }
        }

        void Resolve(string id, bool active)
        {
            if (!Alive())
            {
                return;
            }

            var ordinal = graphOrdinals[id];
            enabled[ordinal] |= active;
            if (--remainingParents[ordinal] != 0)
            {
                return;
            }

            if (!enabled[ordinal]) { Finish(id, false); return; }
            var node = graphNodes[id];
            if (node.Kind == NodeKind.Queue) { Finish(id, true); return; }
            var index = graphStations[id];
            if (node.Kind == NodeKind.Broker)
            {
                AcceptMessage(index, new(request, version, () => { }, OnDispatch: consumer => Finish(id, true, stations[consumer].Settings.Id)));
            }
            else
            {
                Visit(request, index, () => Finish(id, true), consumerBrokers.TryGetValue(index, out var broker) ? () => ReleaseDelivery(broker) : null);
            }
        }

        var roots = outgoing["source"];
        var selected = settings.SourceRouting == RoutingMode.RoundRobin ? roots[Choose("source", roots.Length)] : null;
        foreach (var root in roots)
        {
            Resolve(root, selected is null || selected == root);
        }
    }

    private int AvailableConsumer(int broker)
    {
        var consumers = brokerConsumers[broker];
        var key = stations[broker].Settings.Id;
        var start = routingCursor.GetValueOrDefault(key);
        for (var offset = 0; offset < consumers.Length; offset++)
        {
            var position = (start + offset) % consumers.Length;
            var consumer = consumers[position];
            if (stations[consumer].Busy >= stations[consumer].Settings.Workers)
            {
                continue;
            }

            routingCursor[key] = (position + 1) % consumers.Length;
            return consumer;
        }
        return -1;
    }

    private void AcceptMessage(int broker, QueuedRequest message)
    {
        var station = stations[broker];
        // Completions and arrivals can share a timestamp. Existing messages retain queue priority.
        if (station.QueueLength > 0)
        {
            DrainBroker(broker);
        }

        var consumer = AvailableConsumer(broker);
        if (consumer >= 0)
        {
            DispatchMessage(broker, consumer, message);
        }
        else if (station.QueueLength >= station.Capacity)
        {
            FailAttempt(message.Request, "QueueFull");
        }
        else
        {
            station.Enqueue(message);
        }
    }

    private void DispatchMessage(int broker, int consumer, QueuedRequest message)
    {
        var station = stations[broker];
        station.Busy++;
        station.Started++;
        station.MaxBusy = Math.Max(station.MaxBusy, station.Busy);
        message.OnDispatch!(consumer);
    }

    private void ReleaseDelivery(int broker)
    {
        var station = stations[broker];
        station.Busy--;
        calendar.Schedule(now, () => DrainBroker(broker));
    }

    private void DrainBroker(int broker)
    {
        var station = stations[broker];
        while (station.QueueLength > 0)
        {
            var consumer = AvailableConsumer(broker);
            if (consumer < 0 || !station.TryDequeue(out var message))
            {
                break;
            }

            if (message.IsAlive)
            {
                DispatchMessage(broker, consumer, message);
            }
        }
    }
}
