using OfflineDotNetCodeReviewer.Models;
using OfflineDotNetCodeReviewer.Services;

const int ExitInvalidInput = 1;
const int ExitReviewFailed = 2;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <path-to-file.cs>");
    Console.Error.WriteLine("Example: dotnet run -- \"Samples/BadCustomerService.cs\"");
    return ExitInvalidInput;
}

var filePath = Path.GetFullPath(args[0]);

if (!string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Error: '{args[0]}' is not a C# source file. Please supply a .cs file.");
    return ExitInvalidInput;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: file not found: {filePath}");
    return ExitInvalidInput;
}

string sourceCode;
try
{
    sourceCode = await File.ReadAllTextAsync(filePath);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Error: could not read '{filePath}': {ex.Message}");
    return ExitInvalidInput;
}

if (string.IsNullOrWhiteSpace(sourceCode))
{
    Console.Error.WriteLine($"Error: '{filePath}' is empty, so there is nothing to review.");
    return ExitInvalidInput;
}

var fileName = Path.GetFileName(filePath);

Console.WriteLine($"Reviewing {fileName} locally with {OllamaCodeReviewService.Model}.");
Console.WriteLine("This can take a few minutes on CPU...");
Console.WriteLine();

using var reviewService = new OllamaCodeReviewService();

try
{
    var result = await reviewService.ReviewAsync(fileName, sourceCode);

    PrintReport(fileName, result);
    return 0;
}
catch (OllamaException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Please check that:");
    Console.Error.WriteLine("  1. Ollama is running (start the Ollama app or run 'ollama serve').");
    Console.Error.WriteLine($"  2. The model is installed: 'ollama list' should show {OllamaCodeReviewService.Model}.");
    Console.Error.WriteLine($"     If it is missing, run 'ollama pull {OllamaCodeReviewService.Model}'.");
    return ExitReviewFailed;
}

static void PrintReport(string fileName, CodeReviewResult result)
{
    Console.WriteLine("OFFLINE .NET CODE REVIEW");
    Console.WriteLine("========================");
    Console.WriteLine($"Model: {OllamaCodeReviewService.Model}");
    Console.WriteLine($"File: {fileName}");
    Console.WriteLine();
    Console.WriteLine($"Summary: {result.Summary}");
    Console.WriteLine();
    Console.WriteLine($"Issues: {result.Issues.Count}");

    foreach (var issue in result.Issues.OrderBy(issue => SeverityRank(issue.Severity)))
    {
        Console.WriteLine();
        Console.WriteLine($"[{issue.Severity.ToUpperInvariant()}] {issue.Category}");
        Console.WriteLine($"  Explanation: {issue.Explanation}");
        Console.WriteLine($"  Suggested fix: {issue.SuggestedFix}");
    }
}

// Most severe first; anything unrecognised goes last.
static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
{
    "CRITICAL" => 0,
    "HIGH" => 1,
    "MEDIUM" => 2,
    "LOW" => 3,
    _ => 4
};
