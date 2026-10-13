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

## Phase 6 — Gamification Discovery and Design

**Status: DISCOVERY & DESIGN COMPLETE (PENDING APPROVAL TO IMPLEMENT)**

A complete discovery, architecture audit, and design specification for Phase 6 (Gamification: XP, Levels, and Achievements) was conducted across the MOT backend and Flutter client.

### 1. Existing Functionality Verified
- **User Entity Properties:**
  - `User.cs` already contains `public int XP { get; set; } = 0;`, `public int Level { get; set; } = 1;`, and `public int CurrentStreak { get; set; } = 0;`.
  - These columns already exist in the MariaDB `Users` table and the EF Core model snapshot. Currently, they default to `0, 1, 0` and are not updated by any business logic.
  - `UserProfileDto` and `GetCurrentUserQuery` already return `Level` and `XP`.
- **Study Sessions:**
  - Entity: `StudySession` (`Id`, `UserId`, `StartTime`, `EndTime`, `DurationMinutes`, `IsCompleted`).
  - Completion Event: `EndStudySessionCommand` sets `EndTime = DateTime.UtcNow`, `DurationMinutes = Math.Max(0, ...)`, and `IsCompleted = true`.
  - Idempotency: Re-ending an already completed session is a safe no-op.
  - Active/Abandoned: Sessions with `EndTime == null` or `!IsCompleted` remain incomplete and are never eligible for XP.
- **Daily Tasks:**
  - Entity: `DailyTask` (`Id`, `UserId`, `Title`, `Description`, `IsCompleted`, `CreatedAt`).
  - Completion Event: `ToggleTaskCommand` (inverts `IsCompleted`) and `UpdateTaskCommand` (sets `IsCompleted`).
  - Critical Finding: `DailyTask` has **no `CompletedAt` timestamp**. Tasks can be checked, unchecked, and rechecked indefinitely.
- **Habits & Completions:**
  - Entity: `Habit` and `HabitCompletion` (`Id`, `HabitId`, `Date` normalized to UTC midnight).
  - Completion Event: `ToggleHabitCompletionCommand`. If already completed for UTC today, untoggles (`RemoveRange`); if not, inserts completion with unique index on `(HabitId, Date)`.
  - Streaks: Centralized `HabitStreakCalculator` dynamically computes streaks from dates. No stored streak snapshot exists in `Habit` or `User`.
- **Identity & Security:**
  - All operations resolve `UserId` strictly via `ICurrentUserService.UserId`.
  - Client input cannot supply or override `UserId`.
- **Account Deletion & Cascading:**
  - `DeleteAccountCommand` removes `User`. MariaDB foreign keys (`Tasks`, `Habits`, `HabitCompletions`, `StudySessions`) are configured with `ON DELETE CASCADE`. Any new gamification tables referencing `Users` must also use `ON DELETE CASCADE`.
- **Flutter Baseline:**
  - `UserProfile.fromJson` already parses `level` and `xp`.
  - `ProfileScreen` already displays `'Level ${profile.level} • ${profile.xp} XP'`.
  - `HomeScreen` provides standard 5-tab navigation (`Tasks`, `Habits`, `Timer`, `Live`, `Profile`).

### 2. Proposed XP Award Rules and Values (Configurable)
All values will be encapsulated in a configurable options class (`GamificationOptions`):
- **Study Session Completion:**
  - Base Award: **10 XP** for any completed session of $\ge 5$ minutes.
  - Duration Rate: **1 XP for every 2 minutes** of completed focus time (e.g., 25-minute Pomodoro = 22 XP; 50-minute session = 35 XP).
  - Anti-Gaming Cap: Maximum 70 XP per single session (120-minute cap). Sessions $< 5$ minutes award 0 XP.
- **Daily Task Completion:**
  - Base Award: **15 XP** per completed task.
  - Award Policy: One-time award per `TaskId`. If untoggled and retoggled, 0 additional XP.
- **Habit Completion:**
  - Base Award: **20 XP** per completed habit per calendar day.
  - Award Policy: One-time award per `(HabitId, Date)`. If untoggled and retoggled on the same day, 0 additional XP.
- **Streak Bonuses:**
  - Awarded exclusively through milestone achievements (3-day, 7-day, 30-day streaks) to prevent compounding mathematical drift or timezone exploits.

### 3. Idempotency and Concurrency Strategy
- **Durable Append-Only XP Ledger (`XpTransactions` table):**
  - Rather than directly incrementing `User.XP` without an audit trail, every XP event creates an immutable ledger entry.
  - Unique composite constraint: `UQ_XpTransactions_UserId_SourceType_SourceId` on `(UserId, SourceType, SourceId)`.
  - `SourceType`: `"StudySession"`, `"Task"`, `"Habit"`, `"Achievement"`.
  - `SourceId`:
    - Study Session: `SessionId.ToString()`
    - Daily Task: `TaskId.ToString()`
    - Habit: `"{HabitId}:{Date:yyyy-MM-dd}"`
    - Achievement: `AchievementCode`
- **Toggling & Re-completion Behavior:**
  - If a user unchecks a task or untoggles a habit, their historical earned XP is preserved (preventing negative balances and level demotions).
  - If the user re-checks the task or re-toggles the habit, the transaction lookup checks if a record with `(UserId, SourceType, SourceId)` already exists. If found, **0 additional XP** is awarded.
- **Concurrency Collision Handling:**
  - In concurrent requests from multiple devices, the unique constraint catches MariaDB Error 1062 (`DbUpdateException`), detaches the duplicate candidate entity, and returns idempotently.
- **Materialized Balance Synchronization:**
  - `User.XP` and `User.Level` are updated atomically in the same transaction as the ledger record, guaranteeing $O(1)$ read performance for user profiles while maintaining 100% auditability and reconciliation capability (`ReconcileUserXp`).

### 4. Level Progression Formula
- **Deterministic Quadratic Progression:**
  - Level $L$ total XP threshold:
    $$\text{Threshold}(L) = 50 \cdot L \cdot (L - 1)$$
  - Level derivation from total XP $X$:
    $$\text{Level}(X) = \max\left(1, \left\lfloor \frac{1 + \sqrt{1 + \frac{2X}{25}}}{2} \right\rfloor\right)$$
  - Milestone table:
    - Level 1: 0 – 99 XP (100 XP to Level 2)
    - Level 2: 100 – 299 XP (200 XP to Level 3)
    - Level 3: 300 – 599 XP (300 XP to Level 4)
    - Level 4: 600 – 999 XP (400 XP to Level 5)
    - Level 5: 1000 – 1499 XP (500 XP to Level 6)
  - Level Progress:
    - Current level base: $XP_{curr} = 50 \cdot L \cdot (L - 1)$
    - Next level target: $XP_{next} = 50 \cdot (L + 1) \cdot L$
    - Delta: $XP_{delta} = 100 \cdot L$
    - Progress %: $\text{Math.Clamp}((double)(X - XP_{curr}) / XP_{delta}, 0.0, 1.0)$
  - Mathematical Properties: Closed-form $O(1)$, zero-XP yields Level 1 at 0%, strictly monotonic, cannot overflow signed 32-bit `int` (Level 5000 requires $>1.2$ billion XP).

### 5. Initial Achievements Catalog
Achievements are defined as code constants in `MOT.Domain` and persisted upon unlock in `UserAchievements`:
1. `FIRST_STEP`: Complete your first daily task (+25 XP).
2. `FOCUS_INITIATE`: Complete your first study session of $\ge 15$ minutes (+25 XP).
3. `HABIT_STARTER`: Complete your first habit (+25 XP).
4. `CENTURY_CLUB`: Reach 100 total minutes of focus time (+50 XP).
5. `DEEP_DIVER`: Complete a single focus session of $\ge 60$ minutes (+50 XP).
6. `STREAK_BUILDER`: Reach a 3-day habit streak on any habit (+50 XP).
7. `UNSTOPPABLE`: Reach a 7-day habit streak on any habit (+100 XP).
8. `TASK_MASTER`: Complete 10 daily tasks total (+75 XP).

- **Unlock Safety:**
  - Unique composite constraint: `UQ_UserAchievements_UserId_AchievementCode` on `(UserId, AchievementCode)`.
  - Duplicate unlocks are strictly blocked at both the application level and database constraint level.

