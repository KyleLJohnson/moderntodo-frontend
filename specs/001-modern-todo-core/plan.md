# Technical Implementation Plan — Modern To Do 9 (Core Feature Set)

> **Feature:** 001-modern-todo-core  
> **Status:** DRAFT  
> **Last updated:** 2026-04-01  
> **Confidence:** HIGH (88%) — existing codebase is well-structured; gaps are clearly identified

---

## Overview

Modern To Do 9 is a Blazor WebAssembly SPA backed by Azure Functions (isolated worker) with SQLite persisted to Azure Blob Storage. The existing codebase already ships a working task CRUD experience. This plan formalises what must be built to reach a production-ready, fully tested "v1" of the application, as described by the project issue.

---

## Architecture at a Glance

```
[User Browser]
      │
      ▼
[Blazor WASM SPA — Azure Static Web Apps]
      │  (same-origin /api/* routing)
      ▼
[Azure Functions v4 — Isolated Worker]
      │
      ├── TaskFunctions   (CRUD + filter)
      ├── AuthFunctions   (register, login, refresh)  ← NEW
      └── CategoryFunctions (CRUD)                    ← NEW
      │
      ▼
[SQLite via Azure Blob Storage]
      Tables: Tasks, Users, RefreshTokens, Categories
```

---

## Repositories Involved

| Repository | Role | Primary Language |
|---|---|---|
| `KyleLJohnson/moderntodo-frontend` | Blazor WebAssembly SPA | C# / Razor |
| `KyleLJohnson/moderntodo-api` | Azure Functions REST API | C# |

---

## Current State (Brownfield Inventory)

### Frontend (`moderntodo-frontend`)
| File / Component | Status | Notes |
|---|---|---|
| `Pages/Home.razor` | ✅ Working | Task list with CRUD, filter tabs |
| `Components/TaskCard.razor` | ✅ Working | Edit, delete, toggle complete |
| `Components/TaskForm.razor` | ✅ Working | Create/edit drawer |
| `Services/TaskApiService.cs` | ✅ Working | HTTP client wrapper for API |
| `Models/TaskDto.cs` | ✅ Working | `Id, Title, Description, DueDate, Priority, IsCompleted, CreatedAt` |
| `Program.cs` | ✅ Working | DI setup, HTTP client scoped to API base URL |
| Authentication UI | ❌ Missing | Login, Register, Logout pages |
| Search | ❌ Missing | No global search bar |
| Categories UI | ❌ Missing | No category management |
| Unit Tests | ❌ Missing | No test project in frontend repo |

### Backend (`moderntodo-api`)
| File / Component | Status | Notes |
|---|---|---|
| `Functions/TaskFunctions.cs` | ✅ Working | GET, POST, PUT, DELETE `/api/tasks` |
| `Services/TaskRepository.cs` | ✅ Working | GetAll, GetById, Create, Update, Delete |
| `Services/DbService.cs` | ✅ Working | SQLite init + blob persistence |
| `Models/TodoTask.cs` | ✅ Working | Matches frontend DTO |
| Authentication endpoints | ❌ Missing | No register/login/JWT issuance |
| User model + table | ❌ Missing | Single-user today |
| Categories endpoints | ❌ Missing | No category concept |
| Input validation middleware | ⚠️ Partial | Title null-check only |
| Unit / Integration Tests | ❌ Missing | No test project |
| CORS configuration | ⚠️ Partial | SWA handles same-origin; local dev needs config |

---

## Implementation Task List

### Phase 1 — Foundation & Testing Infrastructure

- [ ] **T001** [P] Create a test project `tests/BlazorTodo.Api.Tests` (xUnit + Moq) in `moderntodo-api`
  - Add `xunit`, `Moq`, `FluentAssertions` NuGet packages
  - Configure `<IsTestProject>true</IsTestProject>` in `.csproj`
  - Add project reference to `BlazorTodo.Api`

- [ ] **T002** [P] Create a test project `tests/BlazorTodo.Tests` (bUnit + xUnit) in `moderntodo-frontend`
  - Add `bunit`, `xunit`, `Moq` NuGet packages
  - Add project reference to `BlazorTodo`

