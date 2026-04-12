# Clarity OS

A .NET 9 microservices backend for AI-powered task management. Users create and manage tasks through a REST API, then ask an AI to reschedule them. The AI generates proposals that can be accepted or rejected.

## Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [User Secrets Setup](#user-secrets-setup)
- [Running Both Services](#running-both-services)
- [API Documentation (Scalar)](#api-documentation-scalar)
- [API Endpoints](#api-endpoints)
- [LLM Model Selection](#llm-model-selection)
- [Service-to-Service Communication](#service-to-service-communication)
- [Dependency Injection Summary](#dependency-injection-summary)
- [Middleware Pipeline](#middleware-pipeline)
- [Custom Exception Middleware (VG)](#custom-exception-middleware-vg)
- [G (Godkänt) Requirements](#g-godkänt-requirements)
- [VG (Väl Godkänt) Requirements](#vg-väl-godkänt-requirements)
- [How to Trigger Errors (Teacher Demo)](#how-to-trigger-errors-teacher-demo)

## Architecture

| Service | Project | Port | Role |
|---------|---------|------|------|
| ContentApi (Service A) | `ClarityOS.ContentApi` | 5001 | Task CRUD, AI proposals, calls Service B |
| AiProxyApi (Service B) | `ClarityOS.AiProxyApi` | 5002 | Secure LLM gateway (Gemini by default) |

ContentApi stores tasks in an in-memory EF Core database and delegates AI work to AiProxyApi over HTTP. AiProxyApi authenticates requests via an `X-Api-Key` header and forwards prompts to the Gemini API.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A free [Google Gemini API key](https://aistudio.google.com)

## User Secrets Setup

Both projects use .NET User Secrets so no keys are stored in source.

```powershell
# ContentApi secrets
dotnet user-secrets set "LlmProxy:BaseUrl" "http://localhost:5002" --project ClarityOS.ContentApi
dotnet user-secrets set "LlmProxy:ApiKey"  "your-secret-key"       --project ClarityOS.ContentApi

# AiProxyApi secrets
dotnet user-secrets set "AiProxyApi:ApiKey" "your-secret-key"       --project ClarityOS.AiProxyApi
dotnet user-secrets set "Gemini:ApiKey"     "your-gemini-api-key"   --project ClarityOS.AiProxyApi
```

The `AiProxyApi:ApiKey` and `LlmProxy:ApiKey` values must match — ContentApi sends the key, AiProxyApi validates it.

## Running Both Services

### Option 1: Visual Studio (Multiple Startup Projects)

1. Open `ClarityOS.slnx` in Visual Studio.
2. Right-click the solution → **Configure Startup Projects**.
3. Select **Multiple startup projects**, set both projects to **Start**.
4. Press **F5**. Both services launch simultaneously.

### Option 2: CLI (two terminals)

Terminal 1:
```powershell
dotnet run --project ClarityOS.ContentApi
```

Terminal 2:
```powershell
dotnet run --project ClarityOS.AiProxyApi
```

ContentApi listens on `http://localhost:5001` (HTTPS: `https://localhost:7001`).
AiProxyApi listens on `http://localhost:5002` (HTTPS: `https://localhost:7002`).

## API Documentation (Scalar)

Both services expose interactive API docs via Scalar (development mode only):

- ContentApi: `https://localhost:7001/scalar/v1`
- AiProxyApi: `https://localhost:7002/scalar/v1`

XML documentation comments are enabled in both `.csproj` files (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`), so all endpoint descriptions appear in Scalar.

## API Endpoints

### ContentApi — Tasks

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/tasks` | List all tasks. Supports `?category=`, `?startDate=`, `?sort=` query params |
| GET | `/api/tasks/{id}` | Get a single task by ID |
| POST | `/api/tasks` | Create a new task |
| PUT | `/api/tasks/{id}` | Update an existing task |
| DELETE | `/api/tasks/{id}` | Delete a task |
| POST | `/api/tasks/ai-reschedule` | Send pending tasks to the AI for rescheduling proposals |

### ContentApi — Proposals

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/proposals` | List all AI proposals |
| POST | `/api/proposals/{id}/accept` | Accept a proposal (applies changes to the linked task) |
| POST | `/api/proposals/{id}/reject` | Reject a proposal |

### AiProxyApi — LLM Gateway

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/llm/models` | List available LLM models |
| POST | `/api/llm/generate` | Generate AI proposals (requires `X-Api-Key` header) |

## LLM Model Selection

The default LLM provider is Google Gemini with the `gemini-3.1-flash-lite-preview` model. You can override the model per-request by passing a `"model"` field in the generate request body:

```json
{
  "tasks": [...],
  "userPrompt": "reschedule everything to next week",
  "model": "gemini-2.5-flash-preview-05-20"
}
```

Available models can be queried via `GET /api/llm/models`. Currently supported:

- `gemini-3.1-flash-lite-preview` (default)
- `gemini-2.5-flash-preview-05-20`
- `gemini-2.5-pro-preview-05-06`

## Service-to-Service Communication

ContentApi calls AiProxyApi using a typed `HttpClient` (`ILlmProxyClient` → `LlmProxyClient`). The base URL and API key are read from User Secrets. Every request to AiProxyApi includes an `X-Api-Key` header. AiProxyApi validates this key via `ApiKeyMiddleware` before the request reaches any controller.

## Dependency Injection Summary

### ContentApi

| Registration | Lifetime | Purpose |
|-------------|----------|---------|
| `AppDbContext` (InMemory) | Scoped | EF Core data access |
| `ITaskRepository` → `TaskRepository` | Scoped | Task data operations |
| `IProposalRepository` → `ProposalRepository` | Scoped | Proposal data operations |
| `ILlmProxyClient` → `LlmProxyClient` | Typed HttpClient | HTTP calls to AiProxyApi |

### AiProxyApi

| Registration | Lifetime | Purpose |
|-------------|----------|---------|
| `GeminiOptions` | Options pattern | Gemini API configuration |
| `ILlmClient` → `GeminiClient` | Typed HttpClient | HTTP calls to Gemini API |

AiProxyApi is stateless — no database, no repositories.

## Middleware Pipeline

### ContentApi

```
ExceptionMiddleware → HttpsRedirection → Routing → Authorization → MapControllers
```

### AiProxyApi

```
ExceptionMiddleware → ApiKeyMiddleware → HttpsRedirection → Routing → Authorization → MapControllers
```

## Custom Exception Middleware (VG)

`ExceptionMiddleware` is the first middleware in the pipeline. It wraps the entire request in a try/catch and converts unhandled exceptions into RFC 7807 `ProblemDetails` JSON responses (`application/problem+json`). No `app.UseExceptionHandler()` or `app.UseDeveloperExceptionPage()` is used.

### Exception-to-Status-Code Mapping

| Exception | Status Code | Title |
|-----------|-------------|-------|
| `ValidationException` | 400 Bad Request | Validation Error |
| `NotFoundException` | 404 Not Found | Not Found |
| `AiParsingException` | 422 Unprocessable Entity | AI Parsing Error |
| `HttpRequestException` | 502 Bad Gateway | LLM Service Error |
| All others | 500 Internal Server Error | Internal Server Error |

### How to Trigger Each Error

1. **400 Validation Error** — `POST /api/tasks` with a `dueDate` in the past:
   ```json
   { "title": "Test", "category": "school", "dueDate": "2020-01-01T00:00:00" }
   ```

2. **404 Not Found** — `GET /api/tasks/00000000-0000-0000-0000-000000000000` or `POST /api/proposals/00000000-0000-0000-0000-000000000000/accept`

3. **422 AI Parsing Error** — Call `ai-reschedule` when there are no incomplete tasks (delete all tasks or mark them completed). The LLM returns an empty or invalid response that fails parsing.

4. **500 Internal Server Error** — Any unhandled exception not matching the above types.

5. **502 LLM Service Error** — Stop AiProxyApi or the Gemini API is unreachable, then call `POST /api/tasks/ai-reschedule`. The `HttpRequestException` maps to 502.

---

## G (Godkänt) Requirements

1. **RESTful CRUD with correct HTTP verbs and status codes** — `TasksController` implements GET, POST, PUT, DELETE with `200 OK`, `201 Created` (via `CreatedAtAction`), `204 No Content`, and `404 Not Found` (via `NotFoundException` caught by middleware). See `Controllers/TasksController.cs`.
2. **DTO usage (never exposing EF entities)** — Controllers return `TaskResponse` and `ProposalResponse` record DTOs, mapped via private `ToResponse()` helpers. EF entities (`ClarityTask`, `AiProposal`) never appear in API responses. See `DTOs/TaskResponse.cs`, `DTOs/ProposalResponse.cs`.
3. **Query parameter filtering on GET /api/tasks** — The `GetAll` action accepts `?category=`, `?startDate=`, and `?sort=` via `[FromQuery]` parameters and applies LINQ filtering and ordering before returning results. See `Controllers/TasksController.cs`.
4. **IHttpClientFactory typed clients (both services)** — ContentApi registers `AddHttpClient<ILlmProxyClient, LlmProxyClient>` and AiProxyApi registers `AddHttpClient<ILlmClient, GeminiClient>`, both using the typed client pattern backed by `IHttpClientFactory`. See both `Program.cs` files.
5. **Secure API key communication between Service A and Service B** — `LlmProxyClient` reads the key from config (`LlmProxy:ApiKey`) and attaches it as an `X-Api-Key` header. `ApiKeyMiddleware` in AiProxyApi validates the header against `AiProxyApi:ApiKey` and returns `401` if missing or incorrect. See `LlmProxy/LlmProxyClient.cs` and `Middleware/ApiKeyMiddleware.cs`.
6. **DI container configuration (services and repositories)** — `AppDbContext` is registered as Scoped with `UseInMemoryDatabase`, `ITaskRepository` and `IProposalRepository` are registered as Scoped, and HTTP clients use the typed client pattern. See `ContentApi/Program.cs`.
7. **No service locator, no captive dependencies** — All dependencies are injected via primary constructors. `IServiceProvider` is never injected into any controller or service. No `AddSingleton` registrations exist that could capture Scoped services.
8. **Middleware pipeline in correct order** — ContentApi: `ExceptionMiddleware → HttpsRedirection → Routing → Authorization → MapControllers`. AiProxyApi: `ExceptionMiddleware → ApiKeyMiddleware → HttpsRedirection → Routing → Authorization → MapControllers`. See both `Program.cs` files.
9. **Custom ActionFilter (MeasureExecutionTime)** — `MeasureExecutionTimeAttribute` inherits `ActionFilterAttribute`, starts a `Stopwatch` in `OnActionExecuting`, stops and logs elapsed time via `ILogger` in `OnActionExecuted`. Applied as a class-level attribute on `TasksController`. See `Filters/MeasureExecutionTimeAttribute.cs`.
10. **Scalar documentation with XML comments** — Both `.csproj` files enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and suppress warning 1591. Both `Program.cs` files call `AddOpenApi()`, `MapOpenApi()`, and `MapScalarApiReference()`. All public endpoints have `<summary>`, `<param>`, `<returns>`, and `<response>` XML doc comments.

---

## VG (Väl Godkänt) Requirements

1. **Custom ExceptionMiddleware (not UseExceptionHandler)** — A hand-written `ExceptionMiddleware` class is registered as the first middleware in the pipeline. The built-in `app.UseExceptionHandler()` is not used anywhere. See `Middleware/ExceptionMiddleware.cs`.
2. **ILogger injection and exception logging** — The middleware receives `ILogger<ExceptionMiddleware>` via primary constructor injection and calls `logger.LogError(ex, "Unhandled exception: {Message}", ex.Message)` for every caught exception.
3. **RFC 7807 ProblemDetails response structure** — The catch block builds a `ProblemDetails` object with `Status`, `Title`, `Detail`, `Instance`, and `Type` fields, matching the RFC 7807 specification.
4. **Dynamic status codes by exception type** — A C# switch expression maps `ValidationException → 400`, `NotFoundException → 404`, `AiParsingException → 422`, `HttpRequestException → 502`, and all others → `500`. See `Middleware/ExceptionMiddleware.cs`.
5. **Content-Type set to application/problem+json** — The response content type is explicitly set to `"application/problem+json"` before the serialized `ProblemDetails` JSON is written to the response body.

---

## How to Trigger Errors (Teacher Demo)

All examples assume ContentApi is running on `http://localhost:5001`. Use Scalar, curl, or any HTTP client.

### 400 — ValidationException

Create a task with a `dueDate` in the past:

```http
POST http://localhost:5001/api/tasks
Content-Type: application/json

{ "title": "Late task", "category": "school", "dueDate": "2020-01-01T00:00:00" }
```

Expected: `400 Bad Request` with ProblemDetails body containing `"detail": "DueDate cannot be in the past"`.

### 404 — NotFoundException

Request a task or proposal that does not exist:

```http
GET http://localhost:5001/api/tasks/00000000-0000-0000-0000-000000000000
```

or

```http
POST http://localhost:5001/api/proposals/00000000-0000-0000-0000-000000000000/accept
```

Expected: `404 Not Found` with ProblemDetails body.

### 422 — AiParsingException

Trigger this by calling `ai-reschedule` when there are no incomplete tasks (either delete all tasks or mark them completed first). The LLM will return an empty or invalid response that fails parsing.

> **Note:** In a production scenario this should return a user-friendly message like "No tasks to reschedule" instead of a 422. The current behavior is kept intentionally to demonstrate the `AiParsingException → 422` middleware mapping for the VG requirement.

```http
POST http://localhost:5001/api/tasks/ai-reschedule
Content-Type: application/json

{ "userPrompt": "reschedule everything" }
```

Expected: `422 Unprocessable Entity` with `"detail": "The AI agent returned an invalid format"`.

### 401 — ApiKeyMiddleware (AiProxyApi)

Call AiProxyApi directly without the `X-Api-Key` header:

```http
POST http://localhost:5002/api/llm/generate
Content-Type: application/json

{ "tasks": [], "userPrompt": "test" }
```

Expected: `401 Unauthorized` with body `"Unauthorized: invalid or missing X-Api-Key header."`.

### 502 — HttpRequestException

Stop AiProxyApi (Service B), then call the reschedule endpoint:

```http
POST http://localhost:5001/api/tasks/ai-reschedule
Content-Type: application/json

{ "userPrompt": "reschedule everything" }
```

Expected: `502 Bad Gateway` with `"title": "LLM Service Error"`.
