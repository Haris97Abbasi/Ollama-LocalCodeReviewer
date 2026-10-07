# Plan: Offline .NET Code Reviewer (Ollama + Qwen2.5-Coder 7B)

## Context

The brief (`Offline_DotNet_Code_Reviewer_Ollama_Claude_Code_Task.pdf`) asks for a small, portfolio-ready .NET console app that reviews one local C# file using `qwen2.5-coder:7b` through local Ollama (`http://localhost:11434`). No cloud LLM, no API key; source code never leaves the machine. Timebox is about one hour, so the plan is a finished, readable MVP with no extras.

Environment already checked: .NET SDKs 9.0.101 and 10.0.302 are installed, and `qwen2.5-coder:7b` is already pulled in Ollama. The repository currently holds only the PDF and is not a git repository.

## Decisions

- **Target framework:** `net10.0` (current LTS, installed).
- **Layout:** project at the repo root, so `dotnet run -- "Samples/BadCustomerService.cs"` works straight after cloning.
- **Ollama access:** plain `HttpClient` + `System.Text.Json` against `POST /api/chat`. No NuGet packages.
- **Structured output:** send a JSON schema in Ollama's `format` field, `stream: false`, `temperature: 0`.
- **Out of scope:** web UI, database, Docker, RAG, MCP, file modification, shell execution, the stretch goals.

## Steps

1. **Create the project:** `dotnet new console` at the repo root, producing `OfflineDotNetCodeReviewer.csproj` (net10.0, nullable enabled). Add `<Compile Remove="Samples/**" />` so the deliberately flawed sample is not compiled into the app.
2. **Add `.gitignore`** (standard .NET: `bin/`, `obj/`, `.vs/`, user files).
3. **Models** — `Models/CodeIssue.cs` and `Models/CodeReviewResult.cs` as sealed records, exactly as in the brief:
   - `CodeReviewResult(string Summary, List<CodeIssue> Issues)`
   - `CodeIssue(string Category, string Severity, string Explanation, string SuggestedFix)`
4. **Prompt** — `Prompts/CodeReviewPrompt.cs` with:
   - a system prompt: reviewing C#/.NET code, inspect only the provided source, do not invent missing context, say when something cannot be determined, prioritise real defects over style nitpicks, return JSON only;
   - focus areas: correctness/bugs, security, null handling, exception handling, performance, maintainability, obvious missing tests;
   - allowed categories (Bug, Security, Code Smell, Performance, Maintainability, Testing) and severities (Low, Medium, High, Critical);
   - a method that builds the user message from the file name and source.
5. **Service interface** — `Services/ICodeReviewService.cs` with one method: `Task<CodeReviewResult> ReviewAsync(string fileName, string sourceCode, CancellationToken ct)`.
6. **Ollama service** — `Services/OllamaCodeReviewService.cs`:
   - posts to `http://localhost:11434/api/chat` with model `qwen2.5-coder:7b`, system + user messages, the JSON schema in `format`, `stream: false`;
   - long timeout (about 5 minutes) for CPU inference;
   - parses `message.content` into `CodeReviewResult` (case-insensitive);
   - fallback when parsing fails: strip code fences, take the text from the first `{` to the last `}`, retry; if that still fails, return a result whose summary carries the raw model text and an empty issue list;
   - throws a clear, specific exception for "Ollama not reachable" and "model not found" (HTTP 404).
7. **CLI** — `Program.cs`:
   - require exactly one argument, otherwise print usage;
   - validate that the file exists and has a `.cs` extension;
   - read only that file;
   - print a "reviewing locally…" line, call the service, print the report;
   - friendly errors with non-zero exit codes: bad input, Ollama unavailable ("start Ollama and check `ollama list` shows qwen2.5-coder:7b"), timeout.
8. **Console report** — plain text in the brief's format: header `OFFLINE .NET CODE REVIEW`, Model, File, Summary, issue count, then per issue `[SEVERITY] Category`, `Explanation:`, `Suggested fix:`. Issues sorted Critical → Low.
9. **Demo sample** — `Samples/BadCustomerService.cs`, about 40–50 lines, containing: possible null dereference, swallowed exception, hard-coded secret-like value, repeated enumeration of an `IEnumerable`, missing input validation.
10. **Build and test end to end:**
    - `dotnet build` with no warnings;
    - `dotnet run -- "Samples/BadCustomerService.cs"` and check the review is structured and useful;
    - adjust the prompt if the output is weak.
11. **Test the failure paths:** no argument, missing file, non-`.cs` file, and Ollama stopped (or pointed at a wrong port) to confirm the helpful message.
12. **README.md** — headline from the brief, purpose, privacy/offline value proposition, architecture diagram (`.NET Console App -> Ollama local API -> Qwen2.5-Coder 7B`), prerequisites, Ollama/model setup, how to run the demo, real example output captured in step 10, limitations, and "Why local AI?".
13. **Final polish:** re-read the code for leftovers, confirm the Definition of Done from the brief, then `git init` and a first commit only if you want it.

## Files to be created

`OfflineDotNetCodeReviewer.csproj`, `Program.cs`, `Models/CodeIssue.cs`, `Models/CodeReviewResult.cs`, `Prompts/CodeReviewPrompt.cs`, `Services/ICodeReviewService.cs`, `Services/OllamaCodeReviewService.cs`, `Samples/BadCustomerService.cs`, `README.md`, `.gitignore`

## Verification

- `dotnet build` succeeds with zero warnings.
- `dotnet run -- "Samples/BadCustomerService.cs"` prints the report with a summary and several issues, each with severity, category, explanation and suggested fix.
- Each bad-input case (step 11) prints a clear message and returns a non-zero exit code.
- With Ollama stopped, the app tells the user to start Ollama and verify the model, with no stack trace.
- The README alone is enough for a fresh user with Ollama and the model installed to run the demo.
