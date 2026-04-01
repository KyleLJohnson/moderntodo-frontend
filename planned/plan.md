# Modern To Do 11 — Implementation Plan

> **Project:** Modern To Do 11  
> **Governance:** L0 Enterprise / L1 Product / L3 Domain (Global Tax)  
> **Architecture:** Modular Monolith  
> **Test Coverage Target:** 80%  
> **Frontend Repo:** KyleLJohnson/moderntodo-frontend (Blazor WebAssembly, .NET 10)  
> **Backend Repo:** KyleLJohnson/moderntodo-api (Azure Functions v4, .NET 8)  
> **Primary AI Agent:** GitHub Copilot (claude-sonnet-4-5)

---

## Current State Summary

Both repositories contain a working baseline To-Do application:

| Layer | Technology | Status |
|---|---|---|
| Frontend | Blazor WebAssembly (.NET 10), Azure Static Web App | ✅ Baseline CRUD UI |
| Backend | Azure Functions v4 (.NET 8), Dapper, SQLite | ✅ Baseline CRUD API |
| Database | SQLite file stored in Azure Blob Storage | ✅ Basic schema |
| Auth | None — all endpoints are `AuthorizationLevel.Anonymous` | ❌ Missing |
| Tests | None in either repository | ❌ Missing |
| Search | Not implemented | ❌ Missing |
| Sort | Not implemented (hardcoded `ORDER BY CreatedAt DESC`) | ❌ Missing |
| Categories/Tags | Not implemented | ❌ Missing |

---

## Implementation Tasks

### Phase 1 — Foundation & Quality (Do first — blocks everything else)

#### 1.1 Backend: Unit & Integration Tests (KyleLJohnson/moderntodo-api)

- [ ] **T001** Create a `tests/BlazorTodo.Api.Tests` xUnit project and add to `BlazorTodo.Api.slnx`
  - File: `tests/BlazorTodo.Api.Tests/BlazorTodo.Api.Tests.csproj`
  - Dependencies: `xunit`, `Moq`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`
- [ ] **T002** Write unit tests for `TaskRepository` covering all five public methods:
  - `GetAllAsync(null)` — returns all tasks ordered newest-first
  - `GetAllAsync(true)` — filters to completed tasks only
  - `GetAllAsync(false)` — filters to incomplete tasks only
  - `GetByIdAsync(existingId)` — returns the matching task
  - `GetByIdAsync(missingId)` — returns `null`
  - `CreateAsync(task)` — inserts and returns task with auto-assigned `Id` and `CreatedAt`
  - `UpdateAsync(existingId, task)` — updates fields and returns updated task
  - `UpdateAsync(missingId, task)` — returns `null`
  - `DeleteAsync(existingId)` — deletes and returns `true`
  - `DeleteAsync(missingId)` — returns `false`
  - File: `tests/BlazorTodo.Api.Tests/Services/TaskRepositoryTests.cs`
  - Use an in-memory SQLite connection (`Data Source=:memory:`) to avoid blob dependency
- [ ] **T003** Write unit tests for `TaskFunctions` HTTP trigger functions:
  - `GetTasks` — returns 200 with task list
  - `GetTasks?completed=true` — filters tasks
  - `CreateTask` with valid body — returns 201 and the created task
  - `CreateTask` with missing/empty title — returns 400
  - `UpdateTask` with valid body and existing id — returns 200
  - `UpdateTask` with missing id — returns 404
  - `DeleteTask` with existing id — returns 204
  - `DeleteTask` with missing id — returns 404
  - File: `tests/BlazorTodo.Api.Tests/Functions/TaskFunctionsTests.cs`
  - Mock `TaskRepository` using `Moq`
- [ ] **T004** Add a GitHub Actions CI workflow to `moderntodo-api`:
  - File: `.github/workflows/ci.yml`
  - Trigger on `push` and `pull_request` to `main`
  - Steps: checkout → setup dotnet 8 → restore → build → test with coverlet (80% threshold)

#### 1.2 Frontend: Unit Tests (KyleLJohnson/moderntodo-frontend)

- [ ] **T005** Create a `tests/BlazorTodo.Tests` bUnit project and add to `BlazorTodo.slnx`
  - File: `tests/BlazorTodo.Tests/BlazorTodo.Tests.csproj`
  - Dependencies: `bunit`, `xunit`, `Moq`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`
