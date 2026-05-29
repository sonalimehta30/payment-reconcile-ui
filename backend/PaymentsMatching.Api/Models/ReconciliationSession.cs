namespace PaymentsMatching.Api.Models;

public sealed class ReconciliationSession
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? File1Name { get; set; }

    public string? File2Name { get; set; }

    public ICollection<MatchResult> MatchResults { get; set; }
        = new List<MatchResult>();
}