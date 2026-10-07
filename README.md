# Offline .NET Code Reviewer

**Privacy-first AI code review with .NET, Ollama and Qwen2.5-Coder.** Source code is analyzed locally and never needs to be sent to a cloud LLM.

## What it does

A small .NET console application that reviews a single C# source file. You pass it the path of a `.cs` file; it reads the file, asks a coding model running on your own machine to review it, and prints a structured report of issues with a severity, an explanation and a suggested fix for each.

No cloud LLM, no API key, no account.

## Why local AI?

Many teams cannot paste proprietary code into a hosted AI service: client contracts, regulated data, unreleased products or company policy forbid it. Running the model locally removes that obstacle.

- **Privacy:** the source file goes to `localhost` and nowhere else.
- **No API key or per-token cost:** the model runs on hardware you already own.
- **Works offline:** once the model is downloaded, no internet connection is needed.
- **Full control:** you choose the model and the prompt, and nothing changes underneath you.

The trade-off is speed and depth: a 7B model on a laptop is slower and less thorough than a large hosted model. See [Limitations](#limitations).

## Architecture

```
.NET Console App  ->  Ollama local API (http://localhost:11434)  ->  Qwen2.5-Coder 7B
```

| Path | Purpose |
|---|---|
| `Program.cs` | CLI: validates the input file, calls the review service, prints the report |
| `Services/OllamaCodeReviewService.cs` | Calls Ollama's `/api/chat` endpoint and parses the JSON reply |
| `Services/ICodeReviewService.cs` | Review service interface |
| `Prompts/CodeReviewPrompt.cs` | System prompt and user message |
| `Models/` | `CodeReviewResult` and `CodeIssue` records |
| `Samples/BadCustomerService.cs` | Deliberately flawed file for the demo (not compiled into the app) |

The app uses only `HttpClient` and `System.Text.Json` from the .NET base library; there are no NuGet dependencies. The reply is constrained with a JSON schema passed in Ollama's `format` field, so it can be deserialized straight into C# records.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/download)
- About 5 GB of disk space for the model

## Setup

1. Install Ollama and make sure it is running (start the Ollama app, or run `ollama serve`).
2. Download the model:

   ```
   ollama pull qwen2.5-coder:7b
   ```

3. Check it is installed:

   ```
   ollama list
   ```

## Run the demo

```
git clone https://github.com/Haris97Abbasi/Ollama-LocalCodeReviewer.git
cd Ollama-LocalCodeReviewer
dotnet run -- "Samples/BadCustomerService.cs"
```

To review your own file, pass its path instead:

```
dotnet run -- "C:\path\to\YourClass.cs"
```

## Example output

Real output from reviewing the included sample:

```
Reviewing BadCustomerService.cs locally with qwen2.5-coder:7b.
This can take a few minutes on CPU...

OFFLINE .NET CODE REVIEW
========================
Model: qwen2.5-coder:7b
File: BadCustomerService.cs

Summary: The file contains several issues that could lead to bugs, security vulnerabilities, and performance problems.

Issues: 4

[HIGH] Bug
  Explanation: The `GetEmailDomain` method does not handle the case where the customer's email is null or does not contain an '@' symbol, which could cause a `NullReferenceException` or `ArgumentOutOfRangeException`.
  Suggested fix: Add null checks and validation for the email string before splitting it.

[HIGH] Security
  Explanation: The `ApiKey` constant is hard-coded in the code, which could expose it if the code is shared or if the file is accidentally committed to a version control system.
  Suggested fix: Store the API key in a secure configuration file or environment variable.

[MEDIUM] Performance
  Explanation: The `ExportCustomers` method reads all customers into memory and then writes them to a file, which could be inefficient if the list of customers is large.
  Suggested fix: Consider using a streaming approach to write the file, or only read the necessary customers into memory.

[MEDIUM] Testing
  Explanation: The file does not contain any test cases, which makes it difficult to ensure that the code works correctly.
  Suggested fix: Write unit tests for each method to verify its correctness.
```

## Error handling

The app exits with a clear message instead of a stack trace:

| Situation | Exit code |
|---|---|
| No file, more than one file, a missing file, a non-`.cs` file or an empty file | 1 |
| Ollama is not running, the model is not installed, or the request times out | 2 |

When Ollama cannot be used, the message tells you to start Ollama and check that `qwen2.5-coder:7b` is installed.

## Safety and privacy

- Reads only the one file you pass on the command line.
- Sends that file only to Ollama on `localhost:11434`.
- Never modifies files and never runs shell commands.

## Limitations

- **It misses things.** The sample file contains five planted defects (hard-coded secret, null dereference, swallowed exception, repeated enumeration, missing input validation). In testing, the 7B model found two to four of them per run and never all five; the repeated enumeration was never reported. Results also vary from run to run. Treat the output as a first-pass assistant, not a replacement for human review.
- **It can be wrong.** Some findings are generic or mistaken, as in the "Performance" and "Testing" entries above.
- **It is slow on CPU.** Reviewing the 64-line sample took roughly 2.5 to 4.5 minutes on the development machine without a GPU. The request times out after 10 minutes.
- **One file at a time.** The model sees only the file you pass, so it cannot judge anything that depends on other files in the project.
- **Small files only.** Large files may exceed the model's context window and be reviewed incompletely.
- **Fixed configuration.** The model name and Ollama address are constants in `OllamaCodeReviewService.cs`.

## License

MIT. See [LICENSE](LICENSE).
