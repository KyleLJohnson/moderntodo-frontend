# Implementation Plan: Add Time Task Was Entered to the UI

## Issue Reference
- **Issue:** Project: Modern To Do 5 (#10)
- **Feature:** Add time that task was entered to the UI
- **Repositories:** `KyleLJohnson/moderntodo-frontend` · `KyleLJohnson/moderntodo-api`
- **Architecture:** Modular Monolith
- **Test Coverage Target:** 80%
- **Governance:** L1 Product, L0 Enterprise, L3 Domain (Global Tax)

---

## Overview

The feature requires displaying the creation timestamp of each task in the Blazor WebAssembly frontend UI. The backend API (Azure Functions + SQLite via Azure Blob Storage) already stores and returns a `CreatedAt` field. The frontend data model (`TaskDto`) already maps that field. The only missing piece is rendering it in the task card UI and adding appropriate styling and tests.

---

## Current State Analysis

### Backend (`KyleLJohnson/moderntodo-api`)
| Component | File | Status |
|---|---|---|
| Database Schema | `DbService.cs` → `CreateSchemaAsync` | ✅ `CreatedAt TEXT NOT NULL` column already exists |
| Domain Model | `src/BlazorTodo.Api/Models/TodoTask.cs` | ✅ `CreatedAt` property present, defaults to `DateTime.UtcNow` |
| Data Layer | `src/BlazorTodo.Api/Services/TaskRepository.cs` → `CreateAsync` | ✅ Sets `task.CreatedAt = DateTime.UtcNow` before insert |
| API Layer | `src/BlazorTodo.Api/Functions/TaskFunctions.cs` → `CreateTask` / `GetTasks` | ✅ `CreatedAt` included in JSON response via model serialization |

**Backend requires no changes.**

### Frontend (`KyleLJohnson/moderntodo-frontend`)
| Component | File | Status |
|---|---|---|
| Data Transfer Object | `src/BlazorTodo/Models/TaskDto.cs` | ✅ `CreatedAt` property present (`DateTime CreatedAt`) |
| API Service | `src/BlazorTodo/Services/TaskApiService.cs` | ✅ Deserializes full `TaskDto` including `CreatedAt` |
| Task Card UI | `src/BlazorTodo/Components/TaskCard.razor` | ❌ Does **not** display `CreatedAt` |
| Global Styles | `src/BlazorTodo/wwwroot/css/app.css` | ❌ No `.created-at` style class exists |

---

## Implementation Tasks

### 1. Frontend – Display `CreatedAt` in `TaskCard.razor`

**File:** `src/BlazorTodo/Components/TaskCard.razor`

**Change:** Add a creation timestamp element inside `.task-meta` div, after the due-date span.

```razor
@if (Task.CreatedAt != default)
{
    <span class="created-at" title="Created @Task.CreatedAt.ToLocalTime().ToString("f")">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
        </svg>
        @FormatCreatedAt(Task.CreatedAt)
    </span>
}
```

Add the helper method in `@code`:

```csharp
private static string FormatCreatedAt(DateTime createdAt)
{
    var local = createdAt.ToLocalTime();
    var diff = DateTime.Now - local;

    if (diff.TotalMinutes < 1) return "just now";
    if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
    if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
    if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
    return local.ToString("MMM d");
}
```

**Placement:** Inside the `.task-meta` div, after the existing `@if (Task.DueDate.HasValue)` block.

---

### 2. Frontend – Add CSS Styling for `created-at`

**File:** `src/BlazorTodo/wwwroot/css/app.css`

**Change:** Add a `.created-at` style class consistent with the existing `.due-date` style pattern.

```css
.created-at {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-size: 0.75rem;
    color: var(--text-muted, #8a8a8a);
}

.created-at svg {
    width: 12px;
    height: 12px;
    flex-shrink: 0;
}
```

**Placement:** After the `.due-date` and `.due-date.overdue` CSS rules in `app.css`.

---

### 3. Backend – No Changes Required

The backend already:
- Creates `CreatedAt` via `task.CreatedAt = DateTime.UtcNow` in `TaskRepository.CreateAsync`
- Stores `CreatedAt` as `TEXT NOT NULL` in SQLite schema
- Returns `CreatedAt` in all GET and POST API responses via JSON serialization

No backend code changes are needed for this feature.

---

### 4. Database – No Changes Required

The SQLite database schema already includes the `CreatedAt` column:

```sql
CREATE TABLE IF NOT EXISTS Tasks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Title       TEXT    NOT NULL,
    Description TEXT,
    DueDate     TEXT,
    Priority    INTEGER NOT NULL DEFAULT 1,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CreatedAt   TEXT    NOT NULL
);
```

No database migrations are required.

---

### 5. Testing

Since there is currently no test project in the frontend repository, testing tasks are defined here for implementation agents to set up. The target is **80% line coverage**.

#### 5a. Create Test Project (Frontend)

**Action:** Add a new xUnit test project to the solution.

```bash
dotnet new xunit -n BlazorTodo.Tests -o tests/BlazorTodo.Tests --framework net10.0
dotnet sln BlazorTodo.slnx add tests/BlazorTodo.Tests/BlazorTodo.Tests.csproj
```

Add project reference and required packages:

```xml
<!-- tests/BlazorTodo.Tests/BlazorTodo.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\src\BlazorTodo\BlazorTodo.csproj" />
  <PackageReference Include="bunit" Version="1.*" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  <PackageReference Include="coverlet.collector" Version="6.*" />
</ItemGroup>
```

#### 5b. Unit Tests for `FormatCreatedAt` Helper

**File:** `tests/BlazorTodo.Tests/Components/TaskCardTests.cs`

**Test cases:**

| Input | Expected Output |
|---|---|
| `DateTime.UtcNow` | `"just now"` |
| `DateTime.UtcNow.AddMinutes(-30)` | `"30m ago"` |
| `DateTime.UtcNow.AddHours(-3)` | `"3h ago"` |
| `DateTime.UtcNow.AddDays(-2)` | `"2d ago"` |
| `DateTime.UtcNow.AddDays(-10)` | formatted as `"MMM d"` (e.g. `"Mar 22"`) |
| `default(DateTime)` | element not rendered (conditional guard) |

#### 5c. Component Rendering Tests (bUnit)

**File:** `tests/BlazorTodo.Tests/Components/TaskCardRenderTests.cs`

Verify that:
- When `Task.CreatedAt` is set, the `.created-at` span is rendered in the DOM
- When `Task.CreatedAt == default`, the `.created-at` span is **not** rendered
- The tooltip (`title` attribute) contains the full formatted date/time string
- Relative time text is visible for tasks created recently

#### 5d. Backend Tests (in `KyleLJohnson/moderntodo-api`)

**File:** `tests/BlazorTodo.Api.Tests/Services/TaskRepositoryTests.cs`

Verify that:
- `CreateAsync` sets `CreatedAt` to a non-default `DateTime`
- `CreatedAt` is close to `DateTime.UtcNow` (within 1 second tolerance)
- `GetAllAsync` returns tasks ordered by `CreatedAt DESC`
- The returned DTO includes a valid `CreatedAt` timestamp

---

## Acceptance Criteria

- [ ] Each task card in the UI shows a human-readable creation time (e.g., "just now", "3h ago", "Mar 22")
- [ ] Hovering over the creation time shows the full date/time as a tooltip
- [ ] The creation time style is visually consistent with the existing due-date meta element
- [ ] `CreatedAt` is `default(DateTime)` only if not supplied by the API — in that case the element is hidden
- [ ] All new code paths are covered by unit/component tests
- [ ] Frontend builds successfully (`dotnet build`)
- [ ] Test coverage ≥ 80% for changed/added code

---

## File Change Summary

### Frontend (`KyleLJohnson/moderntodo-frontend`)

| File | Action | Description |
|---|---|---|
| `src/BlazorTodo/Components/TaskCard.razor` | **Modify** | Add `<span class="created-at">` in `.task-meta`, add `FormatCreatedAt` helper method |
| `src/BlazorTodo/wwwroot/css/app.css` | **Modify** | Add `.created-at` and `.created-at svg` CSS rules |
| `tests/BlazorTodo.Tests/BlazorTodo.Tests.csproj` | **Create** | New xUnit + bUnit test project |
| `tests/BlazorTodo.Tests/Components/TaskCardTests.cs` | **Create** | Unit tests for `FormatCreatedAt` logic |
| `tests/BlazorTodo.Tests/Components/TaskCardRenderTests.cs` | **Create** | bUnit component rendering tests |
| `BlazorTodo.slnx` | **Modify** | Add test project to solution |

### Backend (`KyleLJohnson/moderntodo-api`)

| File | Action | Description |
|---|---|---|
| No changes required | — | Backend already fully implements `CreatedAt` storage and retrieval |
| `tests/BlazorTodo.Api.Tests/Services/TaskRepositoryTests.cs` | **Create** (optional) | Integration tests for `CreatedAt` in repository (if test project doesn't exist) |

---

## Implementation Order

1. **Frontend UI change** – `TaskCard.razor` (add created-at display + helper method)
2. **Frontend CSS** – `app.css` (add `.created-at` styles)
3. **Frontend tests** – Create test project and write unit + component tests
4. **Backend tests** – Add `CreatedAt` assertions to backend test suite (if applicable)
5. **Build & validate** – `dotnet build` + `dotnet test` in both repos

---

## Design Notes

- Use relative time formatting for recency ("just now", "5m ago", "3h ago", "2d ago") for tasks created within the last 7 days, then fall back to "MMM d" format for older tasks. This matches common modern UI patterns.
- The clock SVG icon used should match the existing icon style (24px viewBox, 2px stroke, no fill) consistent with other meta icons in `TaskCard.razor`.
- `DateTime.UtcNow` is used in the backend; convert to local time (`ToLocalTime()`) in the frontend before display and tooltip formatting.
- No new NuGet dependencies are required for the frontend feature itself (only for the test project: bUnit).
- The feature is purely additive — no breaking changes to the API contract or database schema.