- [ ] **T003** Update `context/tech-stack.md` with the confirmed, concrete tech stack:
  - Languages: C# 13 / .NET 10
  - Frontend: Blazor WebAssembly 10.0.x, Microsoft.AspNetCore.Components.WebAssembly
  - Backend: Azure Functions v4 Isolated, Dapper, Microsoft.Data.Sqlite, Azure.Storage.Blobs
  - Auth: JWT Bearer tokens (System.IdentityModel.Tokens.Jwt)
  - Testing: xUnit 2.x, Moq 4.x, FluentAssertions 6.x, bUnit 1.x
  - Infrastructure: Azure Static Web Apps, Azure Functions, Azure Blob Storage, Application Insights

- [ ] **T004** Update `context/architecture.md` with full system diagram, components table, data flow, and ADRs for:
  - ADR-001: SQLite in Blob Storage (accepted, scaling trade-off documented)
  - ADR-002: JWT auth in Azure Functions (preferred over Azure AD B2C for simplicity)
  - ADR-003: Blazor WASM over Blazor Server (offline capability, SWA compatibility)

---

### Phase 2 — Database Schema Expansion (Backend)

- [ ] **T005** Update `DbService.CreateSchemaAsync()` to add new tables alongside the existing `Tasks` table:

  ```sql
  -- Users table
  CREATE TABLE IF NOT EXISTS Users (
      Id           INTEGER PRIMARY KEY AUTOINCREMENT,
      Email        TEXT NOT NULL UNIQUE,
      PasswordHash TEXT NOT NULL,
      CreatedAt    TEXT NOT NULL
  );

  -- RefreshTokens table
  CREATE TABLE IF NOT EXISTS RefreshTokens (
      Id         INTEGER PRIMARY KEY AUTOINCREMENT,
      UserId     INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
      Token      TEXT NOT NULL UNIQUE,
      ExpiresAt  TEXT NOT NULL,
      CreatedAt  TEXT NOT NULL
  );

  -- Categories table
  CREATE TABLE IF NOT EXISTS Categories (
      Id      INTEGER PRIMARY KEY AUTOINCREMENT,
      UserId  INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
      Name    TEXT NOT NULL,
      Color   TEXT NOT NULL DEFAULT '#6366f1'
  );

  -- Add UserId and CategoryId columns to Tasks (migration)
  ALTER TABLE Tasks ADD COLUMN UserId     INTEGER REFERENCES Users(Id) ON DELETE CASCADE;
  ALTER TABLE Tasks ADD COLUMN CategoryId INTEGER REFERENCES Categories(Id) ON DELETE SET NULL;
  ```

  > **Note:** Use `ALTER TABLE … ADD COLUMN` wrapped in try/catch to be idempotent on existing databases.

- [ ] **T006** Write unit tests for `DbService` schema creation (T001 prerequisite):
  - Test schema is created successfully on a fresh in-memory SQLite db
  - Test migration columns are added idempotently

---

### Phase 3 — Authentication Backend (moderntodo-api)

- [ ] **T007** Add NuGet packages to `BlazorTodo.Api.csproj`:
  - `System.IdentityModel.Tokens.Jwt` (latest stable)
  - `Microsoft.AspNetCore.Cryptography.KeyDerivation` (for PBKDF2 password hashing)

- [ ] **T008** Create `Models/User.cs`:
  ```csharp
  namespace BlazorTodo.Api.Models;
  public class User
  {
      public int Id { get; set; }
      public string Email { get; set; } = string.Empty;
      public string PasswordHash { get; set; } = string.Empty;
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
  ```

- [ ] **T009** Create `Models/AuthDto.cs` (request/response DTOs):
  ```csharp
  namespace BlazorTodo.Api.Models;
  public record RegisterRequest(string Email, string Password);
  public record LoginRequest(string Email, string Password);
  public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);
  public record RefreshRequest(string RefreshToken);
  ```

- [ ] **T010** Create `Services/PasswordService.cs` (PBKDF2):
  - `string Hash(string plaintext)` — returns Base64-encoded hash with salt
  - `bool Verify(string plaintext, string hash)` — constant-time comparison
  - No hardcoded secrets; salt embedded in hash output

- [ ] **T011** Create `Services/JwtService.cs`:
  - Read `JWT_SECRET` from `IConfiguration` (environment variable — never hardcoded)
  - Read `JWT_ISSUER` and `JWT_AUDIENCE` from configuration
  - `string GenerateAccessToken(User user)` — 15-minute expiry, `userId` + `email` claims
  - `string GenerateRefreshToken()` — cryptographically random 64-byte Base64 string
  - `ClaimsPrincipal? ValidateToken(string token)` — used by protected endpoints

