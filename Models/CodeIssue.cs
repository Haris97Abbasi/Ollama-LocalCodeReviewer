namespace OfflineDotNetCodeReviewer.Models;

public sealed record CodeIssue(
    string Category,
    string Severity,
    string Explanation,
    string SuggestedFix);