- [ ] **T006** Write bUnit component tests for `TaskCard`:
  - Renders task title, description, priority badge
  - Shows overdue styling when `DueDate` is in the past and task is not complete
  - Complete button shows checkmark when `IsCompleted = true`
  - `OnToggleComplete` callback fires when complete button clicked
  - `OnEdit` callback fires when edit button clicked
  - `OnDelete` callback fires when delete button clicked
  - File: `tests/BlazorTodo.Tests/Components/TaskCardTests.cs`
- [ ] **T007** Write bUnit component tests for `TaskForm`:
  - Renders in "New Task" mode when `EditTask = null`
  - Renders in "Edit Task" mode when `EditTask` is provided, pre-populating fields
  - Validation message appears when form submitted with empty title
  - `OnSave` callback fires with correct data on valid submit
  - `OnCancel` callback fires when cancel button clicked
  - File: `tests/BlazorTodo.Tests/Components/TaskFormTests.cs`
- [ ] **T008** Write bUnit tests for `Home` page:
  - Displays spinner while loading
  - Displays empty state when no tasks returned
  - Displays task list when tasks are returned
  - Filter tabs change visible task count
  - File: `tests/BlazorTodo.Tests/Pages/HomeTests.cs`
  - Mock `TaskApiService` using `Moq`
- [ ] **T009** Add a GitHub Actions CI workflow to `moderntodo-frontend`:
  - File: `.github/workflows/ci.yml`
  - Trigger on `push` and `pull_request` to `main`
  - Steps: checkout → setup dotnet 10 → restore → build → test with coverlet (80% threshold)

---

### Phase 2 — Core Feature: Search & Sort

#### 2.1 Backend: Search & Sort API (KyleLJohnson/moderntodo-api)

- [ ] **T010** Extend `TaskRepository.GetAllAsync` to accept `searchTerm` and `sortBy` parameters:
  - Method signature: `GetAllAsync(bool? completed, string? searchTerm, string? sortBy)`
  - `searchTerm` performs case-insensitive LIKE match on `Title` and `Description`
  - `sortBy` supports: `created_desc` (default), `created_asc`, `priority_desc`, `priority_asc`, `duedate_asc`, `duedate_desc`
  - File: `src/BlazorTodo.Api/Services/TaskRepository.cs`
- [ ] **T011** Update `TaskFunctions.GetTasks` to extract and pass `search` and `sortBy` query parameters:
  - URL: `GET /api/tasks?completed=true&search=keyword&sortBy=priority_desc`
  - File: `src/BlazorTodo.Api/Functions/TaskFunctions.cs`
- [ ] **T012** Add unit tests for new search and sort behavior:
  - File: `tests/BlazorTodo.Api.Tests/Services/TaskRepositoryTests.cs` (extend T002)

#### 2.2 Frontend: Search & Sort UI (KyleLJohnson/moderntodo-frontend)

- [ ] **T013** Add a `SearchSortBar` component:
  - Search input with debounce (300 ms) that fires a callback `OnSearch`
  - Sort dropdown (`Newest`, `Oldest`, `Priority ↓`, `Priority ↑`, `Due Date ↑`, `Due Date ↓`) that fires `OnSortChange`
  - File: `src/BlazorTodo/Components/SearchSortBar.razor`
- [ ] **T014** Update `Home.razor` to include `SearchSortBar` and pass `search`/`sortBy` to the API:
  - Debounced search updates `_search` state variable → triggers `LoadTasks`
  - Sort change updates `_sortBy` state variable → triggers `LoadTasks`
  - File: `src/BlazorTodo/Pages/Home.razor`
- [ ] **T015** Update `TaskApiService.GetTasksAsync` to accept `searchTerm` and `sortBy`:
  - Method signature: `GetTasksAsync(bool? completed, string? searchTerm, string? sortBy)`
  - Append non-null parameters to the query string
  - File: `src/BlazorTodo/Services/TaskApiService.cs`
- [ ] **T016** Add CSS for the `SearchSortBar` component:
  - File: `src/BlazorTodo/wwwroot/css/app.css` (append new rules)
- [ ] **T017** Add bUnit tests for `SearchSortBar`:
  - File: `tests/BlazorTodo.Tests/Components/SearchSortBarTests.cs`

---

### Phase 3 — Core Feature: Categories / Tags

#### 3.1 Backend: Categories (KyleLJohnson/moderntodo-api)

