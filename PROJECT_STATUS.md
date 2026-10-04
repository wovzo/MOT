# Mind On Track (MOT) - Project Status

## Database & EF Core Migration Audit & Correction (StudySession)

* **Original Migration Issue:** The pending migration `20260922051353_AddStudySessions` was inspected and found to define `UserId` as `longtext` (string) with no Foreign Key to the `Users` table and no index.
* **Database State:** The original migration was NOT applied to the database.
* **Model Correction:**
  * `StudySession.UserId` in `MOT.Domain/Entities/StudySession.cs` was corrected from `string` to `Guid`.
  * Navigation property `public User? User { get; set; }` was added.
  * EF Core entity configuration was added in `AppDbContext.OnModelCreating` to establish a required One-to-Many relationship (`User -> StudySessions`) with Foreign Key `StudySession.UserId -> Users.Id`, `DeleteBehavior.Cascade`, and an index on `UserId`.
  * MediatR handlers (`StartStudySessionCommand`, `EndStudySessionCommand`, `GetStudySessionsQuery`) were updated to parse the GUID user ID.
* **Migration Removal & Regeneration:**
  * The incorrect unapplied migration `20260922051353_AddStudySessions` and its designer file were removed using `dotnet ef migrations remove`, cleanly reverting the model snapshot.
  * A corrected migration `20261002081502_AddStudySessions` was generated.
* **SQL Generation for Review:**
  * SQL script `study-session-migration.sql` was generated using `dotnet ef migrations script 20260909161703_AddHabits 20261002081502_AddStudySessions`.
  * Manual review confirmed:
    * `UserId` column is `char(36) COLLATE ascii_general_ci NOT NULL`.
    * Foreign key `FK_StudySessions_Users_UserId` referencing `Users(Id)` with `ON DELETE CASCADE`.
    * Index `IX_StudySessions_UserId` on `StudySessions(UserId)`.
* **Database Safety:**
  * `dotnet ef database update` was intentionally NOT performed.
  * Database update was NOT run. The live database was NOT modified.
* **Final Status:**
  * Build: `Build succeeded. 0 Warning(s), 0 Error(s)`.
  * EF Migrations Status: `20261002081502_AddStudySessions (Pending)`.

## Automated Tests (StudySessions)

* **Test Suite Added:** `MOT.Tests/StudySessions/StudySessionHandlerTests.cs` (8 test executions, 100% passing).
* **Coverage:**
  * Start session with authenticated user GUID.
  * Rejection of missing/invalid authenticated user.
  * Ending own study session (completing session and calculating duration).
  * Preventing termination of another user's session (isolation & not found validation).
  * Query scoping returning exclusively the authenticated user's sessions.
  * Reflection & execution assertion that client input cannot provide or override `UserId`.
* **Database & Migration Status:** Unchanged; database update was NOT run, migrations were NOT modified.

## Authentication Security Hardening — P0.1B

**Status: VERIFIED / COMPLETE**

Authentication security hardening was implemented and independently audited.

### Security changes

- Replaced simulated `hashed_...` password storage with cryptographic password hashing.
- Added `IPasswordHasher` abstraction and ASP.NET Core Identity `PasswordHasher<User>` implementation.
- Registration now hashes the supplied password before persistence.
- Login now verifies the supplied password against the stored password hash.
- Invalid credentials return uniform `401 Unauthorized` semantics without user enumeration.
- Duplicate registration returns `409 Conflict`.
- Validation failures return `400 Bad Request`.
- Unexpected server failures return generic `500 Internal Server Error` responses while detailed exceptions remain server-side logs.
- Activated FluentValidation through the MediatR validation pipeline.
- Removed hardcoded JWT secret fallback from application code.
- JWT configuration now fails fast when `Jwt:Secret` is absent.
- Authentication responses do not expose plaintext passwords or password hashes.

### Verification

- Backend build: 0 errors, 0 warnings.
- Full backend test suite: 28/28 passed.
- StudySession security tests: 8/8 passed.
- Authentication tests: 20/20 passed.
- EF Core pending model changes: none.
- No database migration was required.
- No database reset/drop/recreate occurred.
- P0.1B security audit: VERIFIED.
- Commit containing the completed implementation: `1457cafa328366af76c0fedafd8509f3de04e5d1`.

