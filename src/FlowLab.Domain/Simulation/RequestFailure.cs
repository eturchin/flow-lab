namespace FlowLab.Domain;

public sealed record RequestFailure(int RequestId, double CreatedAtMs, double CompletedAtMs, int Attempts, string Reason);