- [ ] **T018** Add `Category` model:
  ```csharp
  // src/BlazorTodo.Api/Models/Category.cs
  public class Category
  {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public string? Color { get; set; }   // hex color, e.g. "#4f46e5"
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
  ```
  - File: `src/BlazorTodo.Api/Models/Category.cs`
- [ ] **T019** Add `CategoryId` (nullable `int`) to `TodoTask` model:
  - File: `src/BlazorTodo.Api/Models/TodoTask.cs`
- [ ] **T020** Update `DbService.CreateSchemaAsync` to create a `Categories` table and add `CategoryId` column to `Tasks`:
  ```sql
  CREATE TABLE IF NOT EXISTS Categories (
      Id        INTEGER PRIMARY KEY AUTOINCREMENT,
      Name      TEXT NOT NULL,
      Color     TEXT,
      CreatedAt TEXT NOT NULL
  );
  ALTER TABLE Tasks ADD COLUMN IF NOT EXISTS CategoryId INTEGER REFERENCES Categories(Id) ON DELETE SET NULL;
  ```
  - File: `src/BlazorTodo.Api/Services/DbService.cs`
- [ ] **T021** Create `CategoryRepository` with full CRUD:
  - `GetAllAsync()` → `SELECT * FROM Categories ORDER BY Name`
  - `GetByIdAsync(int id)` → `SELECT * FROM Categories WHERE Id = @Id`
  - `CreateAsync(Category cat)` → INSERT + return with Id
  - `UpdateAsync(int id, Category cat)` → UPDATE or null if not found
  - `DeleteAsync(int id)` → DELETE, return bool
  - File: `src/BlazorTodo.Api/Services/CategoryRepository.cs`
- [ ] **T022** Create `CategoryFunctions` Azure Function class:
  - `GET /api/categories` → `GetCategories`
  - `POST /api/categories` → `CreateCategory`
  - `PUT /api/categories/{id:int}` → `UpdateCategory`
  - `DELETE /api/categories/{id:int}` → `DeleteCategory`
  - File: `src/BlazorTodo.Api/Functions/CategoryFunctions.cs`
- [ ] **T023** Update `TaskRepository.GetAllAsync` and `CreateAsync`/`UpdateAsync` to join `Categories` and populate `CategoryId`
- [ ] **T024** Register `CategoryRepository` in `Program.cs`:
  - File: `src/BlazorTodo.Api/Program.cs`
- [ ] **T025** Write unit tests for `CategoryRepository` and `CategoryFunctions`:
  - File: `tests/BlazorTodo.Api.Tests/Services/CategoryRepositoryTests.cs`
  - File: `tests/BlazorTodo.Api.Tests/Functions/CategoryFunctionsTests.cs`

#### 3.2 Frontend: Categories UI (KyleLJohnson/moderntodo-frontend)

- [ ] **T026** Add `CategoryDto` model to frontend:
  ```csharp
  // src/BlazorTodo/Models/CategoryDto.cs
  public class CategoryDto
  {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public string? Color { get; set; }
      public DateTime CreatedAt { get; set; }
  }
  ```
  - File: `src/BlazorTodo/Models/CategoryDto.cs`
- [ ] **T027** Add `CategoryId` (nullable `int`) to `TaskDto`:
  - File: `src/BlazorTodo/Models/TaskDto.cs`
- [ ] **T028** Create `CategoryApiService` for CRUD operations against `/api/categories`:
  - File: `src/BlazorTodo/Services/CategoryApiService.cs`
- [ ] **T029** Register `CategoryApiService` in `Program.cs`:
  - File: `src/BlazorTodo/Program.cs`
- [ ] **T030** Add a category filter chip row above the task list in `Home.razor`:
  - Load categories on `OnInitializedAsync`
  - Render one chip per category; clicking a chip sets `_categoryFilter`
  - Include an "All Categories" chip to clear the filter
  - Pass `categoryId` to `TaskApiService.GetTasksAsync` when set
  - File: `src/BlazorTodo/Pages/Home.razor`
- [ ] **T031** Add a `CategorySelect` dropdown inside `TaskForm` so users can assign a category when creating or editing a task:
  - File: `src/BlazorTodo/Components/TaskForm.razor`
- [ ] **T032** Add a `CategoryManager` page at `/categories` to create, rename, recolor, and delete categories:
  - File: `src/BlazorTodo/Pages/Categories.razor`
- [ ] **T033** Add "Categories" link to the `NavMenu`:
  - File: `src/BlazorTodo/Layout/NavMenu.razor`
