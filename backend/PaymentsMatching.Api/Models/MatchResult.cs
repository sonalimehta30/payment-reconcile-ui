namespace PaymentsMatching.Api.Models;

public sealed class MatchResult
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal? SystemAmount { get; set; }
    public decimal? ProviderAmount { get; set; }
    public MatchStatus Status { get; set; }
    public bool Resolved { get; set; }
    public ResolutionSide? ResolutionSide { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum MatchStatus
{
    Matched,
    OnlySystem,
    OnlyProvider,
    AmountMismatch,
}

public enum ResolutionSide
{
    System,
    Provider,
}
