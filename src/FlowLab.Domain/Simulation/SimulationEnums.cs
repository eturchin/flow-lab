namespace FlowLab.Domain;

public enum Distribution { Fixed, Exponential }
public enum NodeKind { Service, Queue, External, Broker }
public enum RoutingMode { Broadcast, RoundRobin }
public enum RunStatus { Queued, Running, Completed, Cancelled, Failed }

public enum QueueDiscipline { Fifo, Lifo }
