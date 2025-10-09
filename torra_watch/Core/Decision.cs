namespace torra_watch.Core
{
    public enum DecisionKind { CandidateFound, Cooldown }

    // Primary constructor parameters must be exactly these names
    public sealed record Decision(
        DecisionKind kind,
        DateTime timeUtc,
        string? symbol,
        decimal? ret3h,
        DateTime? nextCheckUtc,
        string note
    );
}
