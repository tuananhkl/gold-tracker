using GoldTracker.Application.Contracts;
using GoldTracker.Application.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GoldTracker.Api.Endpoints;

public static class PricesEndpoints
{
  public static void MapPricesV1Endpoints(this IEndpointRouteBuilder app)
  {
    app.MapGet("/api/prices/latest", GetLatestPricesV1)
      .WithName("GetLatestPricesV1")
      .WithTags("Prices")
      .Produces<LatestPricesResponse>(StatusCodes.Status200OK)
      .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

    app.MapGet("/api/prices/history", GetPriceHistoryV1)
      .WithName("GetPriceHistoryV1")
      .WithTags("Prices")
      .Produces<PriceHistoryResponse>(StatusCodes.Status200OK)
      .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status404NotFound);

    app.MapGet("/api/prices/changes", GetPriceChangesV1)
      .WithName("GetPriceChangesV1")
      .WithTags("Prices")
      .Produces<PriceChangesResponse>(StatusCodes.Status200OK)
      .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

    app.MapGet("/api/prices/by-date", GetPricesByDateV1)
      .WithName("GetPricesByDateV1")
      .WithTags("Prices")
      .Produces<PricesByDateResponse>(StatusCodes.Status200OK)
      .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
  }

  private static async Task<IResult> GetLatestPricesV1(
    [FromQuery] string? kind,
    [FromQuery] string? brand,
    [FromQuery] string? region,
    IPriceV1Query query,
    CancellationToken ct)
  {
    // Validate
    var (isValid, errorMessage) = ValidationHelpers.ValidateLatestQuery(kind, brand, region);
    if (!isValid)
    {
      return Results.Problem(
        detail: errorMessage,
        statusCode: 400,
        title: "Validation Error"
      );
    }

    var latestQuery = new LatestQuery(kind, brand, region);
    var response = await query.GetLatestAsync(latestQuery, ct);
    return Results.Ok(response);
  }

  private static async Task<IResult> GetPriceHistoryV1(
    [FromQuery] string? kind,
    [FromQuery] string? brand,
    [FromQuery] string? region,
    [FromQuery] DateOnly? from,
    [FromQuery] DateOnly? to,
    [FromQuery] int? days,
    IPriceV1Query query,
    CancellationToken ct)
  {
    // Validate
    var (isValid, errorMessage) = ValidationHelpers.ValidateHistoryQuery(kind, brand, region, from, to, days);
    if (!isValid)
    {
      return Results.Problem(
        detail: errorMessage,
        statusCode: 400,
        title: "Validation Error"
      );
    }

    try
    {
      var historyQuery = new HistoryQuery(kind, brand, region, from, to, days);
      var response = await query.GetHistoryAsync(historyQuery, ct);
      return Results.Ok(response);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot resolve product"))
    {
      return Results.Problem(
        detail: ex.Message,
        statusCode: 404,
        title: "Product Not Found"
      );
    }
  }

  private static async Task<IResult> GetPriceChangesV1(
    [FromQuery] string? kind,
    [FromQuery] string? brand,
    [FromQuery] string? region,
    IPriceV1Query query,
    CancellationToken ct)
  {
    // Validate
    var (isValid, errorMessage) = ValidationHelpers.ValidateChangesQuery(kind, brand, region);
    if (!isValid)
    {
      return Results.Problem(
        detail: errorMessage,
        statusCode: 400,
        title: "Validation Error"
      );
    }

    var changesQuery = new ChangesQuery(kind, brand, region);
    var response = await query.GetChangesAsync(changesQuery, ct);
    return Results.Ok(response);
  }

  private static async Task<IResult> GetPricesByDateV1(
    [FromQuery] string date,
    [FromQuery] string? kind,
    [FromQuery] string? brand,
    [FromQuery] string? region,
    IPriceV1Query query,
    CancellationToken ct)
  {
    // Parse date
    if (string.IsNullOrWhiteSpace(date))
    {
      return Results.Problem(
        detail: "Date parameter is required (format: YYYY-MM-DD)",
        statusCode: 400,
        title: "Validation Error"
      );
    }

    if (!DateOnly.TryParse(date, out var dateOnly))
    {
      return Results.Problem(
        detail: "Invalid date format. Expected YYYY-MM-DD",
        statusCode: 400,
        title: "Validation Error"
      );
    }

    // Validate
    var (isValid, errorMessage) = ValidationHelpers.ValidateByDateQuery(dateOnly, kind, brand, region);
    if (!isValid)
    {
      return Results.Problem(
        detail: errorMessage,
        statusCode: 400,
        title: "Validation Error"
      );
    }

    var byDateQuery = new ByDateQuery(dateOnly, kind, brand, region);
    var response = await query.GetByDateAsync(byDateQuery, ct);
    return Results.Ok(response);
  }
}
