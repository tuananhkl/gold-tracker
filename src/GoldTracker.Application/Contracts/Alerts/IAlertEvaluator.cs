using GoldTracker.Application.Contracts.Alerts;

namespace GoldTracker.Application.Contracts.Alerts;

public interface IAlertEvaluator
{
  Task<IReadOnlyList<AlertCandidate>> EvaluatePriceJumpAsync(decimal percentThreshold, CancellationToken ct = default);
  Task<IReadOnlyList<AlertCandidate>> EvaluateNoDataAsync(int noDataMinutes, CancellationToken ct = default);
  Task<IReadOnlyList<AlertCandidate>> EvaluateScraperErrorsAsync(CancellationToken ct = default);
}

