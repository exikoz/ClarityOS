# ClarityOS

A .NET 9 microservices backend for AI-powered task management. Users create tasks through a REST API, then ask an AI to reschedule them. The AI generates proposals that can be accepted or rejected.

## Architecture

| Service | Project | Port | Role |
|---------|---------|------|------|
| ContentApi | `ClarityOS.ContentApi` | 5001 | Task CRUD, AI proposals, calls AiProxyApi |
| AiProxyApi | `ClarityOS.AiProxyApi` | 5002 | Secure LLM gateway (Google Gemini) |

ContentApi stores tasks in an in-memory EF Core database and delegates AI work to AiProxyApi over HTTP. AiProxyApi authenticates requests via `X-Api-Key` and forwards prompts to the Gemini API.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A [Google Gemini API key](https://aistudio.google.com)

## API Key Setup (Local Development)

Both projects use .NET User Secrets. No keys are stored in source control.

```powershell
# ContentApi
dotnet user-secrets set "LlmProxy:BaseUrl" "http://localhost:5002" --project ClarityOS.ContentApi
dotnet user-secrets set "LlmProxy:ApiKey"  "your-shared-secret"    --project ClarityOS.ContentApi

# AiProxyApi
dotnet user-secrets set "AiProxyApi:ApiKey" "your-shared-secret"    --project ClarityOS.AiProxyApi
dotnet user-secrets set "Gemini:ApiKey"     "your-gemini-api-key"   --project ClarityOS.AiProxyApi
```

The `AiProxyApi:ApiKey` and `LlmProxy:ApiKey` values must match. ContentApi sends the key, AiProxyApi validates it.

## API Key Setup (Production)

In production, set environment variables using the double-underscore convention:

```bash
# ContentApi
LlmProxy__BaseUrl=https://your-aiproxy-host
LlmProxy__ApiKey=your-shared-secret

# AiProxyApi
AiProxyApi__ApiKey=your-shared-secret
Gemini__ApiKey=your-gemini-api-key
```

These can be set via your hosting platform's secrets manager (Azure Key Vault, AWS Secrets Manager, Docker secrets, etc.).

## Security Guarantees

- No API keys in `appsettings.json` or any committed file
- `.gitignore` excludes `.env` files
- Logging never outputs API keys or authorization headers
- ProblemDetails error responses never expose internal credentials
- User Secrets are stored outside the project directory by .NET

## Running the Services

### Visual Studio (recommended)

1. Open `ClarityOS.slnx`
2. Right-click solution > Configure Startup Projects > Multiple startup projects > Start both
3. Press F5

### CLI (two terminals)

```powershell
dotnet run --project ClarityOS.ContentApi
```

```powershell
dotnet run --project ClarityOS.AiProxyApi
```

ContentApi: `http://localhost:5001`  
AiProxyApi: `http://localhost:5002`

## API Documentation

Both services expose Scalar API docs in development:

- ContentApi: `http://localhost:5001/scalar/v1`
- AiProxyApi: `http://localhost:5002/scalar/v1`

## API Endpoints

### Tasks

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/tasks` | List tasks. Supports `?category=`, `?startDate=`, `?sort=` |
| GET | `/api/tasks/{id}` | Get task by ID |
| POST | `/api/tasks` | Create task |
| PUT | `/api/tasks/{id}` | Update task |
| DELETE | `/api/tasks/{id}` | Delete task |
| POST | `/api/tasks/ai-reschedule` | Generate AI rescheduling proposals |

### Proposals

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/proposals` | List all proposals |
| POST | `/api/proposals/{id}/accept` | Accept proposal (applies to task) |
| POST | `/api/proposals/{id}/reject` | Reject proposal |

### LLM Gateway (AiProxyApi)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/llm/models` | List available models |
| POST | `/api/llm/generate` | Generate AI response (requires `X-Api-Key`) |

## Error Handling

Both services use a custom `ExceptionMiddleware` that catches all unhandled exceptions and returns RFC 7807 ProblemDetails responses (`application/problem+json`).

### ContentApi Error Mapping

| Exception | HTTP Status | Title |
|-----------|-------------|-------|
| `ValidationException` | 400 | Validation Error |
| `NotFoundException` | 404 | Not Found |
| `AiParsingException` | 422 | AI Parsing Error |
| `TaskCanceledException` / `TimeoutException` | 504 | Gateway Timeout |
| `HttpRequestException` (429) | 429 | Rate Limit Exceeded |
| `HttpRequestException` (401/403) | 502 | Upstream Authentication Failed |
| `HttpRequestException` (5xx) | 502 | LLM Service Error |
| All others | 500 | Internal Server Error |

### AiProxyApi Error Mapping

| Exception | HTTP Status | Title |
|-----------|-------------|-------|
| `RateLimitException` | 429 | Rate Limit Exceeded |
| `ExternalAuthException` | 502 | Upstream Authentication Failed |
| `ExternalServiceException` | 502 | LLM Service Error |
| `TimeoutException` / `TaskCanceledException` | 504 | Gateway Timeout |
| `HttpRequestException` | 502 | LLM Service Error |
| `InvalidOperationException` | 400 | Configuration Error |
| All others | 500 | Internal Server Error |

No error response ever exposes API keys, internal paths, or stack traces.

## Prompt Engineering

The system prompt in `LlmController` uses these techniques:

1. **Explicit rules**: Numbered constraints the model must follow (exact taskIds, future dates, field length limits)
2. **Uncertainty handling**: "If unsure, state uncertainty in the description"
3. **Few-shot example**: One complete input/output example to anchor format expectations
4. **Output format specification**: Exact JSON schema with field names
5. **Negative constraints**: "Do NOT invent tasks", "Do NOT include markdown"

## AI Evaluation

See [evaluation.md](evaluation.md) for:
- Quality criteria used to assess AI output
- Known limitations (prompt sensitivity, hallucination risk, date reasoning)
- 3 test prompts with documented results
- What was changed in the prompt and why
- Conclusion on when the AI service is/is not appropriate

## LLM Model Selection

Default model: `gemini-3.1-flash-lite-preview`. Override per-request:

```json
{
  "tasks": [...],
  "userPrompt": "reschedule to next week",
  "model": "gemini-2.5-flash-preview-05-20"
}
```

Available: `gemini-3.1-flash-lite-preview`, `gemini-2.5-flash-preview-05-20`, `gemini-2.5-pro-preview-05-06`

## Timeouts

Both HttpClients are configured with a 30-second timeout. If the Gemini API or AiProxyApi does not respond within that window, the request fails with a 504 Gateway Timeout ProblemDetails response.
