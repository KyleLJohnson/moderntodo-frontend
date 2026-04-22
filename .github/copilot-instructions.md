# BlazorTodo Frontend — Project Guidelines

Blazor WebAssembly (.NET 10) frontend for the ModernTodo app, deployed as an Azure Static Web App. Communicates with the BlazorTodo API (Azure Functions backend) via REST.

## Architecture

```
Pages/              Routable pages (@page directive)
Components/         Reusable Razor components (TaskCard, TaskForm)
Models/             DTOs with validation annotations (TaskDto)
Services/
  TaskApiService    Typed HttpClient wrapper for all API calls
Program.cs          WebAssemblyHostBuilder setup and DI registration
wwwroot/
  appsettings.json  Runtime config (ApiBaseUrl)
  staticwebapp.config.json  SWA routing rules and security headers
```

API base URL is read from `appsettings.json` (`ApiBaseUrl`), falling back to the host origin. In production (SWA + linked Functions), `/api/*` requests proxy to the backend automatically via `staticwebapp.config.json`.

## Build & Run

```bash
# From moderntodo-frontend/
dotnet build src/BlazorTodo/BlazorTodo.csproj

# Local dev with hot reload
dotnet watch --project src/BlazorTodo/BlazorTodo.csproj

# Deploy
azd deploy
```

For local dev against the local Functions API, set `ApiBaseUrl` in `wwwroot/appsettings.Development.json`:
```json
{ "ApiBaseUrl": "http://localhost:7071" }
```

## Component Conventions

```razor
@* Reusable component example *@
<TaskCard Task="task" OnEdit="HandleEdit" OnDelete="HandleDelete" />

@code {
    [Parameter] public TaskDto Task { get; set; } = default!;
    [Parameter] public EventCallback<TaskDto> OnEdit { get; set; }
    [Parameter] public EventCallback<int> OnDelete { get; set; }
}
```

- Use `[Parameter]` for public inputs, `EventCallback<T>` for child→parent events
- Forms use `EditForm` + `DataAnnotationsValidator` + `ValidationMessage`; bind via `@bind-Value`
- Input components: `InputText`, `InputSelect`, `InputDate`, `InputTextArea`
- State updates always call `StateHasChanged()` after async callbacks if Blazor can't detect the change automatically

## Data Model

```
TaskDto (frontend DTO — mirrors the backend TodoTask)
├─ Id (int)
├─ [Required] [MaxLength(200)] Title (string)
├─ Description (string?)
├─ DueDate (DateTime?)
├─ Priority (enum: Low=0 Medium=1 High=2)
├─ IsCompleted (bool)
└─ CreatedAt (DateTime, UTC)
```

`TaskDto` lives in `Models/` and is the only type used across components and services — do not use the backend's `TodoTask` directly.

## API Service Conventions

All API calls go through `TaskApiService`:
- Returns `null` or empty list on failure (no exceptions bubble to components)
- Uses `System.Net.Http.Json` helpers (`GetFromJsonAsync`, `PostAsJsonAsync`, etc.)
- JSON serialization is camelCase (matches the backend's `JsonSerializerOptions`)

## Key Pitfalls

- **SWA routing**: `/api/*` routes are proxied to the linked Functions backend, not rewritten — do not add `api/` prefix handling in `staticwebapp.config.json`.
- **No authentication**: All endpoints are anonymous. Do not add auth headers without also updating the backend `AuthorizationLevel` and Bicep CORS config.
- **Priority validation**: Only enforced via the dropdown in `TaskForm`; the backend accepts any int. Keep the enum values (`Low=0`, `Medium=1`, `High=2`) in sync with the server model.
- **DateTime display**: `DueDate` comes from the server as UTC — apply `.ToLocalTime()` or format explicitly before displaying to avoid confusing users.

## Infrastructure

Bicep provisions: Static Web App (Standard tier — required for linked backend). Backend link is optional (`functionAppResourceId` parameter).

SPA fallback (`/index.html`) handles Blazor client-side routing. Security headers (CSP, X-Frame-Options, Referrer-Policy) are set globally in `staticwebapp.config.json`.