### Known legacy-account limitation

Accounts created before P0.1B with simulated `hashed_...` password values cannot authenticate through the new PBKDF2 verifier. They require normal account recreation or an administrative password reset. No insecure compatibility bypass should be introduced.

### Deployment configuration

The application no longer contains a code-level JWT secret fallback.

Production/staging environments must provide `Jwt:Secret` through environment/secret configuration rather than relying on the development value in `appsettings.json`.

Do not store or document the actual secret value in source control or PROJECT_STATUS.md.

## P0.2 — StudySession Full-Stack Integration & API Hardening

**Status: VERIFIED / COMPLETE**

Full-stack integration and API hardening for StudySessions was implemented across the ASP.NET Core backend and Flutter client.

### Backend Changes

- **Active Session Query:** Added `GetActiveStudySessionQuery` and `GetActiveStudySessionQueryHandler` returning the current user's active session (`!IsCompleted`), ordered by most recent start time.
- **Active Session Endpoint:** Exposed `GET /api/studysessions/active` on `StudySessionsController`. Returns HTTP 200 with the session if active, or HTTP 204 NoContent if no session is active.
- **Validation:** Added `StartStudySessionCommandValidator` with FluentValidation rules requiring a non-empty `Title` (maximum 100 characters).
- **Domain Exception:** Added `StudySessionNotFoundException` for missing or unauthorized session access.
- **Controller Error Semantics:**
  - `ValidationException` mapped to HTTP 400 Bad Request with `{ "error": "...", "errors": [...] }`.
  - `StudySessionNotFoundException` mapped to HTTP 404 Not Found with `{ "error": "..." }`.
  - Generic / unhandled exceptions mapped to HTTP 500 Internal Server Error with `{ "error": "An error occurred while processing your request." }` and logged via `ILogger`.
- **Session End Isolation:** `EndStudySessionCommandHandler` throws `StudySessionNotFoundException` when a session does not exist or does not belong to the authenticated user.

### Frontend Changes

- **API Path Correction:** Fixed leading slash issue in `StudySessionRepository` (`studysessions`, `studysessions/start`, `studysessions/$sessionId/end`, `studysessions/active`), ensuring requests properly compose with `NetworkClient.baseUrl`.
- **Active Session Recovery:** Updated `TimerScreen` to query `getActiveSession()` on load. If an active session exists, the elapsed time is calculated from the server UTC `startTime`, state is restored to running, and the client timer resumes seamlessly.
- **Semantic Error Handling:** Wrapped `StudySessionRepository` methods in defensive error parsing checking if the response data is a Map before extracting `{ "error": "..." }`. Displayed cleanly in `TimerScreen` via SnackBar without raw Dio dumps or runtime `NoSuchMethodError` on non-JSON payloads.
- **Async Safety:** Added `mounted` checks across asynchronous gaps in `TimerScreen`.
- **UI & Analysis Fixes:** Corrected Flutter theme CardTheme styling and test imports.

### Deferred Multiple-Active-Session Behavior

- `GetActiveStudySessionQuery` filters active sessions using the existing `!IsCompleted` semantics.
- If multiple active sessions exist for the same user, the query returns the most recently started one using `StartTime DESC`.
- P0.2 intentionally does NOT introduce a unique database constraint or server-side rejection of concurrent active sessions.
- This was deliberately deferred because it represents a product/business rule and would potentially require schema changes. Single-active-session rejection is not enforced in P0.2.

### Verification

- Backend build: 0 errors, 0 warnings.
- Backend tests: 43/43 passed (20 Auth, 8 StudySession handler/security, 15 P0.2 active query / validator / exception / controller tests).
- Flutter test suite: 3/3 passed (including model serialization/deserialization for active and completed sessions).
- Flutter analysis (`flutter analyze`): 20 issues total (0 errors, 1 warning, 19 infos/deprecations). These are pre-existing issues outside the P0.2 implementation files. P0.2 files introduced 0 analyzer issues.
- Working tree: Committed in commit `3871523` (`Study Session Full Integration`).