### 6. Proposed Entities, Indexes, and Migrations
- **Proposed Entities:**
  1. `XpTransaction`:
     - `Guid Id` (PK)
     - `Guid UserId` (FK -> `Users(Id)`, `ON DELETE CASCADE`)
     - `int Amount` (positive integer)
     - `string SourceType` (`varchar(32)`)
     - `string SourceId` (`varchar(64)`)
     - `string Description` (`varchar(200)`)
     - `DateTime CreatedAt` (`datetime(6)`, UTC)
     - Unique Index: `(UserId, SourceType, SourceId)`
     - Index: `(UserId, CreatedAt)`
  2. `UserAchievement`:
     - `Guid Id` (PK)
     - `Guid UserId` (FK -> `Users(Id)`, `ON DELETE CASCADE`)
     - `string AchievementCode` (`varchar(64)`)
     - `DateTime UnlockedAt` (`datetime(6)`, UTC)
     - `int XpAwarded`
     - Unique Index: `(UserId, AchievementCode)`
- **Existing User Entity:**
  - `Users` table already has `XP`, `Level`, `CurrentStreak`. No schema change needed for `User`.
- **Migration Plan:**
  - A single local migration (e.g. `20261013xxxxxx_AddGamificationTables`) will be scaffolded during implementation.
  - Per project rules, no migrations are generated or applied during discovery.
- **MariaDB 10.11 Compatibility:**
  - Relies solely on standard `char(36)`, `varchar`, `int`, and composite unique indexes. Zero triggers or generated columns required. Fully compatible with MariaDB 10.11.

### 7. API Endpoints Proposal
1. `GET /api/gamification/summary`:
   - Returns: `currentLevel`, `totalXp`, `levelBaseXp`, `nextLevelXp`, `progressPercentage`, `totalAchievementsCount`, `unlockedAchievementsCount`.
2. `GET /api/gamification/achievements`:
   - Returns: List of all achievements with `code`, `title`, `description`, `iconName`, `xpAwarded`, `isUnlocked`, `unlockedAt`.
3. `GET /api/gamification/history?limit=20`:
   - Returns: Paginated list of recent `XpTransactionDto` records for activity feed.

### 8. Flutter Implementation Plan
- **Presentation Enhancements:**
  - **Level Progress Indicator:** In `ProfileScreen`, replace the static level chip with an interactive card showing the current level, total XP, next level target, and a smooth `LinearProgressIndicator`.
  - **Achievements Section:** Add an "Achievements" row in `ProfileScreen` displaying unlocked badges and a button navigating to `AchievementsScreen`.
  - **Recent Activity Feed:** Display the last 5 XP transactions under the stat cards.
  - **Gamification Feedback:** Surface a celebratory snackbar/toast when completing a session, task, or habit (`"+20 XP earned!"`).
- **Data Layer:**
  - Create `frontend/lib/features/gamification/data/models/gamification_summary.dart` and `achievement.dart`.
  - Create `frontend/lib/features/gamification/data/repositories/gamification_repository.dart`.
  - Create Riverpod providers: `gamificationSummaryProvider`, `achievementsProvider`.

### 9. Risks, Unresolved Decisions, and Test Plan
- **Risks & Edge Cases:**
  - *Concurrent Multi-Device Activity:* Handled via database composite unique constraints and graceful Error 1062 catching.
  - *Zero XP / Fresh Account:* Handled gracefully by formula ($X=0 \implies Level=1, Progress=0.0$).
  - *Timezone Shift on Habits:* Habit completion date is normalized to UTC midnight, matching existing Phase 3 behavior.
- **Unresolved Design Decisions for Approval:**
  1. *Untoggle XP Policy:* Proposed design keeps previously awarded XP and gives 0 XP on re-toggle. Alternative is decrementing XP, which risks negative balances and level demotions. (Recommended: Keep XP, award 0 on re-toggle).
  2. *Achievement Definitions Storage:* Proposed in-code definitions in `MOT.Domain` vs database seed table. (Recommended: In-code definitions to avoid fragile database seeding).
- **Test Plan:**
  - Level formula unit tests (0 XP, boundary values, level transitions, progress math, overflow safety).
  - XP ledger unit tests (first award, duplicate blocked, uncheck/recheck blocked, concurrent collision).
  - Achievement evaluator tests (first task unlock, focus duration threshold, streak milestone, duplicate unlock blocked).
  - Controller and authorization tests (200, 401, cross-user isolation).
  - Flutter model deserialization and state-management tests.

### 10. Subphases Breakdown (Implementation Order)
- **Phase 6.1 — Gamification Domain Core & Ledger:** Domain entities (`XpTransaction`, `UserAchievement`), `LevelCalculator`, DbContext configuration, local migration scaffolding, and core unit tests.
- **Phase 6.2 — XP Award Service & Event Handlers:** `IGamificationService` integration into `EndStudySessionCommand`, `ToggleTaskCommand`, and `ToggleHabitCompletionCommand` with idempotency guards and handler unit tests.
- **Phase 6.3 — Achievement Catalog & Evaluation Engine:** In-code achievement catalog, condition evaluator, unlock pipeline, and regression tests.
- **Phase 6.4 — Gamification API & Controllers:** `GamificationController` (`summary`, `achievements`, `history`), DTOs, queries, handlers, and HTTP integration tests.
- **Phase 6.5 — Flutter Client Gamification Integration:** Models, repository, Riverpod providers, `ProfileScreen` level progress bar, `AchievementsScreen`, and widget/model tests.

## Phase 6.1 — Core Domain and XP Ledger Implementation

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

Phase 6.1 established the durable XP ledger and pure domain-level progression calculator in accordance with Clean Architecture principles.

### 1. Entities and Domain Constants
- **`XpTransaction` Entity (`MOT.Domain.Entities.XpTransaction`):**
  - `Id`: GUID primary key (`Guid.NewGuid()`).
  - `UserId`: GUID foreign key referencing `Users(Id)` with `OnDelete(DeleteBehavior.Cascade)`.
  - `Amount`: Integer positive XP amount awarded.
  - `SourceType`: Max length 32 (`varchar(32)`), categorizing source (`StudySession`, `Task`, `Habit`, `Achievement`, `Bonus`).
  - `SourceId`: Max length 64 (`varchar(64)`), durable identifier for the triggering event (`SessionId`, `TaskId`, `"{HabitId}:{Date:yyyy-MM-dd}"`, `AchievementCode`).
  - `Description`: Max length 200 (`varchar(200)`), human-readable description for activity logs.
  - `CreatedAt`: UTC timestamp (`DateTime.UtcNow`).
- **`XpSourceTypes` Constants (`MOT.Domain.Gamification.XpSourceTypes`):**
  - Defines strongly-typed constants: `StudySession`, `Task`, `Habit`, `Achievement`, `Bonus`.