- [ ] **T034** Add CSS for category chips and category-colored badges:
  - File: `src/BlazorTodo/wwwroot/css/app.css`
- [ ] **T035** Add bUnit tests for `CategorySelect` and `CategoryManager`:
  - File: `tests/BlazorTodo.Tests/Components/CategorySelectTests.cs`
  - File: `tests/BlazorTodo.Tests/Pages/CategoryManagerTests.cs`

---

### Phase 4 — UX Enhancement: Bulk Operations

#### 4.1 Backend: Bulk API (KyleLJohnson/moderntodo-api)

- [ ] **T036** Add a bulk-complete endpoint `PATCH /api/tasks/bulk-complete`:
  - Request body: `{ "ids": [1, 2, 3] }`
  - Marks all specified tasks as completed; returns count of updated rows
  - File: `src/BlazorTodo.Api/Functions/TaskFunctions.cs`
- [ ] **T037** Add a bulk-delete endpoint `DELETE /api/tasks/bulk`:
  - Request body: `{ "ids": [1, 2, 3] }`
  - Deletes all specified tasks; returns count of deleted rows
  - File: `src/BlazorTodo.Api/Functions/TaskFunctions.cs`
- [ ] **T038** Add `BulkCompleteAsync(IEnumerable<int> ids)` and `BulkDeleteAsync(IEnumerable<int> ids)` to `TaskRepository`:
  - Use parameterized `IN (...)` clause via Dapper
  - File: `src/BlazorTodo.Api/Services/TaskRepository.cs`
- [ ] **T039** Write unit tests for bulk operations:
  - File: `tests/BlazorTodo.Api.Tests/Services/TaskRepositoryTests.cs`
  - File: `tests/BlazorTodo.Api.Tests/Functions/TaskFunctionsTests.cs`

#### 4.2 Frontend: Bulk Operations UI (KyleLJohnson/moderntodo-frontend)

- [ ] **T040** Add a selection checkbox to `TaskCard`:
  - Checkbox appears on hover (or always on mobile) when bulk mode is active
  - File: `src/BlazorTodo/Components/TaskCard.razor`
- [ ] **T041** Add a `BulkActionBar` component that appears when one or more tasks are selected:
  - Shows "X selected", a "Complete Selected" button, and a "Delete Selected" button
  - File: `src/BlazorTodo/Components/BulkActionBar.razor`
- [ ] **T042** Update `Home.razor` to manage `_selectedIds` state and wire up bulk actions:
  - File: `src/BlazorTodo/Pages/Home.razor`
- [ ] **T043** Add `BulkCompleteAsync(List<int> ids)` and `BulkDeleteAsync(List<int> ids)` to `TaskApiService`:
  - File: `src/BlazorTodo/Services/TaskApiService.cs`
- [ ] **T044** Add CSS for selection checkbox and `BulkActionBar`:
  - File: `src/BlazorTodo/wwwroot/css/app.css`

---

### Phase 5 — UX Enhancement: Dark Mode

#### 5.1 Frontend: Dark Mode Toggle (KyleLJohnson/moderntodo-frontend)

- [ ] **T045** Add a `ThemeService` that reads/writes a `theme` preference to `localStorage`:
  - Exposes `IsDark` boolean and `Toggle()` method
  - Raises a `ThemeChanged` event that triggers a `StateHasChanged`
  - File: `src/BlazorTodo/Services/ThemeService.cs`
- [ ] **T046** Register `ThemeService` as a singleton in `Program.cs`:
  - File: `src/BlazorTodo/Program.cs`
- [ ] **T047** Add dark mode CSS custom properties to `app.css` using `[data-theme="dark"]` selector:
  - Override `--bg`, `--surface`, `--border`, `--text`, `--text-muted` for dark mode
  - File: `src/BlazorTodo/wwwroot/css/app.css`
- [ ] **T048** Add a sun/moon toggle button to `MainLayout.razor` that calls `ThemeService.Toggle()`:
  - Apply `data-theme="dark"` attribute to `<html>` via `JS interop` when dark is active
  - File: `src/BlazorTodo/Layout/MainLayout.razor`
- [ ] **T049** Add a `setTheme.js` interop helper in `wwwroot`:
  - `window.setTheme = (theme) => document.documentElement.setAttribute('data-theme', theme);`
  - File: `src/BlazorTodo/wwwroot/setTheme.js`
