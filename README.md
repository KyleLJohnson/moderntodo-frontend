# BlazorTodo Frontend

Blazor WebAssembly frontend for the ModernTodo application. Provides a single-page task management UI that communicates with the BlazorTodo API backend.

## Technologies

| Technology | Version | Purpose |
|---|---|---|
| C# / .NET | 10.0 | Language and runtime |
| Blazor WebAssembly | 10.0.4 | SPA framework (runs in-browser via WebAssembly) |
| Bootstrap | 5 | UI layout and base styles |
| Azure Static Web Apps | — | Hosting and API proxy |
| Bicep | — | Infrastructure as Code (Azure provisioning) |

## What it does

A single-page application for managing a personal task list. Features include:

- **View tasks** — filterable list showing all, active, or completed tasks with counts
- **Create tasks** — slide-in form with title, description, due date, and priority (Low / Medium / High)
- **Edit tasks** — reuses the same form component pre-populated with existing values
- **Complete / uncomplete tasks** — toggle completion state directly from the task card
- **Delete tasks** — remove tasks with a single action

All data is fetched from and persisted to the BlazorTodo API over REST. The `TaskApiService` typed `HttpClient` wrapper handles all API calls, returning `null` or an empty list on failure so errors don't propagate to components.

### Routing and API proxy

In production (Azure Static Web Apps with a linked Functions backend), `/api/*` requests are proxied automatically to the backend — no manual URL configuration needed. For local development, set `ApiBaseUrl` in `wwwroot/appsettings.Development.json` to point at the locally running Functions host.

## Project structure

```
Pages/
  Home.razor        Main task list page with filter bar and task list
Components/
  TaskCard.razor    Individual task display with edit/delete/toggle actions
  TaskForm.razor    Slide-in create/edit form with validation
Models/
  TaskDto.cs        Frontend DTO mirroring the backend model (with validation annotations)
Services/
  TaskApiService    Typed HttpClient wrapper for all API calls
Layout/             App shell, navigation, and layout CSS
wwwroot/
  appsettings.json              Runtime config (ApiBaseUrl)
  staticwebapp.config.json      SWA routing rules and security headers (CSP, X-Frame-Options)
infra/              Bicep templates for Azure provisioning
```

## Data model

```
TaskDto
├─ Id          int          Set by the API
├─ Title       string       Required, max 200 characters
├─ Description string?      Optional
├─ DueDate     DateTime?    UTC — apply .ToLocalTime() before display
├─ Priority    enum         Low = 0 | Medium = 1 | High = 2
├─ IsCompleted bool
└─ CreatedAt   DateTime     UTC, set by the API
```

## Build & run

```bash
# Build
dotnet build src/BlazorTodo/BlazorTodo.csproj

# Run locally with hot reload
dotnet watch --project src/BlazorTodo/BlazorTodo.csproj

# Deploy to Azure
azd deploy
```

### Local configuration

Create `src/BlazorTodo/wwwroot/appsettings.Development.json` to point at the local API:

```json
{
  "ApiBaseUrl": "http://localhost:7071"
}
```

## Infrastructure

Bicep (`infra/`) provisions:

- **Azure Static Web App** — Standard tier (required for linked backend support)
- Optional backend link via `functionAppResourceId` parameter
