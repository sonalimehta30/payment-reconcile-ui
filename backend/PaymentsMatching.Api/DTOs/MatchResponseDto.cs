using PaymentsMatching.Api.Models;

namespace PaymentsMatching.Api.DTOs;

public sealed class MatchResponseDto
{
    public Guid SessionId { get; set; }
    public MatchSummaryDto Summary { get; set; } = null!;
    public IReadOnlyList<PaymentMatchRecordDto> Records { get; set; } = Array.Empty<PaymentMatchRecordDto>();
}

public sealed class MatchSummaryDto
{
    public int Total { get; set; }
    public int Matched { get; set; }
    public int OnlySystem { get; set; }
    public int OnlyProvider { get; set; }
    public int AmountMismatch { get; set; }
}

public sealed class PaymentMatchRecordDto
{
    public string Id { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal? SystemAmount { get; set; }
    public decimal? ProviderAmount { get; set; }
    public MatchStatus Status { get; set; }
    public bool Resolved { get; set; }
    public string? ResolutionSide { get; set; }
}
