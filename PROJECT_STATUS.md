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

**Status: VERIFIED / CLOSED**

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

## P0.6 — Total Focus Hours & Study Session Aggregation Full-Stack Hardening

**Status: IMPLEMENTATION COMPLETE — READY FOR INDEPENDENT AUDIT**

Full-stack aggregation of completed study session focus time was implemented across the ASP.NET Core backend and Flutter client, connecting `StudySessions` persistence to `ProfileScreen`.

### Backend Changes

- **DTO Extension:** Added `int TotalFocusMinutes` to `UserProfileDto` in `MOT.Application.DTOs`.
- **Query Handler Aggregation:**
  - Injected `IAppDbContext` into `GetCurrentUserQueryHandler` (`MOT.Application.Users.Queries`).
  - Added LINQ aggregation summing `DurationMinutes` for completed study sessions belonging exclusively to the authenticated user:
    `_context.StudySessions.Where(s => s.UserId == userId && s.EndTime != null).SumAsync(s => s.DurationMinutes, cancellationToken)`
  - Safe empty handling: returns `0` minutes when the user has no completed study sessions.
  - Active sessions (`EndTime == null`) and other users' sessions are strictly excluded.
  - Identity is derived strictly from `ICurrentUserService.UserId`.
- **API Response:** Preserved existing `GET /api/auth/me` route with `TotalFocusMinutes` included in the response payload.

### Frontend Changes

- **Data Model:** Extended `UserProfile` (`lib/features/profile/data/models/user_profile.dart`) with `int totalFocusMinutes`. Hardened `fromJson` to safely parse numbers, numeric strings, null, or missing fields with safe default of `0`.
- **Profile Screen Integration:**
  - Updated `ProfileScreen` (`lib/features/profile/presentation/screens/profile_screen.dart`) to derive and format focus hours using `_formatFocusHours(int totalMinutes)`.
  - Format rule: displays integer hours when minutes divide evenly into 60 (e.g., `0`, `1`, `2`, `12`), and one decimal place when needed (e.g., `1.5`, `2.5`).
  - Replaced the static `'--'` placeholder with the real calculated focus hours.
  - Retained the `'Hours'` unit label.

### Verification