- [ ] **T050** Reference `setTheme.js` in `index.html`:
  - File: `src/BlazorTodo/wwwroot/index.html`

---

### Phase 6 — Security Hardening

#### 6.1 Backend: Input Validation & CORS (KyleLJohnson/moderntodo-api)

- [ ] **T051** Add validation attributes and constants to `TodoTask`:
  - `Title` max length 200 characters — reject at function level (already partially enforced via `IsNullOrWhiteSpace` check)
  - `Description` max length 2000 characters
  - Add explicit length checks in `TaskFunctions.CreateTask` and `UpdateTask`
  - File: `src/BlazorTodo.Api/Functions/TaskFunctions.cs`
- [ ] **T052** Add validation to `CategoryFunctions.CreateCategory` and `UpdateCategory`:
  - `Name` required, max 100 characters
  - `Color` must be a valid 7-char hex string (regex: `^#[0-9a-fA-F]{6}$`) or null
  - File: `src/BlazorTodo.Api/Functions/CategoryFunctions.cs`
- [ ] **T053** Add CORS configuration to `host.json` for local development:
  - File: `src/BlazorTodo.Api/host.json`
  - Allow origin `http://localhost:5000` and `http://localhost:5001`
- [ ] **T054** Tighten Content-Security-Policy in `staticwebapp.config.json`:
  - Remove `'unsafe-eval'` where possible; scope `script-src` to `'wasm-unsafe-eval'` only for WASM
  - File: `src/BlazorTodo/wwwroot/staticwebapp.config.json`

#### 6.2 Frontend: Security & Accessibility (KyleLJohnson/moderntodo-frontend)

- [ ] **T055** Add `aria-label` attributes to all icon-only buttons in `TaskCard` and `TaskForm`:
  - Files: `src/BlazorTodo/Components/TaskCard.razor`, `src/BlazorTodo/Components/TaskForm.razor`
- [ ] **T056** Add `role="status"` and `aria-live="polite"` to the loading/error state boxes in `Home.razor`:
  - File: `src/BlazorTodo/Pages/Home.razor`
- [ ] **T057** Add keyboard navigation support for the filter bar (arrow keys cycle between tabs):
  - File: `src/BlazorTodo/Pages/Home.razor`

---

### Phase 7 — Infrastructure & DevOps

#### 7.1 Infrastructure Updates

- [ ] **T058** Update `infra/main.bicep` (moderntodo-api) to include the new `Categories` table initialization in the startup script (or note that schema migration runs at app startup via `DbService`)
- [ ] **T059** Update `azure.yaml` in both repos to reflect the current app names and resource group conventions
- [ ] **T060** Add environment-specific `appsettings.{env}.json` pattern for the frontend to point to staging vs production API URLs:
  - File: `src/BlazorTodo/wwwroot/appsettings.Staging.json`
  - File: `src/BlazorTodo/wwwroot/appsettings.Production.json`

#### 7.2 Developer Experience

- [ ] **T061** Update `README.md` in both repos with:
  - Prerequisites (Node.js, .NET SDK versions, Azure Functions Core Tools)
  - Local run instructions (both repos side by side)
  - Environment variable list (`BLOB_CONNECTION_STRING` / `AzureWebJobsStorage`, `ApiBaseUrl`)
  - How to run tests and check coverage
- [ ] **T062** Add a `SETUP.md` update in `moderntodo-frontend` to document the new features (search, sort, categories, bulk ops, dark mode)

---

## Data Models (Final State)

### Backend: `TodoTask` (src/BlazorTodo.Api/Models/TodoTask.cs)
```csharp
public class TodoTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;        // max 200 chars
    public string? Description { get; set; }                  // max 2000 chars
    public DateTime? DueDate { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public bool IsCompleted { get; set; }
    public int? CategoryId { get; set; }                      // NEW
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Backend: `Category` (src/BlazorTodo.Api/Models/Category.cs) — NEW
```csharp
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;   // max 100 chars
    public string? Color { get; set; }                  // hex color e.g. "#4f46e5"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Database Schema (SQLite)
```sql
-- Existing table (updated)
CREATE TABLE IF NOT EXISTS Tasks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Title       TEXT    NOT NULL,
    Description TEXT,
    DueDate     TEXT,
    Priority    INTEGER NOT NULL DEFAULT 1,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CategoryId  INTEGER REFERENCES Categories(Id) ON DELETE SET NULL,  -- NEW
    CreatedAt   TEXT    NOT NULL
);

-- New table
CREATE TABLE IF NOT EXISTS Categories (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT NOT NULL,
    Color     TEXT,
    CreatedAt TEXT NOT NULL
);
```