- [ ] **T012** Create `Services/UserRepository.cs`:
  - `Task<User?> GetByEmailAsync(string email)`
  - `Task<User> CreateAsync(string email, string passwordHash)`
  - `Task<string> CreateRefreshTokenAsync(int userId, string token, DateTime expiresAt)`
  - `Task<(int userId, bool valid)> ValidateRefreshTokenAsync(string token)`
  - `Task RevokeRefreshTokenAsync(string token)`

- [ ] **T013** Create `Functions/AuthFunctions.cs`:
  - `POST /api/auth/register` — validate email format + password strength, hash password, create user, return `AuthResponse`
  - `POST /api/auth/login` — verify credentials, issue JWT + refresh token, return `AuthResponse`
  - `POST /api/auth/refresh` — validate refresh token, issue new JWT + refresh token (rotation), return `AuthResponse`
  - `POST /api/auth/logout` — revoke refresh token (requires valid JWT)
  - Return `400 Bad Request` for invalid input; `401 Unauthorized` for bad credentials; never expose stack traces

- [ ] **T014** Create `Middleware/AuthMiddleware.cs` (helper extension):
  - `HttpRequestData.GetUserId()` — extracts `userId` claim from `Authorization: Bearer <token>` header
  - Returns `null` if token is missing, expired, or invalid
  - Used by `TaskFunctions` and future protected endpoints

- [ ] **T015** Update `TaskFunctions.cs` to enforce authentication:
  - All task endpoints call `req.GetUserId()` and return `401` if null
  - Filter all queries by `UserId` — users can only see/modify their own tasks
  - Pass `userId` to `TaskRepository` methods

- [ ] **T016** Update `TaskRepository.cs` to scope all queries by `userId`:
  - `GetAllAsync(int userId, bool? completed)` — add `WHERE UserId = @UserId`
  - `GetByIdAsync(int userId, int id)` — add ownership check
  - `CreateAsync(int userId, TodoTask task)` — set `task.UserId`
  - `UpdateAsync(int userId, int id, TodoTask updated)` — add `AND UserId = @UserId`
  - `DeleteAsync(int userId, int id)` — add `AND UserId = @UserId`

- [ ] **T017** Write unit tests for `PasswordService` (T001 prerequisite):
  - Hash produces a non-plaintext output
  - Verify returns true for correct password
  - Verify returns false for wrong password
  - Different calls to Hash produce different salts

- [ ] **T018** Write unit tests for `JwtService` (T001 prerequisite):
  - Generated token can be validated
  - Expired token returns null from ValidateToken
  - Token contains expected userId and email claims

- [ ] **T019** Write unit tests for `AuthFunctions` (T001 prerequisite):
  - Register with valid payload returns 201 + token
  - Register with duplicate email returns 409
  - Login with correct credentials returns 200 + token
  - Login with wrong password returns 401
  - Refresh with valid token returns new tokens
  - Refresh with invalid token returns 401

---

### Phase 4 — Categories Backend (moderntodo-api)

- [ ] **T020** Create `Models/Category.cs`:
  ```csharp
  namespace BlazorTodo.Api.Models;
  public class Category
  {
      public int Id { get; set; }
      public int UserId { get; set; }
      public string Name { get; set; } = string.Empty;
      public string Color { get; set; } = "#6366f1";
  }
  ```

- [ ] **T021** Create `Services/CategoryRepository.cs`:
  - `Task<IEnumerable<Category>> GetAllAsync(int userId)`
  - `Task<Category?> GetByIdAsync(int userId, int id)`
  - `Task<Category> CreateAsync(int userId, Category category)`
  - `Task<Category?> UpdateAsync(int userId, int id, Category updated)`
  - `Task<bool> DeleteAsync(int userId, int id)`

- [ ] **T022** Create `Functions/CategoryFunctions.cs`:
  - `GET /api/categories` — list user's categories (requires JWT)
  - `POST /api/categories` — create category (requires JWT)
  - `PUT /api/categories/{id}` — update category (requires JWT + ownership)
  - `DELETE /api/categories/{id}` — delete category (requires JWT + ownership)

- [ ] **T023** Update `TodoTask.cs` model to include `CategoryId`:
  ```csharp
  public int? CategoryId { get; set; }
  ```

