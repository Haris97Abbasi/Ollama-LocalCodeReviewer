using OfflineDotNetCodeReviewer.Models;

namespace OfflineDotNetCodeReviewer.Services;

public interface ICodeReviewService
{
    Task<CodeReviewResult> ReviewAsync(string fileName, string sourceCode, CancellationToken ct = default);
}
