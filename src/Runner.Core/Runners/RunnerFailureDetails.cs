namespace Runner.Core.Runners;

public sealed record RunnerFailureDetails(
    DateTimeOffset Timestamp,
    string Reason,
    int? ExitCode,
    string? ExceptionMessage,
    IReadOnlyList<string> DiagnosticLines);
