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
    /// Parse uploaded CSV files, compute per-key match results,
    /// persist all results under a unique reconciliation session,
    /// and return unresolved records along with a summary.
    /// </summary>
    public async Task<MatchResponseDto> RunMatchAsync( IFormFile systemFile, IFormFile providerFile){
        // ============================================
        // Create NEW reconciliation session
        // ============================================
        var session = new ReconciliationSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            File1Name = systemFile.FileName,
            File2Name = providerFile.FileName,
        };

        _dbContext.ReconciliationSessions.Add(session);

        await _dbContext.SaveChangesAsync();

        // ============================================
        // Parse CSV files
        // ============================================
        var systemRows = await _csvParserService.ParseAsync(systemFile);
        var providerRows = await _csvParserService.ParseAsync(providerFile);

        // ============================================
        // Build maps
        // ============================================
        var systemMap = systemRows.ToDictionary(
            RecordKey,
            StringComparer.OrdinalIgnoreCase);

        var providerMap = providerRows.ToDictionary(
            RecordKey,
            StringComparer.OrdinalIgnoreCase);

        var allKeys = new SortedSet<string>(
            systemMap.Keys,
            StringComparer.OrdinalIgnoreCase);

        allKeys.UnionWith(providerMap.Keys);

        // ============================================
        // Create records
        // ============================================
        var records = allKeys.Select(key =>
            CreateMatchResultRecord(
                session.Id,
                key,
                systemMap.GetValueOrDefault(key),
                providerMap.GetValueOrDefault(key)))
            .ToList();

        // ============================================
        // Save records
        // ============================================
        await SaveMatchResultsAsync(records);

        // ============================================
        // Return unresolved only
        // ============================================
        var unresolvedRecords = records
            .Where(record => record.Status != MatchStatus.Matched)
            .ToList();

        return BuildResponse(unresolvedRecords, records);
    }

    /// <summary>
    /// Retrieve persisted match records.
    /// </summary>
    public async Task<IEnumerable<PaymentMatchRecordDto>> GetMatchesAsync(
        string? filter)
    {
        var query = _dbContext.MatchResults.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim().ToLowerInvariant();

            query = filter switch
            {
                "resolved" => query.Where(
                    result => result.Status == MatchStatus.Matched),

                "unresolved" => query.Where(
                    result => result.Status != MatchStatus.Matched),

                _ => query,
            };
        }

        var records = await query
            .OrderBy(r => r.OrderId)
            .ThenBy(r => r.Currency)
            .ToListAsync();

        return records.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Mark a persisted match result as resolved.
    /// </summary>
    public async Task<PaymentMatchRecordDto> ResolveAsync(
        ResolveRequestDto request)
    {
        if (!Guid.TryParse(request.RecordId, out var recordId))
        {
            throw new KeyNotFoundException(
                $"Match result with id '{request.RecordId}' was not found.");
        }

        var record = await _dbContext.MatchResults.FindAsync(recordId);

        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Match result with id '{request.RecordId}' was not found.");
        }

        if (!Enum.TryParse<ResolutionSide>(
            request.ResolutionSide,
            ignoreCase: true,
            out var resolutionSide))
        {
            throw new InvalidOperationException(
                "ResolutionSide must be either System or Provider.");
        }

        record.Resolved = true;

        record.ResolutionSide = resolutionSide;

        await _dbContext.SaveChangesAsync();

        return MapToDto(record);
    }

    // ============================================
    // Record Key Helpers
    // ============================================

    private static string RecordKey(PaymentRecord record)
        => $"{record.OrderId.Trim()}|{record.Currency.Trim().ToUpperInvariant()}";

    private static string RecordKey(MatchResult record)
        => $"{record.OrderId.Trim()}|{record.Currency.Trim().ToUpperInvariant()}";

    // ============================================
    // Create Match Result
    // ============================================

    private static MatchResult CreateMatchResultRecord(
        Guid sessionId,
        string key,
        PaymentRecord? systemRecord,
        PaymentRecord? providerRecord)
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

            SessionId = sessionId,

            OrderId = orderId,

            Currency = currency,

            SystemAmount = systemAmount,

            ProviderAmount = providerAmount,

            Status = status,

            Resolved = false,

            ResolutionSide = null,

            CreatedAt = DateTime.UtcNow,
        };
    }

    // ============================================
    // Determine Match Status
    // ============================================

    private static MatchStatus DetermineStatus(
        PaymentRecord? systemRecord,
        PaymentRecord? providerRecord)
    {
        if (systemRecord is not null &&
            providerRecord is not null)
        {
            return systemRecord.Amount == providerRecord.Amount
                ? MatchStatus.Matched
                : MatchStatus.AmountMismatch;
        }

        return systemRecord is not null
            ? MatchStatus.OnlySystem
            : MatchStatus.OnlyProvider;
    }

    // ============================================
    // Save Records
    // ============================================

    private async Task SaveMatchResultsAsync(
        List<MatchResult> records)
    {
        await _dbContext.MatchResults.AddRangeAsync(records);

        await _dbContext.SaveChangesAsync();
    }

    // ============================================
    // Build Response
    // ============================================

    private static MatchResponseDto BuildResponse(
        IEnumerable<MatchResult> records,
        IEnumerable<MatchResult> allRecords)
    {
        var responseList = records
            .Select(MapToDto)
            .ToArray();

        var uniqueOrderIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var record in allRecords)
        {
            uniqueOrderIds.Add(record.OrderId.Trim());
        }

        return new MatchResponseDto
        {
            Summary = new MatchSummaryDto
            {
                Total = uniqueOrderIds.Count,

                Matched = allRecords.Count(
                    r => r.Status == MatchStatus.Matched),

                OnlySystem = allRecords.Count(
                    r => r.Status == MatchStatus.OnlySystem),

                OnlyProvider = allRecords.Count(
                    r => r.Status == MatchStatus.OnlyProvider),

                AmountMismatch = allRecords.Count(
                    r => r.Status == MatchStatus.AmountMismatch),
            },

            Records = responseList,
        };
    }

    // ============================================
    // DTO Mapper
    // ============================================

    private static PaymentMatchRecordDto MapToDto(
        MatchResult result)
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