- [ ] **T024** Update `TaskFunctions.cs` to accept optional `categoryId` filter:
  - `GET /api/tasks?categoryId=3` — filter tasks by category

- [ ] **T025** Write unit tests for `CategoryRepository` (T001 prerequisite):
  - GetAll returns only user's categories
  - Create persists a new category
  - Delete removes and returns true; returns false for non-existent ID

---

### Phase 5 — Authentication Frontend (moderntodo-frontend)

- [ ] **T026** Add NuGet package `Microsoft.AspNetCore.Components.Authorization` to `BlazorTodo.csproj`

- [ ] **T027** Create `Models/AuthModels.cs`:
  ```csharp
  namespace BlazorTodo.Models;
  public record LoginRequest(string Email, string Password);
  public record RegisterRequest(string Email, string Password);
  public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);
  ```

- [ ] **T028** Create `Services/AuthService.cs`:
  - `Task<bool> LoginAsync(string email, string password)` — calls `POST /api/auth/login`, stores tokens in `localStorage`
  - `Task<bool> RegisterAsync(string email, string password)` — calls `POST /api/auth/register`
  - `Task LogoutAsync()` — calls `POST /api/auth/logout`, clears localStorage
  - `Task<string?> GetValidAccessTokenAsync()` — returns stored token or refreshes if expired
  - `bool IsAuthenticated` — checks token existence/expiry
  - `string? UserEmail` — decoded from JWT claims

- [ ] **T029** Create `Services/CustomAuthStateProvider.cs` (extends `AuthenticationStateProvider`):
  - `GetAuthenticationStateAsync()` — reads JWT from localStorage, builds `ClaimsPrincipal`
  - Called by `AuthorizeView` and route guards

- [ ] **T030** Update `Program.cs` to register auth services:
  ```csharp
  builder.Services.AddAuthorizationCore();
  builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
  builder.Services.AddScoped<AuthService>();
  ```

- [ ] **T031** Update `TaskApiService.cs` to attach Bearer token to all requests:
  - Inject `AuthService`
  - Before each HTTP call, call `GetValidAccessTokenAsync()` and set `Authorization` header
  - If token is null, throw `UnauthorizedException` (handled by UI to redirect to login)

- [ ] **T032** Create `Pages/Login.razor` (`/login`):
  - Email + password fields
  - Submit calls `AuthService.LoginAsync()`
  - On success: navigate to `/`
  - On failure: inline error message (never expose raw API errors to user)
  - Link to `/register`

- [ ] **T033** Create `Pages/Register.razor` (`/register`):
  - Email + password + confirm-password fields
  - Client-side: confirm-password match validation
  - Submit calls `AuthService.RegisterAsync()`
  - On success: navigate to `/login` with success banner
  - On failure: inline error message
  - Link to `/login`

- [ ] **T034** Update `App.razor` to wrap routes with `<CascadingAuthenticationState>` and add route guard:
  - Unauthenticated users accessing `/` are redirected to `/login`
  - Use `<AuthorizeRouteView>` with a redirect `NotAuthorized` template

- [ ] **T035** Add `Logout` button to `Layout/MainLayout.razor` (or equivalent nav):
  - Calls `AuthService.LogoutAsync()` and navigates to `/login`

- [ ] **T036** Update `TaskDto.cs` to include optional `CategoryId`:
  ```csharp
  public int? CategoryId { get; set; }
  ```

- [ ] **T037** Write bUnit tests for `Login.razor` (T002 prerequisite):
  - Renders email/password fields
  - Submit with valid credentials triggers `AuthService.LoginAsync`
  - Error message renders on failed login

- [ ] **T038** Write bUnit tests for `Register.razor` (T002 prerequisite):
  - Confirm-password mismatch shows validation error before API call
  - Submit with valid data triggers `AuthService.RegisterAsync`

---

### Phase 6 — Categories Frontend (moderntodo-frontend)

- [ ] **T039** Update `Models/TaskDto.cs` to add `CategoryId` (covered by T036)

- [ ] **T040** Create `Models/CategoryDto.cs`:
  ```csharp
  namespace BlazorTodo.Models;
  public class CategoryDto
  {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public string Color { get; set; } = "#6366f1";
  }
  ```