## P0.3 — Habits Full-Stack Integration & API Hardening

**Status: VERIFIED / COMPLETE**

Full-stack integration, validation, user ownership hardening, and automated testing for Habits were implemented across the ASP.NET Core backend and Flutter client.

### Backend Changes

- **User Identity & Security:**
  - Removed client-tamperable `UserId` properties from `CreateHabitCommand`, `ToggleHabitCompletionCommand`, and `GetHabitsQuery`.
  - All command and query handlers (`CreateHabitCommandHandler`, `ToggleHabitCompletionCommandHandler`, `GetHabitsQueryHandler`) now derive the authenticated `UserId` directly from `ICurrentUserService`.
  - Enforced strict tenant isolation: users can only view, create, and toggle their own habits.
- **Validation:** Added `CreateHabitCommandValidator` using FluentValidation (non-empty `Title`, maximum 100 characters). Automatically executed through MediatR `ValidationBehavior`.
- **Domain Exception:** Added `HabitNotFoundException` thrown when attempting to toggle a habit that does not exist or does not belong to the authenticated user.
- **Controller Error Semantics:**
  - In `HabitsController`:
    - `ValidationException` mapped to HTTP 400 Bad Request with `{ "error": "...", "errors": [...] }`.
    - `HabitNotFoundException` mapped to HTTP 404 Not Found with `{ "error": "..." }`.
    - Unexpected server exceptions logged via `ILogger<HabitsController>` and mapped to generic HTTP 500 Internal Server Error without leaking internal exception messages.
  - Success responses preserved: `GET /api/habits` (200 with `List<HabitDto>`), `POST /api/habits` (200 with `{ Id = ... }`), `POST /api/habits/{id}/toggle` (200 with `{ IsCompletedToday = ... }`).
- **Streak & Completion Behavior:** Preserved exact existing streak calculation and completion algorithm.

### Frontend Changes

- **API Path Correction:** Fixed leading slash bug in `HabitRepository` (`'habits'`, `'habits/$habitId/toggle'`), ensuring requests properly compose with `NetworkClient.baseUrl` (`https://mot-dalx.onrender.com/api/`).
- **Defensive Error Handling:** Added safe error parsing in `HabitRepository` verifying `data is Map && data['error'] != null` before indexing into response data, preventing secondary `NoSuchMethodError` on non-JSON server/proxy responses.
- **UI Preservation:** Habit dashboard UI and presentation logic preserved without unrelated modifications.

### Verification

- Backend build: 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- Backend tests: 69/69 passed (20 Auth, 23 StudySessions, 26 new Habit handler, validator, streak, and controller tests).
- Flutter test suite: 6/6 passed (including 3 new Habit model deserialization tests in `habit_model_test.dart`).
- Flutter analysis (`flutter analyze`): 20 issues total (0 errors, 1 warning, 19 infos/deprecations). All 20 are pre-existing diagnostics in other modules. P0.3 files introduced 0 analyzer issues.
- Database & Migrations: Verified via `dotnet ef migrations has-pending-model-changes` — no model changes, no database schema modification or migration required.
- Working tree: Uncommitted changes preserved for user review. No commits or pushes performed.

### Deferred Work

- Tasks full-stack hardening (validation, error semantics, tests).
- User Profile screen integration and `/api/auth/me` endpoint.
- Live Classroom backend integration.
- Single-active-study-session database constraint.
- Global Dio 401 interceptor.
- Unrelated pre-existing analyzer diagnostics in other modules.

## P0.4 — Daily Tasks Full-Stack Integration & API Hardening

**Status: VERIFIED / CLOSED**

Full-stack integration, validation, user ownership hardening, and automated testing for Daily Tasks were implemented across the ASP.NET Core backend and Flutter client.

### Backend Changes