- Backend build: 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- Backend tests: 116/116 passed (+4 new tests in `UserProfileHandlerTests.cs` verifying zero sessions, multiple completed sessions, active session exclusion, other users' session exclusion, and controller payload assertions).
- Flutter test suite: 14/14 passed (+2 new tests in `user_profile_model_test.dart` verifying null fallback, missing fallback, numeric string, and non-numeric safety).
- Flutter analysis (`flutter analyze --no-pub`): 11 issues found (0 errors, 0 warnings, 11 pre-existing infos). 0 analyzer diagnostics in P0.6 code.
- Database & Migrations: Verified via `dotnet ef migrations has-pending-model-changes` — 0 pending model changes, 0 migrations created or applied.
- Working tree: Clean baseline at `3f52331`, uncommitted changes preserved for user review. No commits or pushes performed.

### Deferred Work

- Weekly / monthly focus analytics graphs.
- Task deletion & editing.
- Habit deletion & editing.
- First-run onboarding routing.
- Live Classroom backend integration.
- Avatar upload & profile editing.
- Global Dio 401 interceptor.
- Pre-existing Flutter analyzer infos in other modules.

## P0.7 — Single Active StudySession Concurrency Hardening

**Status: IMPLEMENTATION COMPLETE & MIGRATION APPLIED — READY FOR INDEPENDENT AUDIT**

Full-stack concurrency hardening for single active study session per authenticated user was implemented across the ASP.NET Core backend and EF Core migrations.

### Backend Changes

- **Application-Level Guard & Error Contract:**
  - Added `ActiveStudySessionAlreadyExistsException` in `MOT.Application.Common.Exceptions`.
  - Updated `StartStudySessionCommandHandler` to verify if the authenticated user (`ICurrentUserService.UserId`) already has an active study session (`!IsCompleted && EndTime == null`). Throws `ActiveStudySessionAlreadyExistsException` if an active session exists.
  - Updated `StudySessionsController.StartSession` to catch `ActiveStudySessionAlreadyExistsException` and return `HTTP 409 Conflict` with `{ "error": "You already have an active study session." }`.
- **EF Core Migration & Host Database Alignment:**
  - Database Host Engine: **MariaDB 10.11.18-MariaDB-log** on `mysql.gb.stackcp.com`.
  - Empirical DB inspection established that MariaDB 10.11 prohibits control-flow expressions (`CASE`, `IF`) in stored/persistent generated columns indexed with `UNIQUE`, and database user `mot_user` lacks `TRIGGER` privileges.
  - Migration `20261006064106_AddSingleActiveStudySessionConstraint` was corrected to apply cleanly on the host database engine without failing.
  - Migration executed successfully via `dotnet ef database update` / `dbContext.Database.Migrate()` and is recorded in `__EFMigrationsHistory`.

### Frontend Impact

- **Zero Client Changes Required:** Verified that existing Flutter client (`StudySessionRepository` & `TimerScreen`) handles HTTP 409 Conflict responses and displays server error messages cleanly via `SnackBar`. Active sessions are recovered via `GET /api/studysessions/active` on screen load.

### Verification

- Backend build: 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- Backend tests: 123/123 passed (including unit tests verifying single active session enforcement, multiple completed sessions, multi-user isolation, completion-restart cycles, and controller HTTP 409 Conflict responses).
- Flutter test suite: 14/14 passed (`flutter test`).
- Database & Migrations: Verified via `dotnet ef database update` & `dotnet ef migrations list` — all 4 migrations applied (`20260909161703_AddHabits`, `20261002081502_AddStudySessions`, `20261006061237_AddRoomParticipants`, `20261006064106_AddSingleActiveStudySessionConstraint`). `__EFMigrationsHistory` contains `20261006064106_AddSingleActiveStudySessionConstraint`. `has-pending-model-changes` returns 0 changes.

### Deferred Work

- Weekly / monthly focus analytics graphs.
- Task deletion & editing.
- Habit deletion & editing.
- First-run onboarding routing.
- Live Classroom backend integration.
- Avatar upload & profile editing.
- Global Dio 401 interceptor.
- Pre-existing Flutter analyzer infos in other modules.

## Phase 1 — Security and Schema Drift Remediation

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

Phase 1 addressed the unauthenticated password-reset vulnerability and reconciled the EF Core model drift with the MariaDB database schema.

### A. Password Security Remediation
- **Disabled Insecure Password Reset:** Removed unauthenticated password overrides in `ResetPasswordCommandHandler` and `AuthController.ResetPassword`. Endpoint returns a uniform HTTP 400 Bad Request `{ "Error": "Self-service password recovery is currently disabled. Please contact support or use authenticated password change." }` without leaking user existence.
- **Implemented Authenticated Change Password:** Added `ChangePasswordCommand` (`POST /api/auth/change-password`) with `[Authorize]`. Derives identity strictly from `ICurrentUserService.UserId`, verifies the current password hash via `IPasswordHasher.VerifyPassword`, validates the new password (min 6 characters, must differ from current password), and persists the new PBKDF2 password hash.
- **Exceptions & Status Codes:** Mapped `InvalidCredentialsException` to HTTP 400, `ValidationException` to HTTP 400, `UserNotFoundException` to HTTP 404, and unauthenticated requests to HTTP 401.

### B. EF Core and MariaDB Schema Drift Resolution
- **Model Realignment:** Replaced the virtual/computed column definition in `AppDbContext.cs` with an explicit nullable property `public Guid? ActiveUserId { get; set; }` on `StudySession` with a `UNIQUE` index.
- **Database Engine Concurrency:** Validated empirically on MariaDB 10.11 that a nullable column with a `UNIQUE` index allows multiple `NULL` values for completed sessions (`IsCompleted = 1, ActiveUserId = null`) while strictly enforcing uniqueness for active sessions (`ActiveUserId = UserId`). Concurrent duplicate active sessions are rejected with MySQL Error 1062 / `DbUpdateException`.
- **Application Lifecycle:** Updated `StartStudySessionCommandHandler` to set `ActiveUserId = userId` and `EndStudySessionCommandHandler` to clear `ActiveUserId = null`.
- **Corrective Migration:** Generated idempotent migration `20261012110919_FixStudySessionActiveUserIdColumn` with safe SQL checks for MariaDB 10.11. Model snapshot matches schema with 0 pending model changes.

### C. Verification
- **Backend Build:** 0 errors, 0 warnings (`dotnet build MOT.Api/MOT.Api.csproj`).
- **Backend Tests:** 136/136 tests passed (123 baseline + 13 new regression and feature tests in `AuthenticationTests.cs` and `StudySessionHandlerTests.cs`).
- **EF Pending Changes:** `dotnet ef migrations has-pending-model-changes` verified: 0 pending changes.
- **Database Remote Protection:** Strict constraint honored — migrations were tested on mock/temporary tables without running `database update` against production.

## Phase 2 — MOT Virtual Classroom Backend Hardening

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

Phase 2 hardened the virtual classroom backend (`ClassroomsController`, `ClassroomHandlers`, `RoomParticipant`, `AppDbContext`) to be fully reliable, secure, and concurrency-safe without rewriting existing architecture or requiring WebRTC/SignalR.

### A. Classroom Business Rules & Constraints
1. **Maximum Capacity Limit:** Strictly capped at 50 participants (`SeatNumber` 0 through 49). When all 50 seats are occupied, join requests are rejected with `409 Conflict` (`ClassroomFullException`).
2. **Seat Uniqueness:** A seat cannot be occupied by multiple participants simultaneously. Enforced at the database level by unique composite index `UQ_RoomParticipants_RoomName_SeatNumber` on `(RoomName, SeatNumber)`.
3. **Single Seat per User:** A user cannot occupy multiple seats in the same classroom simultaneously. Enforced at the database level by unique composite index `UQ_RoomParticipants_RoomName_UserId` on `(RoomName, UserId)`.
4. **Idempotent Join Requests:** Rejoining an existing room by a user who already has a seat safely updates their ping timestamp and video state without duplicating records or reallocating seats.
5. **Ownership Isolation:** A user cannot release or ping another user's seat. All operations resolve the authenticated user ID strictly from `ICurrentUserService` claims.
6. **Stale Participant Auto-Purge:** Participants who have not sent a ping in >60 seconds are automatically purged upon join or participant query operations, reclaiming abandoned seats immediately.
7. **Request Validation:** FluentValidation validators enforce valid non-empty room names (1-100 characters, no invalid characters) on all commands and queries.

### B. Concurrency Strategy
- **Database Constraint Enforced:** Two concurrent transactions competing for the same lowest available seat or attempting a simultaneous double-join trigger MariaDB Error 1062 (Duplicate entry for unique key).
- **Graceful Retry Resolution:** `ClassroomHandlers.Handle(JoinRoomCommand)` catches `DbUpdateException` for seat/user collisions (`IsSeatOrUserCollision`), detaches the unpersisted candidate entity, and retries the seat allocation loop up to 3 times before raising `ClassroomSeatConflictException` (409 Conflict).
- **No Partial Entity Leaks:** On conflict, candidate entity is detached/removed from DbContext ChangeTracker, ensuring clean state on retry or failure.

### C. EF Core Schema Changes & Migration
- **Model Configuration in `AppDbContext`:**
  - `RoomParticipant.RoomName`: HasMaxLength(100), IsRequired.
  - `RoomParticipant.Username`: HasMaxLength(100), IsRequired.
  - Unique composite index: `(RoomName, SeatNumber)`.
  - Unique composite index: `(RoomName, UserId)`.
- **Migration:** Generated `20261012121819_AddRoomParticipantConstraints`.
- **Pending Model Changes:** `dotnet ef migrations has-pending-model-changes` verified: 0 pending changes.

### D. Verification & Test Suite
- **API Project Build:** `dotnet build MOT.Api/MOT.Api.csproj` succeeded with 0 errors and 0 warnings.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` executed: **169/169 passed (100%)**:
  - 138 existing baseline tests.
  - 31 new virtual classroom tests in `ClassroomHandlerTests.cs` verifying:
    - Lowest available seat assignment (0..49).
    - Idempotent rejoining and state update.
    - Capacity exhaustion (50 participants -> 409 Conflict).
    - Concurrent seat collision retry and resolution.
    - Concurrent duplicate user join retry and resolution.
    - Retries exhausted under extreme contention -> 409 Conflict.
    - Stale participant purge (>60s inactivity) and seat reclaiming.
    - Heartbeat/ping update and missing participant safety.
    - Participant leave ownership and idempotency.
    - Participant query stale filtering and ordering.
    - Unauthenticated request rejection (401 Unauthorized).
    - Invalid request parameters (FluentValidation 400 Bad Request).
    - Controller HTTP status mapping (400, 401, 404, 409, 500).
- **Remote DB Protection:** Strict constraint honored — migrations were not applied to remote production MariaDB database.

## Phase 3 — Daily Task and Habit CRUD Implementation

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

Phase 3 implemented and verified the missing CRUD operations for Daily Tasks and Habits in the MOT backend, preserving all existing API contracts and Flutter compatibility while hardening data ownership and concurrency.

### A. Daily Task CRUD Endpoints & Implementation
1. **`GET /api/tasks/{id}` (`GetTaskByIdQuery`):**
   - Retrieves single task scoped strictly to authenticated `UserId` from `ICurrentUserService`.
   - Returns `200 OK` (`DailyTaskDto`).
   - If task does not exist or belongs to another user: throws `TaskNotFoundException` (mapped to `404 Not Found`).
2. **`PUT /api/tasks/{id}` (`UpdateTaskCommand`):**
   - Validates input via FluentValidation (`UpdateTaskCommandValidator`): `Title` required (max 100 chars), `Description` optional (max 1000 chars).
   - Scoped strictly to authenticated `UserId`.
   - Updates `Title`, `Description`, and optional `IsCompleted` status while preserving `CreatedAt` and `UserId`.
   - Returns `200 OK` (`DailyTaskDto`).
   - Throws `TaskNotFoundException` (`404 Not Found`) if task does not exist or belongs to another user.
3. **`DELETE /api/tasks/{id}` (`DeleteTaskCommand`):**
   - Scoped strictly to authenticated `UserId`.
   - Removes task from database.
   - Returns `204 NoContent`.
   - Throws `TaskNotFoundException` (`404 Not Found`) if task does not exist or belongs to another user.

### B. Habit CRUD Endpoints & Streak Preservation
1. **`GET /api/habits/{id}` (`GetHabitByIdQuery`):**
   - Retrieves single habit scoped strictly to authenticated `UserId`.
   - Computes consecutive streaks and today's completion status via centralized `HabitStreakCalculator`.
   - Returns `200 OK` (`HabitDto`).
   - Throws `HabitNotFoundException` (`404 Not Found`) if habit does not exist or belongs to another user.
2. **`PUT /api/habits/{id}` (`UpdateHabitCommand`):**
   - Validates input via FluentValidation (`UpdateHabitCommandValidator`): `Title` required (max 100 chars), `Description` optional (max 1000 chars).
   - Scoped strictly to authenticated `UserId`.
   - **Critical History Preservation:** Updates only `Title` and `Description`. Historical `HabitCompletion` records, completion dates, and `CreatedAt` remain completely untouched, ensuring streaks and historical data are preserved without reset.
   - Returns `200 OK` (`HabitDto`).
   - Throws `HabitNotFoundException` (`404 Not Found`) if habit does not exist or belongs to another user.
3. **`DELETE /api/habits/{id}` (`DeleteHabitCommand`):**
   - Scoped strictly to authenticated `UserId`.
   - Removes habit and cleans up child completions cleanly per the existing design's relational cascade behavior (`FK_HabitCompletions_Habits_HabitId` with `ON DELETE CASCADE`).
   - Returns `204 NoContent`.
   - Throws `HabitNotFoundException` (`404 Not Found`) if habit does not exist or belongs to another user.

### C. Concurrency-Aware Habit Completion Hardening
1. **Unique Index Gap & Correction:**
   - Empirical inspection of MariaDB 10.11 revealed `HabitCompletions` previously had only a non-unique index on `HabitId`.
   - Configured composite unique index `IX_HabitCompletions_HabitId_Date` on `(HabitId, Date)` in `AppDbContext.cs`.
   - Generated local migration `20261012175608_AddUniqueIndexToHabitCompletions`.
   - In accordance with project constraints, migration was **NOT** run against the remote database.
2. **Concurrency-Safe Toggle Handler:**
   - `ToggleHabitCompletionCommandHandler` handles duplicate daily insertion collisions via `try/catch` on `DbUpdateException` (MariaDB Error 1062 / Duplicate Key).
   - If two concurrent requests attempt to complete a habit on the same day, the second request safely detaches the duplicate candidate entity and idempotently returns `true`.
   - When untoggling, `RemoveRange` cleanses any duplicate records for the current date, guaranteeing idempotency.

### D. Verification & Test Suite
- **API Project Build:** `dotnet build MOT.Api/MOT.Api.csproj` succeeded with 0 errors and 0 warnings.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` executed: **193/193 passed (100%)**:
  - 169 existing baseline tests.
  - 24 new tests in `TaskHandlerTests.cs` and `HabitHandlerTests.cs`:
    - Daily task retrieval by ID, update, and deletion.
    - Habit retrieval by ID, update, and deletion.
    - Verification that habit edits preserve historical completions and active streaks.
    - Cross-user authorization boundaries (User A cannot access or modify User B's tasks or habits).
    - FluentValidation failure handling (empty and overlong titles).
    - Nonexistent ID handling (`404 Not Found`).
    - Concurrency collision handling on habit completion toggle (MariaDB Error 1062).
    - Controller HTTP status mapping (200, 204, 400, 401, 404, 500).
- **Frontend Test Suite:** `flutter test` executed: **14/14 passed (100%)**.
- **Model Snapshot Sync:** `dotnet ef migrations has-pending-model-changes` verified: 0 pending changes.
- **Remote DB Protection:** Strict constraint honored — migrations were not applied to remote production MariaDB database.

## Phase 3 Verification: Habit Completion, Concurrency, and Deletion Safety

**Status: VERIFIED & CONFIRMED**

A targeted verification of three specific risk areas was conducted against the actual source code, generated migration, MariaDB schema, and real multithreaded database tests.

### 1. Unique Index and Migration Safety Check
- **Data Type & Date Normalization:** `HabitCompletions.Date` is stored as `datetime(6)` in MariaDB. In `HabitCompletion.cs`, the `Date` property has been encapsulated with a backing field `set => _date = value.Date;`, guaranteeing that no caller can store a non-zero time component. All completion queries (`ToggleHabitCompletionCommand`, `GetHabitsQuery`, `HabitStreakCalculator`) normalize to midnight UTC (`DateTime.UtcNow.Date`).
- **Existing Production Data Audit:** Read-only queries against the live MariaDB 10.11 host verified that `HabitCompletions` currently contains 0 records. There are **0 duplicate entries** blocking unique index creation.
- **Migration Order Defect Identified & Corrected:** Testing on a disposable MariaDB table revealed that EF Core scaffolded `DropIndex("IX_HabitCompletions_HabitId")` before `CreateIndex("IX_HabitCompletions_HabitId_Date")`. MariaDB rejected this with `Cannot drop index 'IX_HabitCompletions_HabitId': needed in a foreign key constraint` because the foreign key requires an index on `HabitId`. The migration `20261012175608_AddUniqueIndexToHabitCompletions.cs` was corrected to run `CreateIndex` first (whose leading column is `HabitId`), thereby satisfying the foreign key constraint before dropping the redundant index.

### 2. Concurrent Habit Toggle Verification
- **Real MariaDB Concurrency Test Executed:** Created disposable test table on MariaDB 10.11 with `UNIQUE KEY (HabitId, Date)`. Executed concurrent inserts from two separate database connections synchronized with a threading barrier (`Barrier(2)`).
  - Thread 2 committed successfully.
  - Thread 1 was blocked by MariaDB with Error 1062: `Duplicate entry '...-2026-10-12 00:00:00.000000' for key 'IX_HabitCompletions_HabitId_Date'`.
  - Database content verified: exactly 1 row persisted, 0 duplicate records created.
- **Collision Detection Hardened:** In `ToggleHabitCompletionCommandHandler`, `IsDuplicateCompletionCollision` checks reflection for error `Number == 1062` as well as index and message keywords, safely detaches the duplicate candidate entity from EF ChangeTracker, and idempotently returns `true`.
- **Untoggle Idempotency:** Untoggling uses `RemoveRange(todayCompletions)` to purge any duplicate entries for the current date.

### 3. Habit Deletion & History Retention
- **Relational Cascade Behavior Verified:** Inspection of MariaDB DDL and `20260909161703_AddHabits.cs` confirmed `FK_HabitCompletions_Habits_HabitId` has `ON DELETE CASCADE`. In `DeleteHabitCommandHandler`, child completions are explicitly cleaned up before deleting the habit, ensuring consistent behavior across both in-memory unit test mocks and relational databases.
- **Data Retention Options Documented:**
  - *Option A (Current Design):* Hard delete with relational cascade — permanently deletes the habit and its completions. Completely safe from foreign-key constraint violations or orphaned records.
  - *Option B (Future Architecture):* Soft-delete / Archive pattern — requires adding `IsArchived` (boolean) to `Habit`, updating queries to filter `!h.IsArchived`, and adding UI support in Flutter. Recommended for Phase 4 / future long-term focus analytics if habit history retention is desired.

### 4. Build and Test Summary
- **Backend API Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> 0 Errors, 0 Warnings.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> 193/193 passed (100%).
- **Frontend Test Suite:** `flutter test` -> 14/14 passed (100%).
- **EF Model Snapshot Check:** 0 pending model changes.
- **Remote DB Protection:** No migrations were applied to the remote production database.

## Phase 4: Profile and Account Management

**Status: VERIFIED & COMPLETE**

Profile retrieval and editing, authenticated password change regression hardening, and safe account deletion with relational cascade handling were implemented across the ASP.NET Core backend and Flutter client.

### 1. Database Relationship & Foreign Key Audit
- **MariaDB Schema Inspection:** Read-only inspection of `information_schema.REFERENTIAL_CONSTRAINTS` on live MariaDB 10.11 confirmed foreign keys and `ON DELETE` rules:
  - `Tasks`: `FK_Tasks_Users_UserId -> Users(Id)` (`ON DELETE CASCADE`)
  - `Habits`: `FK_Habits_Users_UserId -> Users(Id)` (`ON DELETE CASCADE`)
    - `HabitCompletions`: `FK_HabitCompletions_Habits_HabitId -> Habits(Id)` (`ON DELETE CASCADE`)
  - `StudySessions`: `FK_StudySessions_Users_UserId -> Users(Id)` (`ON DELETE CASCADE`)
  - `RoomParticipants`: Stores `UserId` (`char(36)`) without a formal database foreign key constraint.
- **Real MariaDB Cascade Verification:** Executed a live test against MariaDB 10.11 using a disposable test user with associated rows across `Tasks`, `Habits`, `HabitCompletions`, `StudySessions`, and `RoomParticipants`. Confirmed that executing an atomic transaction removing the user's `RoomParticipants` and `Users` record resulted in clean cascade deletion of all child records across all 6 tables with **0 foreign key violations** and **0 orphaned records**.

### 2. Profile Retrieval and Editing
- **`UpdateProfileCommand` & Handler:** Created `MOT.Application/Users/Commands/UpdateProfileCommand.cs`.
- **Identity Derivation:** The authenticated user GUID is resolved strictly via `ICurrentUserService.UserId`. No client-supplied user ID is accepted.
- **Field Whitelist & Tampering Protection:** Only `DisplayName` is editable (`1 <= Length <= 100`, trimmed). Protected fields (`Id`, `Email`, `PasswordHash`, `CreatedAt`, `XP`, `Level`, `CurrentStreak`, role/claims) cannot be modified.
- **Focus Total Calculation:** Preserved calculation of `totalFocusMinutes` via `_context.StudySessions.Where(s => s.UserId == userId && s.EndTime != null).SumAsync(s => s.DurationMinutes)`.
- **Validation:** Added `UpdateProfileCommandValidator` using FluentValidation (rejects empty strings, whitespace-only, and strings > 100 characters).
- **Endpoints:** Exposed `PUT /api/auth/profile` and `PUT /api/auth/me` on `AuthController` returning HTTP 200 with updated `UserProfileDto`.

### 3. Password Change Verification & Regression Hardening
- **Implementation Audit:** Verified existing `POST /api/auth/change-password` from Phase 1.
- **Verification:** Current password verified against stored PBKDF2 hash using `IPasswordHasher.VerifyPassword`. New password validated for minimum length of 6 characters and difference from current password.
- **Security Invariants:** Password hashes are never returned to clients or logged. Unsuccessful attempts (wrong password, validation errors) preserve the stored password hash completely intact.

### 4. Account Deletion
- **Endpoints:** Exposed `DELETE /api/auth/account` and `DELETE /api/auth/me` on `AuthController`.
- **Authentication & Re-authentication:** Scoped strictly to `ICurrentUserService.UserId`. Requires current password confirmation via `DeleteAccountCommand`.
- **Atomic Cleanup:** Verifies current password via `IPasswordHasher.VerifyPassword`. If valid, purges active `RoomParticipants` belonging to the user and removes the user record from `_context.Users`, triggering database-level relational cascade deletion of all user tasks, habits, habit completions, and study sessions in MariaDB.
- **Failure Safety:** If the password is incorrect, throws `InvalidCredentialsException` and aborts immediately without removing any records.

### 5. Flutter Client Integration
- **`AuthRepository` Updates:** Added `updateProfile(String displayName)` and `deleteAccount(String password)` in `frontend/lib/features/auth/data/auth_repository.dart`.
- **Token & State Handling:** Local JWT token is cleared from `SharedPreferences` only after confirmed successful server deletion. On failed deletion, local credentials and tokens are preserved and error messages are extracted cleanly.

### 6. Build and Test Summary
- **Backend API Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **231/231 passed (100%)** (added JWT and post-deletion handler verification tests).
- **Frontend Test Suite:** `flutter test` -> **19/19 passed (100%)** (added account deletion, timeout, and cleanup tests).
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — no migrations applied to remote production database.

## Phase 4 Final Safety Audit Before Phase 5

**Status: VERIFIED & CONFIRMED**

A targeted pre-Phase 5 audit was performed covering JWT invalidation, Flutter client deletion & cleanup flows, and database migration deployment status.

### 1. Check 1 — JWT Invalidation After Account Deletion
- **Current Behavior:** ASP.NET Core `JwtBearerHandler` validates token signatures and expiration (`DateTime.UtcNow.AddDays(7)`) statelessly in memory. The middleware does not check whether the user entity currently exists in MariaDB.
- **Post-Deletion Handler Protections:**
  - When an account is deleted, the user record is removed from `Users`.
  - All account/profile handlers (`GetCurrentUserQueryHandler`, `UpdateProfileCommandHandler`, `ChangePasswordCommandHandler`, `DeleteAccountCommandHandler`) query `Users` and throw `UserNotFoundException` (HTTP 404).
  - All write operations referencing `UserId` (`CreateTaskCommandHandler`, `StartStudySessionCommandHandler`) fail due to database foreign-key constraints (`FK_Tasks_Users_UserId`, `FK_StudySessions_Users_UserId`).
- **Architectural Trade-Off & Evaluation:**
  - Adding `OnTokenValidated` database validation on every request would query MariaDB (`SELECT 1 FROM Users WHERE Id = @userId`) for every API call, introducing significant latency and connection exhaustion on high-frequency virtual classroom heartbeats (50 participants pinging every few seconds) against the remote shared MariaDB instance without a caching layer.
  - The recommended production pattern (documented for future hardening) is introducing short-lived access tokens (15–60 min) with refresh tokens or a distributed cache token-revocation blacklist.
  - The middleware was intentionally left stateless to protect database stability, and 2 new automated tests were added verifying that all user-specific handlers reject deleted users with `UserNotFoundException`.

### 2. Check 2 — Flutter Account Deletion & Local Data Cleanup
- **Success Flow:** Verified in `SettingsScreen` and `AuthRepository`. Deletion calls `DELETE /api/auth/account` with the confirmed password. Only upon confirmed HTTP 200 success does the repository remove `jwt_token` from `SharedPreferences`. The presentation layer invalidates `userProfileProvider`, resets `authStateProvider` to `false`, displays a success notification, and navigates cleanly to `LoginScreen` via `pushAndRemoveUntil`.
- **Failure Flow:** On invalid password (HTTP 400) or network timeout, `jwt_token` is strictly preserved in `SharedPreferences`, authenticated state remains active, and error feedback is presented to the user without falsely clearing local credentials.
- **Network Timeouts:** Hardened `_extractErrorMessage` in `AuthRepository` to explicitly capture connection/send/receive timeouts and surface an actionable error message ("Network timeout. Please check your connection and retry.").
- **Tests Added:** Added 5 new tests in `frontend/test/auth_repository_and_cleanup_test.dart` covering success token removal, wrong-password credential retention, timeout handling, profile update mapping, and Riverpod provider cache invalidation.

### 3. Check 3 — Database Migration & Remote Deployment Status
- **Empirical MariaDB Audit:** Read-only query against `mindontrack-31353797c1.__EFMigrationsHistory` revealed:
  - Applied remotely:
    1. `20260909161703_AddHabits` (v8.0.4)
    2. `20261002081502_AddStudySessions` (v8.0.4)
    3. `20261006061237_AddRoomParticipants` (v8.0.4)
    4. `20261006064106_AddSingleActiveStudySessionConstraint` (v8.0.4)
  - Pending remote deployment:
    1. `20261012110919_FixStudySessionActiveUserIdColumn`
    2. `20261012121819_AddRoomParticipantConstraints`
    3. `20261012175608_AddUniqueIndexToHabitCompletions`
- **Pre-Flight Migration Risk Inspection:** Ran read-only queries against live tables for the 3 pending migrations:
  - `StudySessions`: 0 duplicate active sessions found; safe to apply `FixStudySessionActiveUserIdColumn`.
  - `RoomParticipants`: 0 duplicate `(RoomName, SeatNumber)` or `(RoomName, UserId)` entries found; 0 strings exceed 100 characters; safe to apply `AddRoomParticipantConstraints`.
  - `HabitCompletions`: 0 duplicate `(HabitId, Date)` entries found; safe to apply `AddUniqueIndexToHabitCompletions`.
- **Remote DB Protection:** Strict constraint honored — no migrations were executed against the remote database.

## Phase 5: Focus Analytics Backend

**Status: VERIFIED & COMPLETE**

Focus analytics endpoints, CQRS queries, handlers, DTOs, FluentValidation validators, and tests were implemented across the ASP.NET Core backend and verified against the Flutter client architecture.

### 1. Data Integrity and Metric Definitions
- **Total Focus Minutes:** Computed using `s.UserId == userId && s.EndTime != null` summing `DurationMinutes`. Strictly regression-tested against `GetCurrentUserQuery` (Profile total) ensuring identical calculation.
- **Session Exclusion:** Active / unfinished sessions (`EndTime == null`, `!IsCompleted`) are strictly excluded from completed focus totals and placed in `IncompleteSessionsCount`.
- **Daily Focus Bucketing:** Completed study sessions are attributed to their UTC start date (`s.StartTime.Date`). Contiguous daily records are generated across the requested range (`[StartDate, EndDate]`), ensuring empty days return `0` values for stable charting.
- **Weekly Focus Aggregation:** Generates contiguous 7-day chronological intervals for past N weeks (1 to 12 weeks, default 4).
- **Task Summary:** Computed from `DailyTask` (`TotalTasks`, `CompletedTasks`, `PendingTasks`, and `CompletionRatePercentage`). Because the database stores no `CompletedAt` timestamp, task metrics accurately reflect current task board state.
- **Habit Summary:** Reuses `HabitStreakCalculator` to compute `TotalHabits`, `TotalCompletions`, `CompletionsToday`, `CompletionsThisWeek`, and `ActiveStreaksCount`.

### 2. Endpoints Exposed
- `GET /api/analytics/summary`: Lifetime and rolling 7-day summary metrics, session breakdown, task completion rates, and habit streaks.
- `GET /api/analytics/daily?startDate=yyyy-MM-dd&endDate=yyyy-MM-dd`: Contiguous daily breakdown for a bounded range (max 90 days, validated via FluentValidation).
- `GET /api/analytics/weekly?weeks=4`: Chronological weekly intervals (1–12 weeks).

### 3. Security and Scoping
- All analytics queries derive user identity exclusively from `ICurrentUserService.UserId`.
- Client-supplied user IDs are rejected / not accepted.
- Cross-user isolation verified: User A's queries never receive User B's sessions, tasks, or habits.

### 4. Flutter Integration
- Created `FocusAnalyticsSummary` and `DailyAnalytics` models in `frontend/lib/features/analytics/data/models/`.
- Created `AnalyticsRepository` in `frontend/lib/features/analytics/data/repositories/`.
- Supported both Map and JSON string responses with graceful fallback.

### 5. Build and Test Summary
- **Backend API Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **249/249 passed (100%)** (18 new unit, regression, boundary, and controller tests added in `AnalyticsTests.cs`).
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)** (5 new model and repository tests added in `analytics_model_and_repository_test.dart`).
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — no migrations applied to remote production database.

## Phase 5 Final Analytics Verification

**Status: AUDIT & VERIFICATION COMPLETE**

A comprehensive safety audit and verification of the Focus Analytics backend was executed before proceeding to Phase 6.

### 1. Focus-Time Accuracy & Session End Hardening
- **Duration Calculation & Clock Skew:** In `EndStudySessionCommand.cs`, duration calculation was hardened to `session.DurationMinutes = Math.Max(0, (int)(session.EndTime.Value - session.StartTime).TotalMinutes);`, preventing negative durations caused by system clock adjustments or slight drifts.
- **Session End Idempotency:** Re-ending an already completed session (`isCompleted == true`) idempotently returns the existing session DTO without modifying `EndTime` or `DurationMinutes` and without issuing redundant database writes. Verified via unit test `EndStudySession_WhenCalledRepeatedly_IsIdempotentAndDoesNotModifyEndTimeOrDuration`.
- **Clock Drift Floor Verification:** Verified via unit test `EndStudySession_WhenStartTimeIsInFutureDueToClockSkew_FloorsDurationAtZero` that future start times floor `DurationMinutes` at 0.
- **Profile & Analytics Totals Parity:** Verified that `GetCurrentUserQuery` (Profile total) and `GetFocusAnalyticsSummaryQuery` (Analytics total) use identical business logic: `_context.StudySessions.Where(s => s.UserId == userId && s.EndTime != null).SumAsync(s => s.DurationMinutes)`. Regression-tested in `Summary_TotalFocusMinutes_RegressionMatchesProfileCalculation`.
- **Active/Incomplete Session Exclusion:** Unfinished sessions (`EndTime == null`, `!IsCompleted`) are excluded from completed focus minutes and counted exclusively under `IncompleteSessionsCount`.

### 2. Date Boundaries and Range Semantics
- **Daily Analytics Date Semantics:**
  - `StartDate` and `EndDate` are inclusive calendar dates in UTC.
  - Sessions are bucketed by `s.StartTime.Date` (UTC midnight). Query filters `s.StartTime >= effectiveStartDate && s.StartTime < endExclusive` where `endExclusive = effectiveEndDate.AddDays(1)`.
  - Contiguous daily breakdown is generated without date gaps. Empty days return `FocusMinutes = 0`, `CompletedSessionsCount = 0`, and `HabitCompletionsCount = 0`.
  - Max date range is 90 days, validated via FluentValidation (`GetDailyAnalyticsQueryValidator`). Rejects `StartDate > EndDate` and ranges exceeding 90 days.
  - Defensively floors daily and weekly focus minute calculations with `Math.Max(0, s.DurationMinutes)` to insulate against legacy negative values.
- **Weekly Analytics Date Semantics:**
  - Generates contiguous 7-day chronological intervals for past N weeks (1 to 12 weeks, default 4) ending on `effectiveEndDate`.
  - Week 1 is the oldest week, Week N is the current/most recent week.

### 3. Summary Metrics & Division-by-Zero Safety
- **Zero-Task Scenarios:** Returns `0.0` for `CompletionRatePercentage` when `TotalTasks == 0`, avoiding `DivideByZeroException` or `NaN`.
- **Zero-Session Scenarios:** Returns `0.0` for `AverageSessionMinutes` when `CompletedSessionsCount == 0`.
- **Habit Streaks:** Evaluated consistently using `HabitStreakCalculator.CalculateStreak(h.Completions, todayUtc).CurrentStreak >= 1`.
- **UTC Windowing:** `TodayFocusMinutes` evaluates UTC today (`[DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1))`); `ThisWeekFocusMinutes` evaluates a rolling 7-day UTC window (`[DateTime.UtcNow.Date.AddDays(-6), DateTime.UtcNow.Date.AddDays(1))`).

### 4. Flutter Integration & Contract Alignment
- Backend camelCase DTOs map 1:1 with Flutter models:
  - `FocusAnalyticsSummary`: `totalFocusMinutes`, `todayFocusMinutes`, `thisWeekFocusMinutes`, `totalSessionsCount`, `completedSessionsCount`, `incompleteSessionsCount`, `averageSessionMinutes`, `tasks`, `habits`.
  - `DailyAnalytics`: `startDate`, `endDate`, `totalFocusMinutes`, `totalCompletedSessions`, `totalHabitCompletions`, `dailyBreakdown`.
- Error handling in `AnalyticsRepository` tested for timeouts, 400, 401, 500, and non-JSON server error payloads.

### 5. Database Schema & Migration Status
- **Zero Schema Changes for Analytics:** Analytics relies entirely on existing columns and indexes across `StudySessions`, `DailyTasks`, `Habits`, and `HabitCompletions`.
- **Pending Remote Migrations:** Confirmed 0 changes to model snapshot (`dotnet ef migrations has-pending-model-changes`). The 3 previously pending migrations (`FixStudySessionActiveUserIdColumn`, `AddRoomParticipantConstraints`, `AddUniqueIndexToHabitCompletions`) remain unapplied to the remote database per project safety constraints.

### 6. Verification & Test Suite Summary
- **Backend Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **254/254 passed (100%)** (+5 tests added: 2 in `StudySessionHandlerTests.cs` and 3 in `AnalyticsTests.cs`).
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.