- [ ] **T041** Create `Services/CategoryApiService.cs`:
  - `Task<List<CategoryDto>?> GetCategoriesAsync()`
  - `Task<CategoryDto?> CreateCategoryAsync(CategoryDto category)`
  - `Task<CategoryDto?> UpdateCategoryAsync(int id, CategoryDto category)`
  - `Task DeleteCategoryAsync(int id)`
  - Attaches Bearer token via `AuthService` (same pattern as T031)

- [ ] **T042** Register `CategoryApiService` in `Program.cs`

- [ ] **T043** Update `Home.razor` to:
  - Load categories from `CategoryApiService.GetCategoriesAsync()` on init
  - Add a category filter tab (alongside All/Active/Completed/High)
  - Pass selected `CategoryId` filter to `Api.GetTasksAsync(categoryId: ...)`
  - Pass loaded categories to `TaskForm` for the category selector

- [ ] **T044** Update `Components/TaskForm.razor` to include a `Category` field:
  - `InputSelect` bound to `_model.CategoryId`
  - Populated from a `[Parameter] public List<CategoryDto> Categories { get; set; }` parameter
  - Shows "(None)" as the default/unset option

- [ ] **T045** Update `Components/TaskCard.razor` to display category badge:
  - Show colored chip with category name if `Task.CategoryId` is set
  - Pass `[Parameter] public CategoryDto? Category { get; set; }` from parent

- [ ] **T046** Create `Pages/Categories.razor` (`/categories`):
  - List all user categories with name and color swatch
  - Inline add: name + color picker → create
  - Delete button (with confirmation) per category
  - Edit inline

---

### Phase 7 — Search Feature (moderntodo-frontend)

- [ ] **T047** Add search input to `Home.razor` above the filter bar:
  - Debounced (300ms) client-side filter on `_tasks` by `Title` and `Description`
  - Clears when user empties the field
  - Works in conjunction with active filter tab (AND logic)

- [ ] **T048** Update `FilteredTasks` computed property in `Home.razor` to apply search term:
  ```csharp
  private List<TaskDto> FilteredTasks => _tasks
      .Where(t => string.IsNullOrEmpty(_search) ||
                  t.Title.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                  (t.Description?.Contains(_search, StringComparison.OrdinalIgnoreCase) ?? false))
      .Where(t => _activeFilter switch {
          "active"    => !t.IsCompleted,
          "completed" => t.IsCompleted,
          "high"      => t.Priority == Priority.High,
          _           => true
      })
      .ToList();
  ```

---

### Phase 8 — UI Enhancements

- [ ] **T049** Display `CreatedAt` timestamp on `TaskCard.razor`:
  - Show relative time (e.g., "2 hours ago", "yesterday") below the due date
  - Use a `GetRelativeTime(DateTime dt)` helper in a static utility class `Helpers/DateHelper.cs`

- [ ] **T050** Add keyboard shortcut `N` to open "New Task" form from `Home.razor`:
  - Use `@onkeydown` on `body` or `document` via JS interop
  - Only triggers when no input is focused

- [ ] **T051** Add sort options to `Home.razor`:
  - Sort by: Created Date (default), Due Date, Priority
  - Sort direction: Ascending / Descending
  - Persisted in component state (not URL)

- [ ] **T052** Improve empty states:
  - Different messages for "no tasks at all" vs "no tasks matching filter" vs "no tasks matching search"

- [ ] **T053** Add a "Clear completed" bulk action button (visible only when completed tasks exist):
  - Calls `DeleteTask` for each completed task sequentially
  - Updates UI optimistically

---

### Phase 9 — CI/CD & Quality Gates

- [ ] **T054** Create `.github/workflows/frontend-ci.yml`:
  ```yaml
  name: Frontend CI
  on:
    push:
      branches: [main]
    pull_request:
      branches: [main]
  jobs:
    build-and-test:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.x'
        - run: dotnet restore BlazorTodo.slnx
        - run: dotnet build BlazorTodo.slnx --no-restore -c Release
        - run: dotnet test BlazorTodo.slnx --no-build -c Release --collect:"XPlat Code Coverage"
        - name: Check coverage threshold
          run: |
            # Fail if coverage < 80%
            dotnet tool install -g dotnet-reportgenerator-globaltool
            reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:TextSummary
            grep "Line coverage" coverage-report/Summary.txt | awk '{if ($NF+0 < 80) exit 1}'
  ```

