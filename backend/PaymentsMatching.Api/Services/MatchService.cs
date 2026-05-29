using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PaymentsMatching.Api.Data;
using PaymentsMatching.Api.DTOs;
using PaymentsMatching.Api.Models;

namespace PaymentsMatching.Api.Services;

public sealed class MatchService
{
    private readonly AppDbContext _dbContext;
    private readonly CsvParserService _csvParserService;

    /// <summary>
    /// Construct the <see cref="MatchService"/>.
    /// </summary>
    /// <param name="dbContext">Application database context.</param>
    /// <param name="csvParserService">Service for parsing CSV files.</param>
    public MatchService(AppDbContext dbContext, CsvParserService csvParserService)
    {
        _dbContext = dbContext;
        _csvParserService = csvParserService;
    }

    /// <summary>
    /// Parse uploaded CSV files, compute per-key match results, persist all results,
    /// and return unresolved records along with a summary for the process response.
    /// </summary>
    /// <param name="systemFile">Uploaded system CSV file.</param>
    /// <param name="providerFile">Uploaded provider CSV file.</param>
    public async Task<MatchResponseDto> RunMatchAsync(IFormFile systemFile, IFormFile providerFile)
    {
        var systemRows = await _csvParserService.ParseAsync(systemFile);
        var providerRows = await _csvParserService.ParseAsync(providerFile);

        var systemMap = systemRows.ToDictionary(RecordKey, StringComparer.OrdinalIgnoreCase);
        var providerMap = providerRows.ToDictionary(RecordKey, StringComparer.OrdinalIgnoreCase);
        var allKeys = new SortedSet<string>(systemMap.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(providerMap.Keys);

        var previousRecords = await _dbContext.MatchResults.AsNoTracking().ToListAsync();
        var previousMap = previousRecords.ToDictionary(result => RecordKey(result), StringComparer.OrdinalIgnoreCase);

        var records = allKeys.Select(key => CreateMatchResultRecord(
            key,
            systemMap.GetValueOrDefault(key),
            providerMap.GetValueOrDefault(key),
            previousMap.GetValueOrDefault(key))).ToList();

        // Persist all records (so both resolved and unresolved can be queried later)
        await ReplaceAllMatchResultsAsync(records);

        // For the process API return only unresolved records to the UI
        var unresolvedRecords = records.Where(record => record.Status != MatchStatus.Matched).ToList();

        return BuildResponse(unresolvedRecords, records);
    }

    /// <summary>
    /// Retrieve persisted match records from the database. When <paramref name="filter"/>
    /// is null or 'all', this returns all records; otherwise returns records matching
    /// the requested resolution state ('resolved' or 'unresolved'). This method returns
    /// only the list of record DTOs (no summary).
    /// </summary>
    /// <param name="filter">Optional filter for resolution state.</param>
    public async Task<IEnumerable<PaymentMatchRecordDto>> GetMatchesAsync(string? filter)
    {
        var query = _dbContext.MatchResults.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim().ToLowerInvariant();
            query = filter switch
            {
                // Consider a record 'resolved' for filtering when its status is Matched.
                // Any status other than Matched is treated as 'unresolved'. This keeps
                // backend filtering aligned with the UI's status semantics.
                "resolved" => query.Where(result => result.Status == MatchStatus.Matched),
                "unresolved" => query.Where(result => result.Status != MatchStatus.Matched),
                _ => query,
            };
        }

        var records = await query.OrderBy(r => r.OrderId).ThenBy(r => r.Currency).ToListAsync();
        return records.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Mark a persisted match result as resolved and set the resolution side (System or Provider).
    /// </summary>
    /// <param name="request">DTO containing the record id and resolution side.</param>
    /// <returns>The updated record as a DTO.</returns>
    public async Task<PaymentMatchRecordDto> ResolveAsync(ResolveRequestDto request)
    {
        if (!Guid.TryParse(request.RecordId, out var recordId))
        {
            throw new KeyNotFoundException($"Match result with id '{request.RecordId}' was not found.");
        }

        var record = await _dbContext.MatchResults.FindAsync(recordId);

        if (record is null)
        {
            throw new KeyNotFoundException($"Match result with id '{request.RecordId}' was not found.");
        }

        if (!Enum.TryParse<ResolutionSide>(request.ResolutionSide, ignoreCase: true, out var resolutionSide))
        {
            throw new InvalidOperationException("ResolutionSide must be either System or Provider.");
        }

        record.Resolved = true;
        record.ResolutionSide = resolutionSide;

        await _dbContext.SaveChangesAsync();

        return MapToDto(record);
    }

    // Create the canonical per-row key used for matching: "orderId|CURRENCY".
    private static string RecordKey(PaymentRecord record)
        => $"{record.OrderId.Trim()}|{record.Currency.Trim().ToUpperInvariant()}";

    // Create the canonical key for a stored MatchResult: "orderId|CURRENCY".
    private static string RecordKey(MatchResult record)
        => $"{record.OrderId.Trim()}|{record.Currency.Trim().ToUpperInvariant()}";

    // Create a MatchResult entity for the given key using available system/provider rows
    // and carry over resolution state from a previous persisted record if present.
    private static MatchResult CreateMatchResultRecord(string key, PaymentRecord? systemRecord, PaymentRecord? providerRecord, MatchResult? previousRecord)
    {
        var parts = key.Split('|');
        var orderId = parts[0];
        var currency = parts[1];
        var systemAmount = systemRecord?.Amount;
        var providerAmount = providerRecord?.Amount;
        var status = DetermineStatus(systemRecord, providerRecord);

        return new MatchResult
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Currency = currency,
            SystemAmount = systemAmount,
            ProviderAmount = providerAmount,
            Status = status,
            Resolved = previousRecord?.Resolved ?? false,
            ResolutionSide = previousRecord?.ResolutionSide,
            CreatedAt = DateTime.UtcNow,
        };
    }