---

## API Contracts (Final State)

### Tasks

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/tasks` | List tasks. Query: `completed`, `search`, `sortBy`, `categoryId` |
| `POST` | `/api/tasks` | Create task |
| `PUT` | `/api/tasks/{id}` | Update task |
| `DELETE` | `/api/tasks/{id}` | Delete task |
| `PATCH` | `/api/tasks/bulk-complete` | Bulk complete — body: `{ "ids": [...] }` |
| `DELETE` | `/api/tasks/bulk` | Bulk delete — body: `{ "ids": [...] }` |

### Categories

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/categories` | List all categories |
| `POST` | `/api/categories` | Create category |
| `PUT` | `/api/categories/{id}` | Update category |
| `DELETE` | `/api/categories/{id}` | Delete category |

---

## Acceptance Criteria

| Feature | Done When |
|---|---|
| Tests (backend) | `dotnet test` passes with ≥ 80% line coverage |
| Tests (frontend) | `dotnet test` passes with ≥ 80% line coverage |
| Search | Typing in search box filters tasks by title/description in real time |
| Sort | Dropdown changes task order (priority, due date, created date) |
| Categories | User can create categories with a name and color |
| Categories | Tasks can be assigned to a category |
| Categories | Clicking a category chip filters the task list |
| Bulk ops | Selecting multiple tasks shows bulk action bar |
| Bulk ops | "Complete Selected" marks all selected tasks done |
| Bulk ops | "Delete Selected" removes all selected tasks |
| Dark mode | Toggle button switches between light and dark themes persistently |
| Security | CSP headers do not include `unsafe-eval` |
| Security | All user inputs have max-length enforcement on backend |
| Accessibility | All icon-only buttons have `aria-label` |
| CI | Both repos have GitHub Actions CI that runs tests on PR |

---

## Implementation Order

```
Phase 1 (Tests + CI)   → Must be done first; all later PRs require green CI
Phase 2 (Search/Sort)  → No schema change; low risk; high value
Phase 3 (Categories)   → Schema change; requires migration guard in DbService
Phase 4 (Bulk Ops)     → New endpoints + UI; moderate complexity
Phase 5 (Dark Mode)    → Frontend only; low risk
Phase 6 (Security)     → Review pass on all previous phases
Phase 7 (Infra/Docs)   → Finalize for deployment
```

---

## Notes for Implementation Agents

1. **Schema migration**: `DbService.CreateSchemaAsync` uses `CREATE TABLE IF NOT EXISTS`. For the new `CategoryId` column on `Tasks`, use `ALTER TABLE Tasks ADD COLUMN IF NOT EXISTS CategoryId INTEGER REFERENCES Categories(Id) ON DELETE SET NULL` inside a try/catch block to handle the case where the column already exists (SQLite does not support `IF NOT EXISTS` on `ALTER TABLE`).

2. **Test isolation**: Backend tests should use `new SqliteConnection("Data Source=:memory:")` and call `CreateSchemaAsync` directly before each test. Do NOT mock the DB layer — use real SQLite in-memory for repository tests.

3. **Blazor WASM testing**: Use [bUnit](https://bunit.dev/) for component tests. Mock `TaskApiService` and `CategoryApiService` using `Moq` with `MockHttpMessageHandler` or a test double.

4. **Debounce**: The search input debounce (300 ms) should be implemented as a `System.Timers.Timer` in the `SearchSortBar` component, reset on each `oninput` event.

5. **Dark mode JS interop**: Use `IJSRuntime.InvokeVoidAsync("setTheme", isDark ? "dark" : "light")` in `MainLayout.razor`. Read the saved preference from `localStorage` on first load by calling `localStorage.getItem('theme')` via interop.

6. **Coverage target**: Configure `coverlet` with `--threshold 80 --threshold-type line`. Exclude auto-generated files with `[ExcludeFromCodeCoverage]` attribute sparingly.

7. **CORS**: Azure Functions v4 with `ConfigureFunctionsWebApplication()` supports CORS via ASP.NET Core middleware. Add `.ConfigureWebHostDefaults(b => b.UseSetting(...))` or configure via `local.settings.json` for local dev. For SWA production, CORS is handled automatically by the SWA proxy.