- **User Identity & Security:**
  - Removed caller-supplied `UserId` properties from `CreateTaskCommand`, `ToggleTaskCommand`, and `GetTasksQuery`.
  - All command and query handlers (`CreateTaskCommandHandler`, `ToggleTaskCommandHandler`, `GetTasksQueryHandler`) now resolve authenticated `UserId` directly through `ICurrentUserService`.
  - Enforced strict tenant isolation: users can only view, create, and toggle their own tasks.
- **Validation:** Added `CreateTaskCommandValidator` using FluentValidation (non-empty, non-whitespace `Title`, maximum 100 characters). Automatically executed through MediatR `ValidationBehavior`.
- **Domain Exception:** Added `TaskNotFoundException` in `MOT.Application.Common.Exceptions`, thrown when attempting to toggle a task that does not exist or belongs to another user.
- **Controller Error Semantics:**
  - In `TasksController`:
    - Bound `[FromBody] CreateTaskCommand command` directly, removing redundant `CreateTaskRequest`.
    - `ValidationException` mapped to HTTP 400 Bad Request with `{ "error": "..." }`.
    - `TaskNotFoundException` mapped to HTTP 404 Not Found with `{ "error": "..." }`.
    - Unexpected server exceptions logged via `ILogger<TasksController>` and mapped to generic HTTP 500 Internal Server Error without leaking internal exception messages.
  - Success responses preserved: `GET /api/tasks` (200 with `List<DailyTaskDto>`), `POST /api/tasks` (201 Created with `{ id = ... }`), `PATCH /api/tasks/{id}/toggle` (200 OK with `{ isCompleted = ... }`).

### Frontend Changes

- **Defensive Error Handling:** Hardened `TaskRepository` with safe error extraction verifying `data is Map && data['error'] != null` before indexing into response data, preventing secondary `NoSuchMethodError` on non-JSON server/proxy responses.
- **Token Handling:** Removed redundant manual `SharedPreferences` JWT reads in `TaskRepository`, leveraging the `NetworkClient` authorization interceptor.
- **Model Resilience:** Hardened `DailyTask.fromJson` with safe fallback defaults for `id`, `title`, `description`, `isCompleted`, and `createdAt` against missing or null values.
- **UI Async Safety:** Safely guarded `ScaffoldMessenger` in `task_dashboard_screen.dart` across async gaps, reducing analyzer diagnostics from 20 to 18.

### Verification

- Backend build: 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- Backend tests: 101/101 passed (20 Auth, 23 StudySessions, 26 Habits, 32 new Task creation, validation, listing, toggle, and controller status code tests in `TaskHandlerTests.cs`).
- Flutter test suite: 9/9 passed (3 StudySessions, 3 Habits, 3 new Task model deserialization and null-safety tests in `task_model_test.dart`).
- Flutter analysis (`flutter analyze`): 18 issues found (0 errors, 1 pre-existing warning in `profile_screen.dart:4:8`, 17 pre-existing infos in `theme.dart`, `auth`, `classroom`, `habits`, `profile`, `custom_text_field`). 0 diagnostics in `features/tasks`.
- Database & Migrations: Verified via `dotnet ef migrations has-pending-model-changes` — 0 pending model changes, 0 migrations created or applied.
- Working tree: Clean baseline at `290deb8`, uncommitted changes preserved for user review. No commits or pushes performed.

### Deferred Work

- Task due dates, reminders, recurring tasks, and priorities.
- Task edit/update and deletion endpoints/UI.
- Task history/filtering.
- User Profile screen integration and `/api/auth/me` endpoint.
- Live Classroom backend integration.
- Single-active-study-session database constraint.
- Global Dio 401 interceptor.
- Pre-existing Flutter analyzer diagnostics outside Tasks.

## P0.5 — User Profile & Auth Integration Full-Stack Hardening

**Status: IMPLEMENTATION COMPLETE — READY FOR INDEPENDENT AUDIT**

Full-stack integration, user identity resolution, error semantics, UI integration, and automated testing for the User Profile (`/api/auth/me`) were implemented across the ASP.NET Core backend and Flutter client.

### Backend Changes