- [ ] **T055** Create `.github/workflows/api-ci.yml` (in `moderntodo-api` repo):
  ```yaml
  name: API CI
  on:
    push:
      branches: [main]
    pull_request:
      branches: [main]
  jobs:
    build-and-test:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '10.x'
        - run: dotnet restore BlazorTodo.Api.slnx
        - run: dotnet build BlazorTodo.Api.slnx --no-restore -c Release
        - run: dotnet test BlazorTodo.Api.slnx --no-build -c Release --collect:"XPlat Code Coverage"
  ```

- [ ] **T056** Add `.github/workflows/codeql.yml` (security scanning) to both repos:
  - Use `actions/codeql-action` with `csharp` language
  - Run on push to main and on PRs

---

### Phase 10 — Security Hardening

- [ ] **T057** Add input validation to `AuthFunctions.cs`:
  - Email: regex format validation (e.g. must contain `@` and a TLD)
  - Password: minimum 8 characters, at least 1 uppercase, 1 number
  - Return `400` with human-readable errors; do not expose internals

- [ ] **T058** Rate-limit auth endpoints (register, login):
  - Use an in-memory `ConcurrentDictionary<string, (int Count, DateTime Window)>` keyed by client IP
  - Max 5 login attempts per minute per IP; return `429 Too Many Requests` with `Retry-After` header

- [ ] **T059** Add `Content-Security-Policy` nonce support and review existing CSP in `staticwebapp.config.json`:
  - Current `unsafe-inline` / `unsafe-eval` should be replaced with a nonce-based policy for scripts
  - Review and tighten to minimum required directives

- [ ] **T060** Ensure `JWT_SECRET` is at least 256 bits (32 bytes):
  - Add a startup check in `Program.cs` that throws on missing or short secret
  - Document required environment variables in `README.md`

---

### Phase 11 — Documentation

- [ ] **T061** Update `README.md` in `moderntodo-frontend` with:
  - Project description and live demo URL
  - Local development setup (prerequisites, env vars, how to run)
  - Architecture overview (link to `context/architecture.md`)
  - How to run tests

- [ ] **T062** Create `README.md` in `moderntodo-api` with:
  - API endpoint reference (all routes, request/response shapes)
  - Required environment variables: `BLOB_CONNECTION_STRING`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`
  - Local development with Azurite (emulated blob storage)

- [ ] **T063** Update `context/project.md` with:
  - Problem statement: "A simple but modern personal task manager for organising daily work"
  - Persona: Individual user, personal productivity
  - Success criteria: Tasks load in < 500ms; CRUD operations complete in < 200ms; 80% test coverage
  - Key constraints: Azure-only deployment; no external auth providers in v1

---

## Data Model (Final Schema)

### `Tasks` Table
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `Title` | TEXT NOT NULL | Max 200 chars |
| `Description` | TEXT | Nullable |
| `DueDate` | TEXT | ISO-8601 datetime |
| `Priority` | INTEGER | 0=Low, 1=Medium, 2=High |
| `IsCompleted` | INTEGER | 0/1 boolean |
| `CreatedAt` | TEXT | ISO-8601 UTC |
| `UserId` | INTEGER FK → Users.Id | Added via migration |
| `CategoryId` | INTEGER FK → Categories.Id | Added via migration; nullable |

### `Users` Table
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `Email` | TEXT NOT NULL UNIQUE | Lowercase-normalised on insert |
| `PasswordHash` | TEXT NOT NULL | PBKDF2-HMAC-SHA256 with embedded salt |
| `CreatedAt` | TEXT NOT NULL | ISO-8601 UTC |

### `RefreshTokens` Table
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `UserId` | INTEGER FK → Users.Id ON DELETE CASCADE | |
| `Token` | TEXT NOT NULL UNIQUE | 64-byte random Base64 |
| `ExpiresAt` | TEXT NOT NULL | 30-day sliding window |
| `CreatedAt` | TEXT NOT NULL | |

### `Categories` Table
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `UserId` | INTEGER FK → Users.Id ON DELETE CASCADE | |
| `Name` | TEXT NOT NULL | |
| `Color` | TEXT NOT NULL | Hex color code, default `#6366f1` |

---

## API Contract Reference

