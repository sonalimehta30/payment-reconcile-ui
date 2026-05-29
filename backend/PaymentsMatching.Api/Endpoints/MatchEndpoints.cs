using Microsoft.AspNetCore.Http;
using PaymentsMatching.Api.DTOs;
using PaymentsMatching.Api.Services;

namespace PaymentsMatching.Api.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/match");

        group.MapPost(string.Empty, async (HttpRequest request, MatchService matchService) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Multipart/form-data is required.");
            }

            var form = await request.ReadFormAsync();
            var systemFile = form.Files.GetFile("systemFile");
            var providerFile = form.Files.GetFile("providerFile");

            if (systemFile is null || providerFile is null)
            {
                return Results.BadRequest("Both systemFile and providerFile are required.");
            }

            var result = await matchService.RunMatchAsync(systemFile, providerFile);
            return Results.Ok(result);
        })
        .WithName("RunMatch")
        .WithTags("Match")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<MatchResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet(string.Empty, async (string? filter, MatchService matchService) =>
        {
            var result = await matchService.GetMatchesAsync(filter);
            return Results.Ok(result);
        })
        .WithName("GetMatches")
        .WithTags("Match")
        .Produces<MatchResponseDto>(StatusCodes.Status200OK);

        group.MapPost("resolve", async (ResolveRequestDto request, MatchService matchService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RecordId) || string.IsNullOrWhiteSpace(request.ResolutionSide))
            {
                return Results.BadRequest("A valid recordId and resolutionSide are required.");
            }

            try
            {
                var updatedRecord = await matchService.ResolveAsync(request);
                return Results.Ok(updatedRecord);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("ResolveMatch")
        .WithTags("Match")
        .Accepts<ResolveRequestDto>("application/json")
        .Produces<PaymentMatchRecordDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}