- **Endpoint:** Added `[HttpGet("me")]` endpoint to `AuthController` with `[Authorize]` attribute.
- **Query & Handler:**
  - Added `GetCurrentUserQuery : IRequest<UserProfileDto>`.
  - Added `GetCurrentUserQueryHandler` in `MOT.Application.Users.Queries`.
  - User identity is resolved strictly from `ICurrentUserService.UserId`. Client-supplied user IDs are not accepted or processed.
- **DTO & Exceptions:**
  - Added `UserProfileDto` containing `Id`, `Email`, `DisplayName`, `CreatedAt`, `CurrentStreak`, `Level`, and `XP`.
  - Added `UserNotFoundException` in `MOT.Application.Common.Exceptions`.
- **Controller Error Semantics:**
  - HTTP 200 OK returning `UserProfileDto` upon successful profile resolution.
  - HTTP 401 Unauthorized enforced by ASP.NET Core authentication middleware when JWT token is missing, expired, or invalid.
  - HTTP 404 Not Found returning `{ "error": "User not found" }` when authenticated user record does not exist in database (`UserNotFoundException`).
  - HTTP 500 Internal Server Error returning `{ "error": "An error occurred while fetching user profile" }` upon unexpected errors, with full exception logged via `ILogger<AuthController>`.
- **Database & Persistence:**
  - Leveraged existing `IAuthRepository.GetUserByIdAsync` and existing `Users` table schema (`Id`, `Email`, `DisplayName`, `CreatedAt`, `XP`, `Level`, `CurrentStreak`).
  - No database migration or schema modification required (0 pending model changes).

### Frontend Changes

- **Data Model:** Added `UserProfile` model in `lib/features/profile/data/models/user_profile.dart` with defensive `fromJson` parser providing safe fallback defaults for `id`, `email`, `displayName`, `createdAt`, `currentStreak`, `level`, and `xp`.
- **Repository:**
  - Added `getProfile()` to `AuthRepository` in `lib/features/auth/data/auth_repository.dart` calling `auth/me` with `NetworkClient`.
  - Implemented defensive error extraction checking `data is Map && data['error'] != null` to prevent `NoSuchMethodError` on non-JSON server/gateway responses.
- **Riverpod State Management:**
  - Added `userProfileProvider = FutureProvider<UserProfile>` in `lib/features/auth/presentation/providers/auth_provider.dart`.
- **Profile UI Integration:**
  - Updated `ProfileScreen` in `lib/features/profile/presentation/screens/profile_screen.dart` to consume `userProfileProvider`.
  - Displays authenticated user's real `displayName`, `email`, `currentStreak`, `level`, and `xp`.
  - Implemented loading state with `CircularProgressIndicator` and error state with retry button.
  - Preserved static `'--'` with unit `'Hours'` for `Total Focus` (documented as deferred work).
  - Cleaned up unused import (`auth_repository.dart`) and added `const` constructor optimizations.
- **Navigation Integration:**
  - Added Profile tab as 5th item in `HomeScreen` bottom navigation bar (`lib/core/presentation/screens/home_screen.dart`).

### Verification

- Backend build: 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- Backend tests: 112/112 passed (20 Auth, 23 StudySessions, 26 Habits, 32 Tasks, 11 new User Profile tests in `UserProfileHandlerTests.cs`).
- Flutter test suite: 12/12 passed (3 StudySessions, 3 Habits, 3 Tasks, 3 new User Profile deserialization tests in `user_profile_model_test.dart`).
- Flutter analysis (`flutter analyze`): 11 issues found (0 errors, 0 warnings, 11 pre-existing infos). Unused import warning in `profile_screen.dart` resolved. 0 analyzer issues in P0.5 code.
- Database & Migrations: Verified via `dotnet ef migrations has-pending-model-changes` — 0 pending model changes, 0 migrations created or applied.
- Working tree: Clean baseline at `ef7bb63`, uncommitted changes preserved for user review. No commits or pushes performed.

### Deferred Work

- Total Focus hours calculation / aggregation endpoint.
- User profile editing / DisplayName updating.
- Avatar upload / storage.
- Password change / account settings.
- Live Classroom backend integration.
- Single-active-study-session database constraint.
- Global Dio 401 interceptor.
- Pre-existing Flutter analyzer infos in other modules.