### 2. EF Core Model Configuration & Indexes
- Added `DbSet<XpTransaction> XpTransactions { get; set; }` to `IAppDbContext` and `AppDbContext`.
- Configured entity in `AppDbContext.OnModelCreating`:
  - Foreign key: `FK_XpTransactions_Users_UserId` referencing `Users(Id)` with cascade delete (`ON DELETE CASCADE`).
  - Unique composite index: `IX_XpTransactions_UserId_SourceType_SourceId` on `(UserId, SourceType, SourceId)` for duplicate award prevention.
  - History query index: `IX_XpTransactions_UserId_CreatedAt` on `(UserId, CreatedAt)`.
  - Column length constraints configured for MariaDB 10.11 utf8mb4 index safety (total composite index key length $\le 528$ bytes, far below MariaDB's 3072-byte InnoDB limit).

### 3. LevelCalculator Domain Logic
- Implemented pure domain calculator in `MOT.Domain.Gamification.LevelCalculator`:
  - Formula: $\text{Threshold}(L) = 50 \cdot L \cdot (L - 1)$.
  - Delta: $\Delta(L) = 100 \cdot L$ XP required to advance from Level $L$ to $L + 1$.
  - Negative XP clamped to 0 (Level 1, 0.0% progress).
  - Exact boundary transition verification: Uses 64-bit integer comparisons against thresholds to eliminate floating-point square-root inaccuracy near level boundaries.
  - 32-bit integer overflow protection: Internal calculations use `long` (64-bit integers), preventing overflow up to `int.MaxValue` ($2,147,483,647$ XP = Level 6554).
  - Returns `LevelProgress` record containing: `Level`, `TotalXp`, `CurrentLevelThresholdXp`, `NextLevelThresholdXp`, `XpEarnedInCurrentLevel`, `XpRequiredForNextLevel`, `ProgressFraction` (clamped $[0.0, 1.0]$).

### 4. Atomicity Design for Phase 6.2
When integrating award operations into command handlers in Phase 6.2:
1. **Pre-check:** `IGamificationService.AwardXpAsync` checks whether an `XpTransaction` already exists for `(UserId, SourceType, SourceId)`. If present, it returns an idempotent no-op (`AlreadyAwarded = true`, 0 XP).
2. **Transaction Insertion:** If not present, creates and stages the `XpTransaction` entity on the DbContext.
3. **User Balance Mutation:** Atomically mutates `user.XP += amount` and sets `user.Level = LevelCalculator.CalculateLevel(user.XP)` within the same DbContext ChangeTracker.
4. **Commit & Collision Recovery:** Calls `await _context.SaveChangesAsync(cancellationToken)`. If a concurrent request creates a collision on `(UserId, SourceType, SourceId)`, MariaDB raises Error 1062 (`DbUpdateException`). The handler catches this error, detaches the unpersisted `XpTransaction`, reloads/reverts the user entity, and returns `AlreadyAwarded = true` without throwing an unhandled exception or double-incrementing the balance.

### 5. Migration Details
- Generated Migration: `20261013113448_AddXpTransactionsLedger.cs` in `MOT.Infrastructure/Migrations`.
- Schema changes isolated to creating table `XpTransactions` and its 2 indexes. No modifications to other tables.
- Model snapshot verified: `dotnet ef migrations has-pending-model-changes` reports 0 pending changes.
- **Remote DB Protection:** Strict constraint honored — `dotnet ef database update` was NOT run. The live database was NOT modified.
- Existing pending migrations preserved unchanged:
  1. `20261012110919_FixStudySessionActiveUserIdColumn`
  2. `20261012121819_AddRoomParticipantConstraints`
  3. `20261012175608_AddUniqueIndexToHabitCompletions`
  4. `20261013113448_AddXpTransactionsLedger` (newly scaffolded)

### 6. Build and Test Verification
- **Backend Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **318/318 passed (100%)** (+64 new tests added in `LevelCalculatorTests.cs` and `XpLedgerTests.cs`).
  - Unit tests verify: 0 XP, negative XP, threshold calculations, exact boundaries (99/100, 299/300, 599/600), progress fractions (0%, 50%, 99.5%), large XP and `int.MaxValue` overflow safety, entity property defaults, and EF Core cascade delete / composite unique index metadata.
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.

### 7. Completed Phase 6.2 Implementation
See detailed Phase 6.2 implementation details below.

## Phase 6.2 — XP Award Service and Handler Integration

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

Phase 6.2 integrated the gamification XP award pipeline with core activity completion workflows (`StudySessions`, `DailyTasks`, and `Habits`) while guaranteeing atomic relational commits, deduplication, MariaDB Error 1062 collision recovery, and concurrency safety.

### 1. Architectural Components
- **`GamificationOptions` (`MOT.Application.Gamification.GamificationOptions`):**
  - Configurable server-side award parameters:
    - `TaskCompletionXp = 15`
    - `HabitCompletionXp = 20`
    - `StudySessionMinMinutes = 5`
    - `StudySessionBaseXp = 10`
    - `StudySessionMinutesPerXp = 2`
    - `StudySessionMaxXp = 70`
  - Registered via ASP.NET Core `IOptions<GamificationOptions>` in `Program.cs`.
- **`XpAwardResult` (`MOT.Application.Gamification.XpAwardResult`):**
  - Strongly-typed immutable record returned by award methods:
    - `Awarded`: Boolean indicating whether XP was newly awarded.
    - `Amount`: Integer XP amount staged.
    - `NewTotalXp`: Updated user XP balance.
    - `NewLevel`: Updated user level according to `LevelCalculator`.
    - `AlreadyAwarded`: Boolean flag indicating idempotency deduplication triggered.
    - `Reason`: Explanatory message for non-eligibility or duplicate state.
- **`UserConcurrencyLock` (`MOT.Application.Common.UserConcurrencyLock`):**
  - Thread-safe in-memory `SemaphoreSlim` registry keyed by `Guid UserId`.
  - Serializes concurrent awards for the same user, preventing lost updates on `user.XP` and `user.Level` while permitting completely parallel execution across different users.
- **`IGamificationService` & `GamificationService` (`MOT.Application.Gamification`):**
  - Registered as `Scoped` dependency in `MOT.Api/Program.cs`.
  - Encapsulates authoritative award formulas, validation, deduplication checks, and ledger staging.
  - Implements integer overflow protection: throws `OverflowException` if `(long)user.XP + amount > int.MaxValue`.
  - Implements `IsXpDuplicateCollision(DbUpdateException ex)` to identify MariaDB Error 1062 collisions on `IX_XpTransactions_UserId_SourceType_SourceId`.
  - Implements `HandleDuplicateCollision(Guid userId, string sourceType, string sourceId)`: safely detaches unpersisted `XpTransaction` entities from EF Core ChangeTracker and calls `userEntry.Reload()`.

### 2. Activity Award Formulas & Integration
1. **Study Sessions (`EndStudySessionCommandHandler`):**
   - Duration $< 5$ minutes: 0 XP (ineligible).
   - Duration $\ge 5$ minutes: Formula $10 + \lfloor\text{duration} / 2\rfloor$, capped at 70 XP ($5\text{m} \rightarrow 12\text{ XP}, 25\text{m} \rightarrow 22\text{ XP}, 50\text{m} \rightarrow 35\text{ XP}, 120\text{m} \rightarrow 70\text{ XP}, 180\text{m} \rightarrow 70\text{ XP}$).
   - Idempotency key: `(UserId, "STUDYSESSION", SessionId)`.
   - Re-ending an already completed session is a safe no-op with 0 XP awarded and zero duplicate database writes.
2. **Daily Tasks (`ToggleTaskCommandHandler`):**
   - First completion (`task.IsCompleted = true`): 15 XP awarded.
   - Idempotency key: `(UserId, "TASK", TaskId)`.
   - Unchecking task (`task.IsCompleted = false`): 0 XP awarded/deducted, completion status updated.
   - Re-checking task (`task.IsCompleted = true`): 0 XP awarded (duplicate detected via ledger), completion status updated.
3. **Habits (`ToggleHabitCompletionCommandHandler`):**
   - First daily completion: 20 XP awarded.
   - Idempotency key: `(UserId, "HABIT", $"{HabitId}:{Date:yyyy-MM-dd}")`.
   - Untoggling habit completion today: 0 XP deducted, completion record removed.
   - Re-toggling habit completion today: 0 XP awarded (duplicate detected via date-scoped idempotency key), completion record added.
   - Completion on subsequent day ($D+1$): new idempotency key generated, awards 20 XP.
   - Streak bonuses: deferred to Phase 6.3 per design specifications.

### 3. Relational Atomicity & Concurrency Collision Safety
- **Single Commit Atomicity:** Activity status (`session.IsCompleted`, `task.IsCompleted`, `HabitCompletion`) and XP ledger mutation (`XpTransaction`, `user.XP`, `user.Level`) are staged together in the same DbContext ChangeTracker and committed via a single `SaveChangesAsync(cancellationToken)` call.
- **MariaDB Error 1062 Detection:** Handler exception filters inspect both inner and outer exception messages for error number 1062 and index name `IX_XpTransactions_UserId_SourceType_SourceId`.
- **ChangeTracker Cleanliness:** On collision, candidate `XpTransaction` entities are transitioned to `EntityState.Detached`, and the user entity is reloaded from the database, preventing dirty state propagation.

### 4. Build and Test Verification
- **Backend Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **346/346 passed (100%)**:
  - 318 existing baseline tests.
  - 28 new tests in `GamificationServiceTests.cs` verifying:
    - Study session duration formula boundaries (0m, 4m, 5m, 25m, 50m, 120m, 180m cap).
    - Session incomplete / uncompleted validation.
    - Safe no-op on re-ending completed sessions.
    - Daily task first award (+15 XP), uncheck (0 XP), recheck (0 XP duplicate).
    - Habit daily award (+20 XP), untoggle (0 XP), retoggle (0 XP duplicate), next day award (+20 XP).
    - Balance overflow protection (`int.MaxValue`).
    - Error 1062 collision identification and unrelated constraint isolation.
    - End-to-end atomic completions across all three command handlers.
    - Thread-safe serialization with `UserConcurrencyLock`.
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — no migrations applied to remote production database.

### 5. Remaining Work for Next Subphases
- **Phase 6.3 — Achievement Catalog & Evaluation Engine:**
  - In-code catalog of 8 achievements in `MOT.Domain.Gamification`.
  - `UserAchievement` entity configuration and schema foundation.
  - Asynchronous evaluation pipeline on activity completion events.
  - Streak milestone evaluations and bonus awards.
- **Phase 6.4 — Gamification API & Controllers:**
  - `GamificationController` (`summary`, `achievements`, `history`).
  - DTOs, queries, and integration tests.
- **Phase 6.5 — Flutter Client Gamification Integration:**
  - Progress bar in `ProfileScreen`, `AchievementsScreen`, Riverpod state management.

## Phase 6.2 Final Transaction and Concurrency Audit

**Status: AUDIT & REGRESSION HARDENING COMPLETE**

A rigorous, end-to-end transaction boundary and concurrency safety audit was conducted on the completed Phase 6.2 XP award integration.

### 1. Transaction Boundaries & Atomicity Proof
- **Single Unit of Work:** For Study Sessions, Daily Tasks, and Habits, activity completion status mutations, XP ledger transaction insertions, `User.XP` increments, and `User.Level` updates are staged in the same `DbContext` ChangeTracker and committed via a single `await _context.SaveChangesAsync(cancellationToken)` call.
- **MariaDB Relational Guarantees:** EF Core executes the batch inside an implicit database transaction. If any statement or constraint fails, MariaDB executes an atomic rollback (`ROLLBACK`).
  - An activity cannot be marked completed without its corresponding XP award persisting.
  - XP cannot be awarded for an activity that fails to persist.
- **Pre-Persistence Exception Safety:** If an exception occurs during award calculation (e.g., `OverflowException` or entity validation failure), the handler terminates before calling `SaveChangesAsync`. All in-memory modifications remain uncommitted, preserving complete database integrity.

### 2. Concurrency Analysis: Single-Process vs. Multi-Instance
- **Single-Process Guarantee:** Within a single API process, `UserConcurrencyLock` (`ConcurrentDictionary<Guid, SemaphoreSlim>`) guarantees strict FIFO serialization for all concurrent requests targeting the same `UserId`.
  - The lock is acquired via `using (await UserConcurrencyLock.LockAsync(userId, cancellationToken))` before reading or modifying user state.
  - The `IDisposable` releaser ensures release upon both normal completion and exceptions.
- **Multi-Instance / Distributed Limitation Documented:**
  - In a multi-instance (horizontally scaled) environment, separate application instances maintain independent in-memory lock dictionaries.
  - If two instances simultaneously process awards for the same user (e.g. Instance 1 completes Task 1, Instance 2 completes Session 1), both read the initial `User.XP` (e.g. 100).
  - EF Core generates `UPDATE Users SET XP = @newVal WHERE Id = @userId` (setting absolute values, not relative increments `XP = XP + @delta`).
  - While InnoDB row-level locks serialize the writes, the second commit overwrites the first, resulting in a **lost update** of the earlier award on `User.XP` (even though both `XpTransaction` rows persist in the ledger).
  - *Future Distributed Scaling Path:* When multi-instance deployment is introduced, this must be addressed via distributed locking (e.g., MariaDB `GET_LOCK` or Redis distributed locks) or an EF Core concurrency token with automated reload-and-retry.
  - *Current MVP Architecture:* The application operates on a single hosted API instance, where `UserConcurrencyLock` provides complete thread-safety.

### 3. Duplicate-Key Recovery & ChangeTracker Cleanliness
- **Error 1062 Specificity:** `IsXpDuplicateCollision` specifically targets MariaDB Error 1062 on `IX_XpTransactions_UserId_SourceType_SourceId`.
- **Unrelated Errors Propagate:** Unrelated unique constraints (e.g., `IX_Users_Email`, `IX_RoomParticipants_RoomName_SeatNumber`), foreign key errors (1452), deadlocks (1213), and connection errors (2002) return `false` from `IsXpDuplicateCollision` and propagate cleanly.
- **ChangeTracker Recovery:**
  - In `HandleDuplicateCollision`, unpersisted `XpTransaction` entities are set to `EntityState.Detached`.
  - Tracked `User` entities are reloaded via `userEntry.Reload()`, restoring the persisted database balance.
  - In `EndStudySessionCommand` and `ToggleTaskCommand`, the respective activity entities are reloaded via `Reload()` on collision to synchronize in-memory state with the winning concurrent transaction.

### 4. XP Integrity & Validation Hardening
- **Zero & Negative XP Protection:** `StageXpAwardInternalAsync` rejects amounts $\le 0$, returning `NotEligible` and preventing misleading or corrupting ledger entries.
- **Argument Guards:** Non-empty validation enforced for `sourceType` and `sourceId`.
- **Direct Award Serialization:** `AwardXpDirectAsync` wrapped in `UserConcurrencyLock` to ensure consistent serialization across all entry points.
- **Level Recalculation:** `User.Level` is synchronized immediately with `LevelCalculator.CalculateLevel(user.XP)` on every balance mutation.

### 5. Verification & Test Suite Summary
- **Backend Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **368/368 passed (100%)** (+22 new tests covering direct awards, unrelated DB error propagation, failure before persistence, and multi-event concurrent awards).
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — remote database was never connected to or modified.

## Phase 6.3 — Achievement Catalog and Evaluation Engine

**Status: IMPLEMENTATION COMPLETE — VERIFIED**

Phase 6.3 implemented the deterministic achievement catalog, `UserAchievement` persistence model and migration, and the asynchronous activity evaluation pipeline across study sessions, tasks, and habits.

### 1. Architecture & Core Components

- **Achievement Definitions & Catalog (`MOT.Domain.Gamification`):**
  - `AchievementCodes`: Strongly-typed string constants for all 8 launch achievements.
  - `AchievementDefinition`: Immutable domain record containing `Code`, `Title`, `Description`, `IconName`, and `XpReward`.
  - `AchievementCatalog`: Centralized deterministic catalog exposing `All`, `GetByCode(code)`, and `Contains(code)`.
  - Defined initial achievements:
    1. `FIRST_STEP`: "Complete your first daily task" (+25 XP)
    2. `FOCUS_INITIATE`: "Complete your first study session of at least 15 minutes" (+25 XP)
    3. `HABIT_STARTER`: "Complete your first habit" (+25 XP)
    4. `CENTURY_CLUB`: "Reach 100 total minutes of completed focus time" (+50 XP)
    5. `DEEP_DIVER`: "Complete a single focus session of at least 60 minutes" (+50 XP)
    6. `STREAK_BUILDER`: "Reach a 3-day streak on any habit" (+50 XP)
    7. `UNSTOPPABLE`: "Reach a 7-day streak on any habit" (+100 XP)
    8. `TASK_MASTER`: "Complete 10 distinct daily tasks" (+75 XP)
- **`UserAchievement` Entity & Schema Foundation (`MOT.Domain.Entities`, `MOT.Infrastructure`):**
  - Entity attributes: `Id` (Guid PK), `UserId` (Guid FK -> Users), `User` navigation, `AchievementCode` (varchar 50), `UnlockedAt` (DateTime UTC), `XpAwarded` (int).
  - Configured cascade delete on User deletion.
  - Unique composite index: `IX_UserAchievements_UserId_AchievementCode` on `(UserId, AchievementCode)` guaranteeing at-most-once unlock per user.
  - Performance index: `IX_UserAchievements_UserId_UnlockedAt` on `(UserId, UnlockedAt)`.
  - Scaffolded EF Core migration: `20261013145328_AddUserAchievementsTable.cs` with 0 pending model changes. (Strict constraint honored: migration was NOT applied to remote database).
- **Deadlock-Free Staged XP (`IGamificationService.StageAchievementXpAsync`):**
  - Command handlers acquire `UserConcurrencyLock.LockAsync(userId)`.
  - `AchievementService` and `StageAchievementXpAsync` do NOT reacquire `UserConcurrencyLock`, preventing deadlocks on non-reentrant `SemaphoreSlim`.
  - Stages `XpTransaction` with `SourceType = XpSourceTypes.Achievement` and `SourceId = achievementCode`, updates `User.XP` and `User.Level` in-memory.
- **`IAchievementService` & `AchievementService` (`MOT.Application.Gamification`):**
  - Registered as `Scoped` in `MOT.Api/Program.cs`.
  - Inspects both database records and current EF ChangeTracker (`ChangeTracker.Entries<XpTransaction>()`, `ChangeTracker.Entries<HabitCompletion>()`) to account for activity completion in the active transaction before `SaveChangesAsync`.
  - Evaluates and unlocks achievements without re-awarding previously unlocked achievements (idempotency check via `GetUnlockedAchievementCodesAsync`).
  - MariaDB Error 1062 detection: `IsAchievementDuplicateCollision(DbUpdateException ex)` recognizes collisions on `IX_UserAchievements_UserId_AchievementCode` and `UserAchievements`.
  - Collision recovery: `HandleDuplicateCollision` detaches candidate `UserAchievement` entries, detaches associated `XpTransaction` entries, and reloads `User`.
- **Command Handler Integration:**
  - `EndStudySessionCommand`: evaluates `FOCUS_INITIATE`, `DEEP_DIVER`, `CENTURY_CLUB`.
  - `ToggleTaskCommand`: evaluates `FIRST_STEP`, `TASK_MASTER` (distinct task tracking).
  - `ToggleHabitCompletionCommand`: evaluates `HABIT_STARTER`, `STREAK_BUILDER`, `UNSTOPPABLE` using `HabitStreakCalculator`.
  - Extended catch filters in all 3 handlers to handle both `IsXpDuplicateCollision` and `IsAchievementDuplicateCollision`.

### 2. Relational Atomicity & Single Commit Guarantees

- **Single Unit of Work:** The activity status update (`session`, `task`, or `habitCompletion`), activity base XP transaction, `UserAchievement` record(s), achievement XP transaction(s), `user.XP`, and `user.Level` are staged in the same ChangeTracker and committed together via the handler's single `await _context.SaveChangesAsync(cancellationToken)` call.
- All-or-nothing rollback: If any achievement fails or a database constraint fails, MariaDB rolls back the entire batch.

### 3. Verification & Test Suite Summary

- **Backend Build:** `dotnet build MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test MOT.Tests/MOT.Tests.csproj` -> **422/422 passed (100%)**:
  - 368 existing baseline tests.
  - 6 new tests in `AchievementCatalogTests.cs` (catalog count, all 8 definitions, case-insensitivity, null/empty safety, total XP reward sum = 400).
  - 24 new tests in `AchievementServiceTests.cs` covering:
    - Study session thresholds (<15m, 15m, 60m, cumulative 100m, 99m boundary, uncompleted session, idempotency).
    - Task completions (0 tasks, 1 task, duplicate toggles of same task, 9 distinct tasks boundary, 10 distinct tasks, idempotency).
    - Habit completions and streak milestones (0 completions, 1st completion, streak 2 days boundary, streak 3 days, streak 6 days boundary, streak 7 days, idempotency).
    - Error handling, user not found, Error 1062 collision filter isolation, and mock context recovery safety.
  - Existing handlers updated in `StudySessionHandlerTests.cs`, `TaskHandlerTests.cs`, `HabitHandlerTests.cs`, and `GamificationServiceTests.cs`.
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — remote database was never connected to or modified; migrations were not applied to remote DB.

### 4. Remaining Work for Next Subphases

- **Phase 6.4 — Gamification API & Controllers:** Completed.
- **Phase 6.5 — Flutter Client Gamification Integration:**
  - Progress bar in `ProfileScreen`, `AchievementsScreen`, Riverpod state management.

## Phase 6.4 — Gamification API and Controllers

**Status: IMPLEMENTATION COMPLETE — VERIFIED**

Phase 6.4 implemented read-only, authenticated Gamification API endpoints, MediatR queries and handlers, DTOs, FluentValidation rules, and unit/controller tests.

### 1. Endpoints & Response Contracts

All endpoints are hosted under `GamificationController` (`api/gamification`) with `[Authorize]` and `[EnableCors("AllowAll")]`:

1. **`GET /api/gamification/summary`**
   - Returns the authenticated user's current progress calculated strictly via `LevelCalculator`:
     ```json
     {
       "level": 2,
       "totalXp": 250,
       "currentLevelThresholdXp": 100,
       "nextLevelThresholdXp": 300,
       "xpEarnedInCurrentLevel": 150,
       "xpRequiredForNextLevel": 200,
       "progressFraction": 0.75,
       "progressPercentage": 75.0,
       "totalAchievements": 8,
       "unlockedAchievements": 3
     }
     ```
2. **`GET /api/gamification/achievements`**
   - Returns the complete 8-achievement catalog from `AchievementCatalog.All`, joining against the authenticated user's unlock records:
     ```json
     [
       {
         "code": "FIRST_STEP",
         "title": "First Step",
         "description": "Complete your first daily task",
         "iconName": "check_circle",
         "xpReward": 25,
         "isUnlocked": true,
         "unlockedAt": "2026-10-13T12:00:00Z"
       },
       {
         "code": "FOCUS_INITIATE",
         "title": "Focus Initiate",
         "description": "Complete your first study session of at least 15 minutes",
         "iconName": "timer",
         "xpReward": 25,
         "isUnlocked": false,
         "unlockedAt": null
       }
     ]
     ```
3. **`GET /api/gamification/history?limit=20`**
   - Returns recent XP ledger entries for the authenticated user, newest first (`CreatedAt DESC, Id DESC`):
     ```json
     [
       {
         "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
         "amount": 25,
         "sourceType": "ACHIEVEMENT",
         "description": "Unlocked achievement: First Step",
         "createdAt": "2026-10-13T12:00:00Z"
       }
     ]
     ```

### 2. Validation & Security

- **User Scoping:** The user ID is exclusively resolved from `ICurrentUserService.UserId`. Client-supplied user IDs in query parameters or request bodies are strictly rejected/ignored.
- **Limit Validation:** Query parameter `limit` on `/history` is validated via `GetXpHistoryQueryValidator` (`InclusiveBetween(1, 100)`). Invalid limits return HTTP 400 Bad Request with standardized error details. Default limit is 20.
- **Read-Only Guarantees:** All queries execute with `AsNoTracking()` where applicable and never perform mutations, stage transactions, or call `SaveChangesAsync`.
- **Concurrency & Scaling Limitation:** Read queries do not require `UserConcurrencyLock`. The documented limitation that `UserConcurrencyLock` is in-process remains preserved for award commands.

### 3. Verification & Test Suite Summary

- **Backend API Build:** `dotnet build backend/MOT.Api/MOT.Api.csproj` -> **0 Errors, 0 Warnings**.
- **Backend Test Suite:** `dotnet test backend/MOT.Tests/MOT.Tests.csproj` -> **462/462 passed (100%)**:
  - 422 baseline tests.
  - 40 new tests in `GamificationApiTests.cs` covering:
    - Summary for new user with 0 XP (Level 1, 0% progress, 0 unlocked achievements).
    - Summary at exact level threshold boundaries and with partial progress.
    - User scoping and isolation across all queries.
    - Complete catalog mapping including locked achievements and unlock timestamps.
    - Empty history handling and newest-first chronological ordering.
    - History limit boundaries and validation rejection of invalid limits (<= 0 or > 100).
    - Read requests never mutating state or invoking `SaveChangesAsync`.
    - Missing user (`UserNotFoundException` -> 404) and unauthenticated (`UnauthorizedAccessException` -> 401).
    - Controller HTTP status codes (200, 400, 401, 404, 500) and attribute configurations.
- **Frontend Test Suite:** `flutter test` -> **24/24 passed (100%)**.
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — remote database was never connected to or modified; migrations were not applied to remote DB.

### 4. Phase 6.5 — Flutter Gamification UI Integration

**Status: IMPLEMENTATION & VERIFICATION COMPLETE**

A complete Flutter UI integration was implemented for the Gamification subsystem, consuming the backend endpoints (`GET /api/gamification/summary`, `GET /api/gamification/achievements`, `GET /api/gamification/history?limit=20`) without modifying backend business logic or database schemas.

#### 1. Data Models (`frontend/lib/features/gamification/data/models/`)
- `GamificationSummary`: parses `level`, `totalXp`, `currentLevelThresholdXp`, `nextLevelThresholdXp`, `xpEarnedInCurrentLevel`, `xpRequiredForNextLevel`, `progressFraction` (clamped to `[0.0, 1.0]`), `progressPercentage`, `totalAchievements`, `unlockedAchievements`. Includes defensive numeric fallbacks for `int` and `double` parsing.
- `AchievementItem`: parses `code`, `title`, `description`, `iconName`, `xpReward`, `isUnlocked`, `unlockedAt` (`DateTime?`).
- `XpTransactionItem`: parses `id`, `amount`, `sourceType`, `description`, `createdAt` (`DateTime`).

#### 2. Repository & Riverpod Providers (`frontend/lib/features/gamification/`)
- `GamificationRepository`:
  - `getSummary()`: queries `GET /api/gamification/summary`.
  - `getAchievements()`: queries `GET /api/gamification/achievements`.
  - `getHistory({int limit = 20})`: queries `GET /api/gamification/history?limit=20`.
  - Robust exception handling extracting server error `message` and formatting network/timeout failures.
- Riverpod Providers (`gamification_providers.dart`):
  - `gamificationRepositoryProvider`: provides singleton `GamificationRepository`.
  - `gamificationSummaryProvider`: `FutureProvider` auto-fetching authenticated gamification summary.
  - `achievementsProvider`: `FutureProvider` auto-fetching achievement catalog with unlock statuses.
  - `xpHistoryProvider`: `FutureProvider` auto-fetching recent 20 XP transactions.

#### 3. UI Screens & Components
- **`ProfileScreen` (`frontend/lib/features/profile/presentation/screens/profile_screen.dart`):**
  - **Level Progress Card:** Displays current level badge, total XP, current progress fraction (`LinearProgressIndicator`), and XP remaining to next level (`earnedInLevel / requiredForNext XP`), sourced authoritatively from `gamificationSummaryProvider` (with graceful fallback to `profile.level`/`profile.xp` while loading).
  - **Quick Stat Cards:** Retains existing Focus Minutes and Current Streak stats.
  - **Achievements Navigation Tile:** Displays unlocked count badge (`X / Y Unlocked`) with trophy icon, navigating smoothly to `AchievementsScreen`.
  - **Recent XP Activity Section:** Renders list of recent XP ledger entries showing date, award amount (`+X XP`), source icon (`TASK`, `HABIT`, `STUDYSESSION`, `ACHIEVEMENT`), and description, with clean empty and error states.
  - **Pull-to-Refresh & Invalidation:** Invalidation hooks for `gamificationSummaryProvider` and `xpHistoryProvider` on refresh and user logout.
- **`AchievementsScreen` (`frontend/lib/features/gamification/presentation/screens/achievements_screen.dart`):**
  - Dedicated screen displaying all 8 achievements from the catalog.
  - **Progress Summary Banner:** Shows `Unlocked X of Y` and completion percentage with gradient visual card.
  - **Achievement Cards:** Rich card layout for unlocked vs locked achievements (gold tint & highlight for unlocked; dimmed with lock indicator for locked).
  - **Safe Icon Mapping:** Resolves catalog icon names (`check_circle`, `timer`, `repeat`, `flag`, `trophy`, `award`, `stars`) to Material icons, with a safe fallback to `Icons.emoji_events_rounded` for any unknown icon names.
  - **Timestamp & XP Reward Display:** Shows unlock date for unlocked badges, XP reward tag (`+X XP`), title, and description.
  - **Empty and Error States:** Full empty state banner and error state with retry button.

#### 4. Verification & Test Suite Summary
- **Frontend Test Suite:** `flutter test` -> **52/52 passed (100%)**:
  - 18 unit tests in `gamification_model_and_repository_test.dart` (models parsing, fallbacks, repo calls, error surfacing, parameter passing).
  - 10 widget & provider tests in `gamification_ui_and_provider_test.dart` (`ProfileScreen` level card, achievements navigation, history list, empty states, error retry, `AchievementsScreen` progress banner, icon fallbacks).
  - 24 existing tests across auth, analytics, habits, timers, classroom smoke tests.
- **Frontend Static Analysis:** `flutter analyze --no-fatal-infos` -> **0 errors, 0 warnings** in new and updated files.
- **Backend Test Suite:** `dotnet test backend/MOT.Tests/MOT.Tests.csproj` -> **462/462 passed (100%)**.
- **EF Model Snapshot Check:** `dotnet ef migrations has-pending-model-changes` -> **0 pending model changes**.
- **Remote DB Protection:** Strict constraint honored — remote database was never connected to or modified; no migrations applied.

---

## Phase 7 — Comprehensive Release-Readiness Audit

**Audit Status: COMPLETED**  
**Release Readiness Category: Ready for controlled staging deployment** (Blocked from direct production deployment pending pre-migration database remediation and staging sign-off)

---

### 1. Audit Scope and Baseline Metrics

A thorough, end-to-end release-readiness audit was performed across the entire Mind On Track (MOT) repository covering Phases 0 through 6.5.

- **Technology Stack Preserved:**
  - Frontend: Flutter (Dart 3.13 / Flutter 3.47) with Flutter Riverpod
  - Backend: ASP.NET Core (.NET 8.0), EF Core 8.0, Pomelo MySQL/MariaDB provider (target MariaDB 10.11)
  - Architecture: Clean Architecture, CQRS/MediatR, FluentValidation, Repository pattern
- **Remote Database Protection Invariant:**
  - Zero connections, queries, schema mutations, or migration commands executed against the remote MariaDB database.
  - Local EF Core model snapshot remains in 100% synchronization (`0 pending model changes`).
- **Initial Baseline Verification (Prior to Fixes):**
  - Backend Build: `dotnet build backend/MOT.Api/MOT.Api.csproj` -> 0 Errors, 0 Warnings
  - Backend Test Suite: `dotnet test backend/MOT.Tests/MOT.Tests.csproj` -> 462/462 Passed (100%)
  - Frontend Test Suite: `flutter test` -> 52/52 Passed (100%)
  - Frontend Static Analysis: 15 issues (6 async context lint warnings across auth/habit screens, 9 Flutter 3.47 deprecation infos)
  - Working Tree Status: Clean on branch `main`

---

### 2. Verified Defect Fixes Applied

During the audit, 3 verified defects were identified, fixed with minimal targeted edits, and regression-tested:

1. **Incomplete Provider Cache Invalidation on Account Deletion (`SettingsScreen`):**
   - *Defect:* `SettingsScreen._showDeleteAccountDialog` invalidated `userProfileProvider` and reset `authStateProvider`, but omitted `gamificationSummaryProvider`, `achievementsProvider`, and `xpHistoryProvider`. If an account was deleted and a new user logged in during the same application run, stale XP, level progress, and achievement data could remain in memory.
   - *Fix:* Added imports and explicit invalidations for `gamificationSummaryProvider`, `achievementsProvider`, and `xpHistoryProvider` in `settings_screen.dart`.
   - *Regression Test:* Added multi-provider invalidation verification to `frontend/test/auth_repository_and_cleanup_test.dart`.

2. **Unshielded BuildContext across Async Gaps in Auth Screens:**
   - *Defect:* In `login_screen.dart` and `register_screen.dart`, network failure handlers invoked `ScaffoldMessenger.of(context)` and `setState` without checking `if (mounted)`. If the screen unmounted while the network request was in flight, this triggered unhandled Flutter runtime exceptions.
   - *Fix:* Added `if (mounted)` guards to all catch and finally blocks in `login_screen.dart` and `register_screen.dart`, and used `dialogContext` inside dialog builder.

3. **Shadowed Context in Habit Dashboard Sheet & List Builder:**
   - *Defect:* In `habit_dashboard_screen.dart`, the `showModalBottomSheet` builder and `ListView.builder` used shadowed `context` variables, causing `ScaffoldMessenger.of(context)` calls after async gaps to reference unmounted dialog/item contexts.
   - *Fix:* Renamed shadowed contexts to `sheetContext` and `itemContext`, and properly guarded `ScaffoldMessenger` with `if (mounted)`.

---

### 3. Backend Security & Authorization Audit

- **Authentication & Endpoint Protection:**
  - Controller-level `[Authorize]` attributes verified across `TasksController`, `HabitsController`, `StudySessionsController`, `AnalyticsController`, `GamificationController`, and `ClassroomsController`.
  - Only anonymous endpoints in the entire API are `POST api/auth/register` and `POST api/auth/login`.
- **Identity Isolation & User Scoping:**
  - All user-specific queries, commands, and operations resolve user ID exclusively from `ICurrentUserService.UserId` (extracted from the authenticated JWT claims).
  - Client-supplied IDs in query strings or request bodies cannot override `UserId`.
  - All database entity queries filter by `UserId == userId` (e.g. `t.Id == request.Id && t.UserId == userId`). Cross-user access returns `TaskNotFoundException`, `HabitNotFoundException`, or `StudySessionNotFoundException` (HTTP 404), preventing resource enumeration.
- **Password Recovery & Account Lifecycle:**
  - `POST api/auth/reset-password` remains explicitly disabled, returning a uniform HTTP 400 Bad Request to prevent account enumeration and unauthorized overrides.
  - `POST api/auth/change-password` requires verification of the current password using PBKDF2 hashing before accepting a new password.
  - `DELETE api/auth/account` requires current password verification before performing destructive actions.
  - **Stateless JWT Behavior on Account Deletion:** Because JWTs are stateless and verified via HMAC-SHA256 signature and expiration (`TokenValidationParameters.ValidateLifetime = true`), a previously issued token technically remains syntactically valid until its expiry time. However, because the user row and all associated child entities are deleted from the database, all authenticated endpoints that perform database user lookups fail with HTTP 404 (`UserNotFoundException`) or return empty datasets. For absolute token revocation, a Redis-backed token blacklist or token version stamp would be required for distributed multi-instance architectures.
- **Error Response Uniformity & Information Leakage Prevention:**
  - FluentValidation failures return consistent HTTP 400 Bad Request (`{ Error = ..., Errors = ... }`).
  - Missing resources return HTTP 404 Not Found (`{ Error = ... }`).
  - Duplicate email, active session conflicts, and seat conflicts return HTTP 409 Conflict (`{ Error = ... }`).
  - Uncaught exceptions return HTTP 500 Internal Server Error with generic messages (`{ Error = "An unexpected error occurred..." }`), logging the underlying exception details securely to Serilog console without exposing SQL statements, connection strings, or stack traces to the client.

---

### 4. XP and Gamification Integrity & Concurrency Audit

- **Server-Authoritative Awards:** All XP calculations, levels, and achievements are computed strictly on the backend. No client request parameter can supply or manipulate XP award amounts.
- **Incomplete Session Protection:** Study sessions with `!session.IsCompleted` or `session.EndTime == null` return `XpAwardResult.NotEligible` and award 0 XP. Duration $< 5$ minutes awards 0 XP.
- **Duplicate Award Prevention:**
  - Ending an already ended study session is an idempotent no-op (`session.IsCompleted == true` skips award logic).
  - Unchecking and re-checking a task checks `XpTransactions.AnyAsync(UserId, "TASK", taskId)`. Because the ledger record was inserted on first completion, rechecking awards 0 XP (`XpAwardResult.Duplicate`).
  - Untoggling and re-toggling a habit completion on the same UTC date checks `XpTransactions.AnyAsync(UserId, "HABIT", $"{habitId}:{yyyy-MM-dd}")` and awards 0 duplicate XP.
  - Achievements check `UserAchievements` catalog and database unique constraint `IX_UserAchievements_UserId_AchievementCode`, ensuring at most one unlock and one XP award per user.
- **Atomicity & Ledger Integrity:**
  - Staging `XpTransaction`, incrementing `User.XP`, calculating `User.Level = LevelCalculator.CalculateLevel(User.XP)`, and persisting the activity state commit within a single `SaveChangesAsync()` call.
  - Non-positive award amounts are rejected with `XpAwardResult.NotEligible`.
  - Balance overflow is checked: `(long)user.XP + amount > int.MaxValue` throws `OverflowException`.
- **Concurrency Analysis Across Scenarios:**
  1. *Single API Instance (In-Process Concurrency):* Fully protected. `UserConcurrencyLock.LockAsync(userId)` serializes all concurrent operations for the same user within the process, eliminating race conditions.
  2. *Duplicate Source-Event Retries (Cross-Instance):* Fully protected by database unique constraints:
     - `IX_XpTransactions_UserId_SourceType_SourceId` on `(UserId, SourceType, SourceId)`
     - `IX_UserAchievements_UserId_AchievementCode` on `(UserId, AchievementCode)`
     - `IX_HabitCompletions_HabitId_Date` on `(HabitId, Date)`
     - `IX_StudySessions_ActiveUserId` on `(ActiveUserId)`
     Any concurrent attempt hits DB error 1062, caught by `IsXpDuplicateCollision` / `IsAchievementDuplicateCollision`, which detaches the transaction, reloads the user, and safely recovers without duplicating XP.
  3. *Concurrent Awards for DIFFERENT Events Across MULTIPLE API Instances (Release Limitation):*
     - If two separate API instances process two different activities (e.g. Task A on Instance 1 and Task B on Instance 2) for the same user at the exact same millisecond:
     - Both instances read `User.XP = 100`.
     - Instance 1 inserts `XpTransaction(Task A, +15)` and sets `User.XP = 115`.
     - Instance 2 inserts `XpTransaction(Task B, +15)` and sets `User.XP = 115`.
     - Because `UserConcurrencyLock` is in-memory (`SemaphoreSlim`), both commit successfully. The ledger will record both transactions (30 XP total), but the denormalized `User.XP` row could suffer a lost update (`115` instead of `130`).
     - **Recommendation / Blocker for Horizontal Scaling:** Multi-instance deployment requires distributed locking (e.g. MariaDB `GET_LOCK(CONCAT('user_', userId), 10)`), EF Core concurrency tokens (`RowVersion` on `User`), or atomic SQL increments (`UPDATE Users SET XP = XP + @amount WHERE Id = @userId`). For single-instance deployment, the current implementation is completely safe.

---

### 5. Analytics and Activity Correctness Audit

- **Total Focus Minutes Parity:** `GetCurrentUserQueryHandler` and `GetFocusAnalyticsSummaryQueryHandler` both calculate total focus minutes using identical filtering: `s.UserId == userId && s.EndTime != null`.
- **Duration Non-Negativity:** `EndStudySessionCommandHandler` computes duration using `Math.Max(0, (int)(session.EndTime.Value - session.StartTime).TotalMinutes)`. Analytics aggregation queries floor daily/weekly durations with `Math.Max(0, s.DurationMinutes)` to insulate against legacy anomalies.
- **Date Range Semantics:**
  - Daily analytics generates contiguous calendar days from `StartDate` to `EndDate` inclusive. Days with no activity return `FocusMinutes = 0`, `CompletedSessionsCount = 0`, `HabitCompletionsCount = 0`.
  - Weekly analytics generates contiguous 7-day chronological intervals for past N weeks (default 4).
  - Habit streak evaluation reuses `HabitStreakCalculator.CalculateStreak(h.Completions, todayUtc)`.
- **Division-by-Zero Safety:**
  - `taskCompletionRate` returns `0.0` when `totalTasks == 0`.
  - `averageSessionMinutes` returns `0.0` when `completedSessions == 0`.

---

### 6. Flutter Client Integration Audit

- **Contract Synchronization:** Flutter models (`GamificationSummary`, `AchievementItem`, `XpTransactionItem`, `UserProfile`, `DailyAnalytics`, `FocusAnalyticsSummary`) strictly map backend camelCase JSON contracts and include defensive parsing for numeric types and nullable dates.
- **Network & Interceptors:** `NetworkClient` injects `Authorization: Bearer <token>` from `SharedPreferences`. Network timeouts (120s) and error payloads are mapped to user-friendly error messages.
- **State Management & Caching:**
  - Riverpod providers (`gamificationSummaryProvider`, `achievementsProvider`, `xpHistoryProvider`, `userProfileProvider`, `authStateProvider`) manage data state.
  - Full cache invalidation implemented on pull-to-refresh, manual retry, user logout, and account deletion.
- **UI Degradation & Fallbacks:**
  - Unknown achievement icon strings safely fall back to `Icons.emoji_events_rounded`.
  - Profile level progress falls back to `profile.level` and `profile.xp` while gamification summary loads.
  - Empty and error states provide clear messaging and retry buttons.
- **Static Analysis Status:**
  - `flutter analyze --no-fatal-infos` -> **0 errors, 0 warnings**. 9 remaining infos are cosmetic Flutter 3.47 deprecations (`withOpacity` in legacy screens and `prefer_const`).

---

### 7. Database Migration & Deployment Safety Audit

#### Migration Inventory and Status
The repository contains 9 migrations. The first 4 were previously applied. The following 5 migrations are **pending deployment**:

| Order | Migration Identifier | Target Table | Type | Breaking / Locking Risk |
|---|---|---|---|---|
| 1 | `20261012110919_FixStudySessionActiveUserIdColumn` | `StudySessions` | Schema DDL + Backfill + Unique Index | **HIGH:** Unique index creation fails if multiple uncompleted sessions exist for a single user. |
| 2 | `20261012121819_AddRoomParticipantConstraints` | `RoomParticipants` | Schema DDL + Unique Indexes | **LOW/MEDIUM:** Unique index creation fails if duplicate seats or duplicate user entries exist in active rooms. |
| 3 | `20261012175608_AddUniqueIndexToHabitCompletions` | `HabitCompletions` | Schema DDL + Unique Index | **HIGH:** Unique index creation fails if duplicate habit completions exist for the same `(HabitId, Date)`. |
| 4 | `20261013113448_AddXpTransactionsLedger` | `XpTransactions` | Create Table + Unique Index | **LOW:** New table creation; zero existing rows. |
| 5 | `20261013145328_AddUserAchievementsTable` | `UserAchievements` | Create Table + Unique Index | **LOW:** New table creation; zero existing rows. |

#### Critical Operational Risk: Automatic Startup Migration
- In `Program.cs` lines 116-120:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbContext.Database.Migrate();
  }
  ```
- **Hazard:** When `MOT.Api` boots with a live production database connection, `dbContext.Database.Migrate()` automatically attempts to apply all 5 pending migrations in sequence. If existing duplicate data exists in `HabitCompletions` or `StudySessions`, the migration will fail with MySQL Error 1062, aborting container startup.

#### Pre-Migration Data Sanitation & Deployment Checklist (Runbook)
Before applying migrations or booting an updated API container against staging/production:

1. **Backup Database:** Execute a full logical backup (`mysqldump` / RDS snapshot) of the MariaDB database.
2. **Audit & Deduplicate `StudySessions`:**
   ```sql
   -- Check for multiple active sessions per user
   SELECT UserId, COUNT(*) 
   FROM StudySessions 
   WHERE IsCompleted = 0 AND EndTime IS NULL 
   GROUP BY UserId 
   HAVING COUNT(*) > 1;

   -- Clean up older duplicate active sessions if found (keep only the newest)
   UPDATE StudySessions s
   INNER JOIN (
       SELECT Id, UserId, ROW_NUMBER() OVER(PARTITION BY UserId ORDER BY StartTime DESC) as rn
       FROM StudySessions
       WHERE IsCompleted = 0 AND EndTime IS NULL
   ) ranked ON s.Id = ranked.Id
   SET s.IsCompleted = 1, s.EndTime = s.StartTime
   WHERE ranked.rn > 1;
   ```
3. **Audit & Deduplicate `HabitCompletions`:**
   ```sql
   -- Check for duplicate completions on the same date
   SELECT HabitId, Date, COUNT(*) 
   FROM HabitCompletions 
   GROUP BY HabitId, Date 
   HAVING COUNT(*) > 1;

   -- Deduplicate HabitCompletions keeping the oldest record
   DELETE c1 FROM HabitCompletions c1
   INNER JOIN HabitCompletions c2 
   WHERE c1.HabitId = c2.HabitId 
     AND c1.Date = c2.Date 
     AND c1.Id > c2.Id;
   ```
4. **Flush Stale `RoomParticipants`:**
   ```sql
   -- Clean up ephemeral classroom seats
   TRUNCATE TABLE RoomParticipants;
   ```
5. **Apply Migrations in Exact Order:**
   Run EF Core migration bundle or execute `dotnet ef database update` in maintenance mode.

---

### 8. Automated Test Execution Summary

| Test Suite | Execution Command | Tests Passed | Tests Failed | Pass Rate | Status |
|---|---|---|---|---|---|
| **Backend Build** | `dotnet build backend/MOT.Api/MOT.Api.csproj` | 0 Errors, 0 Warnings | — | 100% | **PASSED** |
| **Backend Unit Tests** | `dotnet test backend/MOT.Tests/MOT.Tests.csproj` | **462** | 0 | 100% | **PASSED** |
| **EF Model Snapshot** | `dotnet ef migrations has-pending-model-changes` | 0 pending changes | 0 | 100% | **PASSED** |
| **Frontend Unit & Widget Tests** | `flutter test` (in `frontend/`) | **52** | 0 | 100% | **PASSED** |
| **Frontend Static Analysis** | `flutter analyze --no-fatal-infos` | 0 Errors, 0 Warnings (9 cosmetic deprecations) | 0 | 100% | **PASSED** |

*Note: All test executions run against clean in-memory mocks, isolated SQLite/InMemory contexts, and mocked Dio HTTP clients without remote network calls.*

---

### 9. Regression and Manual Smoke-Test Checklist

| Functional Area | Test Scenario | Status | Verification Reference |
|---|---|---|---|
| **Authentication** | User registration with valid email, password, and name | **Automated** | `AuthenticationTests.cs` |
| **Authentication** | User login with valid credentials (JWT token generation) | **Automated** | `AuthenticationTests.cs` |
| **Authentication** | User login with invalid password (401 response) | **Automated** | `AuthenticationTests.cs` |
| **Authentication** | Disabled self-service password reset (uniform 400 response) | **Automated** | `AuthController.cs` / `ProfileManagementTests.cs` |
| **Authentication** | Authenticated password change with PBKDF2 verification | **Automated** | `ProfileManagementTests.cs` |
| **Authentication** | Account deletion with password verification & cascade cleanup | **Automated** | `ProfileManagementTests.cs` / `auth_repository_and_cleanup_test.dart` |
| **Authentication** | Logout / delete account clears all cached Riverpod providers | **Automated** | `auth_repository_and_cleanup_test.dart` |
| **Daily Tasks** | Create task, list tasks, get task by ID (user-scoped) | **Automated** | `TaskHandlerTests.cs` |
| **Daily Tasks** | Toggle task complete (awards 15 XP on first completion) | **Automated** | `TaskHandlerTests.cs` / `GamificationServiceTests.cs` |
| **Daily Tasks** | Toggle task uncomplete (does not revoke XP; does not re-award on re-check) | **Automated** | `TaskHandlerTests.cs` / `XpLedgerTests.cs` |
| **Daily Tasks** | Delete task (user-scoped) | **Automated** | `TaskHandlerTests.cs` |
| **Habits** | Create habit, list habits, get habit by ID (user-scoped) | **Automated** | `HabitHandlerTests.cs` |
| **Habits** | Toggle habit completion today (awards 20 XP on first completion) | **Automated** | `HabitHandlerTests.cs` / `GamificationServiceTests.cs` |
| **Habits** | Toggle habit untoggle (removes completion; does not re-award XP on re-toggle) | **Automated** | `HabitHandlerTests.cs` / `XpLedgerTests.cs` |
| **Habits** | Dynamic streak calculation (consecutive UTC days) | **Automated** | `HabitHandlerTests.cs` / `HabitStreakCalculator` |
| **Study Sessions** | Start session (enforces single active session per user) | **Automated** | `StudySessionHandlerTests.cs` |
| **Study Sessions** | End session (calculates duration, sets IsCompleted, awards duration XP) | **Automated** | `StudySessionHandlerTests.cs` / `GamificationServiceTests.cs` |
| **Study Sessions** | Repeat session end (idempotent no-op; 0 duplicate XP) | **Automated** | `StudySessionHandlerTests.cs` / `XpLedgerTests.cs` |
| **Study Sessions** | Abandoned/incomplete session (0 XP awarded; excluded from completed totals) | **Automated** | `StudySessionHandlerTests.cs` / `AnalyticsTests.cs` |
| **Analytics** | Focus summary (total, today, week, average session, task/habit rates) | **Automated** | `AnalyticsTests.cs` / `UserProfileHandlerTests.cs` |
| **Analytics** | Daily breakdown (contiguous chronological days, empty day handling) | **Automated** | `AnalyticsTests.cs` / `GetDailyAnalyticsQuery` |
| **Analytics** | Weekly breakdown (contiguous 7-day intervals) | **Automated** | `AnalyticsTests.cs` / `GetWeeklyAnalyticsQuery` |
| **Gamification** | Level calculation formulas (threshold boundaries, levels 1–50) | **Automated** | `LevelCalculatorTests.cs` |
| **Gamification** | Achievement catalog evaluation (8 achievements: first step, initiate, deep diver, etc.) | **Automated** | `AchievementServiceTests.cs` / `AchievementCatalogTests.cs` |
| **Gamification** | Read-only APIs (`GET /summary`, `GET /achievements`, `GET /history`) | **Automated** | `GamificationApiTests.cs` |
| **Gamification** | History query limit validation (1–100 boundary checking) | **Automated** | `GamificationApiTests.cs` / `GetXpHistoryQueryValidator` |
| **Virtual Classroom** | Join room, assign seat, detect room full (12 max) | **Automated** | `ClassroomHandlerTests.cs` |
| **Virtual Classroom** | Seat collision handling and heartbeat ping | **Automated** | `ClassroomHandlerTests.cs` |
| **Flutter UI** | Profile screen level card, achievements tile, recent XP activity list | **Automated** | `gamification_ui_and_provider_test.dart` |
| **Flutter UI** | Achievements screen progress banner, locked/unlocked cards, icon fallback | **Automated** | `gamification_ui_and_provider_test.dart` |
| **Flutter UI** | Pull-to-refresh and network error retry buttons | **Automated** | `gamification_ui_and_provider_test.dart` |
| **Physical Testing** | Live camera/microphone stream in WebRTC classroom on iOS/Android device | **Not Yet Verified** | Requires physical device testing on staging environment |
| **Physical Testing** | Push notifications and background timer alerts on physical device | **Not Yet Verified** | Requires physical device testing on staging environment |

---

### 10. Release Assessment and Categorization

#### Selected Assessment Category:
### **2. Ready for controlled staging deployment**

#### Detailed Justification Based on Verified Evidence:
1. **Why Local Testing and Controlled Staging are READY:**
   - 100% of automated tests pass across both backend (462/462) and frontend (52/52).
   - EF Core model snapshot is in 100% parity with code (`has-pending-model-changes` returns 0).
   - All authorization, identity-scoping, and error-handling requirements are verified.
   - All gamification award rules, idempotency guards, and single-instance concurrency serialization are proven.
   - All Flutter models, repositories, and Riverpod cache lifecycles are validated and clean of analyzer warnings.

2. **Why Production Deployment is BLOCKED Pending Actions:**
   - **Unapplied Remote Database Migrations:** 5 migrations remain unapplied to the remote database.
   - **Pre-Migration Data Risks:** If the remote database has existing duplicate habit completions or unclosed study sessions, the automatic `dbContext.Database.Migrate()` on startup will crash with Error 1062. Pre-migration deduplication SQL scripts must be executed before deployment.
   - **Horizontal Multi-Instance Scaling Limitation:** Distributed concurrency safety across multiple API container replicas is not yet database-backed (currently in-process via `SemaphoreSlim`). The production environment must run as a single container instance until distributed locks or concurrency tokens are deployed.