    // Determine the MatchStatus for the supplied pair of payment records.
    private static MatchStatus DetermineStatus(PaymentRecord? systemRecord, PaymentRecord? providerRecord)
    {
        if (systemRecord is not null && providerRecord is not null)
        {
            return systemRecord.Amount == providerRecord.Amount ? MatchStatus.Matched : MatchStatus.AmountMismatch;
        }

        return systemRecord is not null ? MatchStatus.OnlySystem : MatchStatus.OnlyProvider;
    }

    // Replace all persisted match results with the supplied list (used after processing a run).
    private async Task ReplaceAllMatchResultsAsync(List<MatchResult> records)
    {
        _dbContext.MatchResults.RemoveRange(_dbContext.MatchResults);
        await _dbContext.SaveChangesAsync();
        await _dbContext.MatchResults.AddRangeAsync(records);
        await _dbContext.SaveChangesAsync();
    }

    private static MatchResponseDto BuildResponse(IEnumerable<MatchResult> records, IEnumerable<MatchResult> allRecords)
    {
        var responseList = records.Select(MapToDto).ToArray();
        var uniqueOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in allRecords)
        {
            uniqueOrderIds.Add(record.OrderId.Trim());
        }

        return new MatchResponseDto
        {
            Summary = new MatchSummaryDto
            {
                Total = uniqueOrderIds.Count,
                Matched = allRecords.Count(r => r.Status == MatchStatus.Matched),
                OnlySystem = allRecords.Count(r => r.Status == MatchStatus.OnlySystem),
                OnlyProvider = allRecords.Count(r => r.Status == MatchStatus.OnlyProvider),
                AmountMismatch = allRecords.Count(r => r.Status == MatchStatus.AmountMismatch),
            },
            Records = responseList,
        };
    }

    private static PaymentMatchRecordDto MapToDto(MatchResult result)
        => new PaymentMatchRecordDto
        {
            Id = result.Id.ToString(),
            OrderId = result.OrderId,
            Currency = result.Currency,
            SystemAmount = result.SystemAmount,
            ProviderAmount = result.ProviderAmount,
            Status = result.Status,
            Resolved = result.Resolved,
            ResolutionSide = result.ResolutionSide?.ToString(),
        };
}
