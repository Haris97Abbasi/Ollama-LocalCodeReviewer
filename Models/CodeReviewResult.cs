namespace OfflineDotNetCodeReviewer.Models;

public sealed record CodeReviewResult(
    string Summary,
    List<CodeIssue> Issues);