### Auth Endpoints (NEW)
| Method | Route | Auth Required | Request Body | Response |
|---|---|---|---|---|
| POST | `/api/auth/register` | No | `{ email, password }` | `{ accessToken, refreshToken, expiresInSeconds }` |
| POST | `/api/auth/login` | No | `{ email, password }` | `{ accessToken, refreshToken, expiresInSeconds }` |
| POST | `/api/auth/refresh` | No | `{ refreshToken }` | `{ accessToken, refreshToken, expiresInSeconds }` |
| POST | `/api/auth/logout` | Yes (Bearer) | `{ refreshToken }` | `204 No Content` |

### Task Endpoints (EXISTING — updated to require auth)
| Method | Route | Auth Required | Notes |
|---|---|---|---|
| GET | `/api/tasks` | Yes | Optional `?completed=true/false&categoryId=N` |
| POST | `/api/tasks` | Yes | Creates task for authenticated user |
| PUT | `/api/tasks/{id}` | Yes | User must own the task |
| DELETE | `/api/tasks/{id}` | Yes | User must own the task |

### Category Endpoints (NEW)
| Method | Route | Auth Required | Request Body |
|---|---|---|---|
| GET | `/api/categories` | Yes | — |
| POST | `/api/categories` | Yes | `{ name, color }` |
| PUT | `/api/categories/{id}` | Yes | `{ name, color }` |
| DELETE | `/api/categories/{id}` | Yes | — |

---

## Environment Variables

### `moderntodo-api` (Azure Functions)
| Variable | Required | Description |
|---|---|---|
| `BLOB_CONNECTION_STRING` | Yes | Azure Blob Storage connection string (or Azurite for local dev) |
| `AzureWebJobsStorage` | Yes (Functions runtime) | Fallback storage for Functions; can mirror `BLOB_CONNECTION_STRING` |
| `JWT_SECRET` | Yes | Min 32-character random string; used to sign JWTs |
| `JWT_ISSUER` | Yes | e.g., `https://moderntodo-api.azurewebsites.net` |
| `JWT_AUDIENCE` | Yes | e.g., `https://moderntodo.azurestaticapps.net` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | For observability |

### `moderntodo-frontend` (Blazor WASM — `wwwroot/appsettings.json`)
| Key | Default | Description |
|---|---|---|
| `ApiBaseUrl` | (empty — uses `HostEnvironment.BaseAddress`) | Override for local dev to point at local Functions host |

---

## Definition of Done (per Constitution)

A task is complete when:
1. Code compiles with no warnings (`-warnaserror`)
2. Unit tests for that component pass (where applicable)
3. Overall test coverage remains ≥ 80%
4. No new critical/high CodeQL findings
5. PR reviewed and approved
6. No hardcoded secrets introduced

---

## Task Execution Order (Recommended)

```
T001 → T002 → T003 → T004                   (Foundation)
T005 → T006                                   (DB schema)
T007 → T008 → T009 → T010 → T011 → T012     (Auth models/services)
T013 → T014 → T015 → T016                   (Auth functions + task guard)
T017 → T018 → T019                           (Auth tests)
T020 → T021 → T022 → T023 → T024 → T025     (Categories backend)
T026 → T027 → T028 → T029 → T030 → T031     (Auth frontend services)
T032 → T033 → T034 → T035 → T036            (Auth frontend pages)
T037 → T038                                   (Auth frontend tests)
T039 → T040 → T041 → T042 → T043 → T044 → T045 → T046  (Categories frontend)
T047 → T048                                   (Search)
T049 → T050 → T051 → T052 → T053            (UI enhancements)
T054 → T055 → T056                           (CI/CD)
T057 → T058 → T059 → T060                   (Security)
T061 → T062 → T063                           (Docs)
```

Tasks marked `[P]` (T001, T002) can run in parallel since they touch different repos.

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SQLite in blob storage is not suitable for concurrent multi-user load | Medium | High | Document 10-user limit; plan migration to Azure SQL Serverless in v2 |
| JWT secret rotation breaks active sessions | Low | Medium | Implement token refresh; document rotation procedure in runbook |
| `ALTER TABLE` migration fails on corrupt db in blob | Low | High | Always backup blob before schema migration; wrap in try/catch with health check |
| Test coverage target 80% hard to hit for Blazor WASM | Medium | Medium | Use bUnit for component tests; focus on service layer for unit coverage |
| Azure Static Web Apps CSP incompatibility with Blazor WASM | Medium | Medium | Test CSP headers in staging before production deploy |
