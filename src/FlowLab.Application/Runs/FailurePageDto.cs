namespace FlowLab.Application;

public sealed record RequestFailureDto(int RequestId, double CreatedAtMs, double CompletedAtMs, int Attempts, string Reason);
public sealed record FailurePageDto(int Total, int Offset, int Limit, IReadOnlyList<RequestFailureDto> Items);
