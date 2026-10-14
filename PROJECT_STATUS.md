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

---

## Phase 8 — Staging Deployment Preflight and Migration Safety Review

**Review Status: COMPLETE (READY FOR OPERATOR REVIEW & APPROVAL)**  
**Target Environment:** Staging Isolated Environment  
**Execution Policy:** Strict Zero-Database Mutation (No remote queries, no schema changes, no migration applications executed in this task)

---

### 1. Deployment Configuration & Startup Decoupling Analysis

#### A. Current Startup & Migration Behavior
- **Inspection of `Program.cs` (lines 116–120):**
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbContext.Database.Migrate();
  }
  ```
  `dbContext.Database.Migrate()` is currently executed unconditionally inside `Program.cs` during application startup.
  - **Risk:** Whenever any API container or local process boots, EF Core automatically discovers all pending migrations and executes their `Up()` scripts against the target database before any HTTP request is served.
  - If unhandled data anomalies (such as duplicate habit completions or multiple active sessions) exist in the target database, container startup will immediately throw a fatal `DbUpdateException` (MySQL Error 1062) and terminate the application process in a crash loop.

#### B. Connection String Resolution & Cross-Environment Contamination Risk
- **Inspection of `Program.cs` (lines 65–67):**
  ```csharp
  var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
                         ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                         ?? throw new InvalidOperationException("Connection string not found.");
  ```
  - **Critical Contamination Hazard:** `appsettings.json` contains a fallback connection string pointing to `mysql.gb.stackcp.com`.
  - If a staging container is provisioned without `DB_CONNECTION_STRING` explicitly injected (or if the environment variable name is misspelled), the application silently falls back to the production database host in `appsettings.json`.
  - Combined with the unconditional `dbContext.Database.Migrate()` on startup, **a misconfigured staging deployment could inadvertently apply schema migrations directly to the production database.**

#### C. Proposed Code & Configuration Changes (Pre-Deployment Guardrail)
To eliminate startup risks, the following two changes are proposed for operator approval prior to staging deployment:

1. **Decouple Migrations from Web Application Startup:**
   Modify `Program.cs` to gate migration execution behind an explicit environment variable:
   ```csharp
   // Only execute schema migrations if explicitly requested by an operator
   var applyMigrations = string.Equals(
       Environment.GetEnvironmentVariable("APPLY_MIGRATIONS_ON_STARTUP"), 
       "true", 
       StringComparison.OrdinalIgnoreCase);

   if (applyMigrations)
   {
       using (var scope = app.Services.CreateScope())
       {
           var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
           dbContext.Database.Migrate();
       }
   }
   ```
   *Impact:* Web API containers boot with `APPLY_MIGRATIONS_ON_STARTUP=false` (default), ensuring that container restarts never run DDL or alter database state. Migrations become an intentional, operator-controlled execution step.

2. **Prevent Accidental Fallback to Production Connection String:**
   In `Program.cs`, in non-development environments (`!app.Environment.IsDevelopment()`), require `DB_CONNECTION_STRING` from the environment and forbid falling back to `appsettings.json`:
   ```csharp
   var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
   if (string.IsNullOrWhiteSpace(connectionString))
   {
       if (builder.Environment.IsDevelopment())
       {
           connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
       }
       else
       {
           throw new InvalidOperationException(
               "CRITICAL: DB_CONNECTION_STRING environment variable is required in non-development environments. Startup aborted to prevent cross-environment targeting.");
       }
   }
   ```
   *Impact:* Completely prevents a staging or production container from accidentally communicating with or modifying an unintended database.

#### D. Single API Instance Deployment Requirement
- Because user-level concurrency serialization relies on in-memory `UserConcurrencyLock` (`SemaphoreSlim`), the staging and production deployment configurations must enforce **exactly one container replica**:
  - **Render Web Service:** Set instance count to `1` (disable auto-scaling).
  - **Docker Compose:** `deploy.replicas: 1`.
  - **Kubernetes:** `spec.replicas: 1`.

---

### 2. Comprehensive Pending Migration Review

The repository contains 9 total migrations. The first 4 were previously applied. The following 5 migrations are **pending deployment**:

```
[Previously Applied]
  1. 20260909161703_AddHabits
  2. 20261002081502_AddStudySessions
  3. 20261006061237_AddRoomParticipants
  4. 20261006064106_AddSingleActiveStudySessionConstraint

[Pending Deployment]
  5. 20261012110919_FixStudySessionActiveUserIdColumn
  6. 20261012121819_AddRoomParticipantConstraints
  7. 20261012175608_AddUniqueIndexToHabitCompletions
  8. 20261013113448_AddXpTransactionsLedger
  9. 20261013145328_AddUserAchievementsTable
```

#### Detailed Inspection of Each Pending Migration

#### 1. `20261012110919_FixStudySessionActiveUserIdColumn`
- **Target Table:** `StudySessions`
- **`Up` Operations:**
  1. Inspects `INFORMATION_SCHEMA.COLUMNS`; dynamically executes `ALTER TABLE StudySessions ADD ActiveUserId char(36) COLLATE ascii_general_ci NULL` (or `MODIFY COLUMN` if already partially present).
  2. Backfills: `UPDATE StudySessions SET ActiveUserId = UserId WHERE IsCompleted = 0 AND EndTime IS NULL;`
  3. Inspects `INFORMATION_SCHEMA.STATISTICS`; dynamically creates unique index `IX_StudySessions_ActiveUserId` on `(ActiveUserId)`.
- **Unique Index Prerequisite:** Non-null values of `ActiveUserId` must be unique. If any user has $\ge 2$ rows where `IsCompleted = 0 AND EndTime IS NULL`, the backfill statement assigns identical `ActiveUserId` values, causing `CREATE UNIQUE INDEX` to fail with error 1062.
- **MariaDB 10.11 Compatibility:** Fully compatible; uses prepared dynamic SQL. In MariaDB/MySQL, DDL statements cause an implicit transaction commit.
- **Rollback (`Down`) Limitations:** Drops unique index and drops column `ActiveUserId`. Reversible, but dropping the column clears all active session tracking data.

#### 2. `20261012121819_AddRoomParticipantConstraints`
- **Target Table:** `RoomParticipants`
- **`Up` Operations:**
  1. Alters `Username` from `longtext` to `varchar(100)` utf8mb4.
  2. Alters `RoomName` from `longtext` to `varchar(100)` utf8mb4.
  3. Creates unique index `IX_RoomParticipants_RoomName_SeatNumber` on `(RoomName, SeatNumber)`.
  4. Creates unique index `IX_RoomParticipants_RoomName_UserId` on `(RoomName, UserId)`.
- **Prerequisites & Risks:** If existing data has usernames or room names longer than 100 characters, column modification will fail with `Data too long for column`. If duplicate seats or duplicate user sessions exist in the same room, unique index creation fails with error 1062.
- **MariaDB 10.11 Compatibility:** Fully compatible.
- **Rollback (`Down`) Limitations:** Drops unique indexes and alters columns back to `longtext`. Safely reversible.

#### 3. `20261012175608_AddUniqueIndexToHabitCompletions`
- **Target Table:** `HabitCompletions`
- **`Up` Operations:**
  1. Creates composite unique index `IX_HabitCompletions_HabitId_Date` on `(HabitId, Date)`.
  2. Drops redundant single-column index `IX_HabitCompletions_HabitId`.
- **Unique Index Prerequisite:** No two rows may have the same `(HabitId, Date)`. Any historical duplicates will immediately trigger error 1062.
- **Ordering & Dependency Safeguard:** The composite unique index is created *before* the single-column index is dropped. This satisfies InnoDB's requirement that the foreign key `FK_HabitCompletions_Habits_HabitId` has a covering index on its leading column at all times.
- **MariaDB 10.11 Compatibility:** Fully compatible.
- **Rollback (`Down`) Limitations:** Recreates single-column index and drops composite unique index. Safely reversible.

#### 4. `20261013113448_AddXpTransactionsLedger`
- **Target Table:** `XpTransactions` (New Table)
- **`Up` Operations:**
  1. Creates table `XpTransactions` (`Id char(36)`, `UserId char(36)`, `Amount int`, `SourceType varchar(32)`, `SourceId varchar(64)`, `Description varchar(200)`, `CreatedAt datetime(6)`).
  2. Creates foreign key `FK_XpTransactions_Users_UserId` referencing `Users(Id)` with `ON DELETE CASCADE`.
  3. Creates index `IX_XpTransactions_UserId_CreatedAt`.
  4. Creates composite unique index `IX_XpTransactions_UserId_SourceType_SourceId`.
- **Prerequisites & Risks:** Zero migration risk during `Up` because the table is brand new.
- **CRITICAL ROLLBACK LIMITATION (`Down`):** `migrationBuilder.DropTable("XpTransactions");`
  - If the application runs in staging and awards XP, rolling back this migration drops the table and **permanently destroys all user XP ledger history.**

#### 5. `20261013145328_AddUserAchievementsTable`
- **Target Table:** `UserAchievements` (New Table)
- **`Up` Operations:**
  1. Creates table `UserAchievements` (`Id char(36)`, `UserId char(36)`, `AchievementCode varchar(64)`, `UnlockedAt datetime(6)`, `XpAwarded int`).
  2. Creates foreign key `FK_UserAchievements_Users_UserId` referencing `Users(Id)` with `ON DELETE CASCADE`.
  3. Creates composite unique index `IX_UserAchievements_UserId_AchievementCode`.
  4. Creates index `IX_UserAchievements_UserId_UnlockedAt`.
- **Prerequisites & Risks:** Zero migration risk during `Up` because the table is brand new.
- **CRITICAL ROLLBACK LIMITATION (`Down`):** `migrationBuilder.DropTable("UserAchievements");`
  - Rolling back this migration drops the table and **permanently destroys all unlocked user achievement records.**

---

### 3. Read-Only SQL Preflight Review Document

The following read-only `SELECT` queries must be executed by an operator against the target staging database before applying any migrations. No writes, updates, or deletions are performed by these queries.

#### Preflight Query 1: Detect Users with Multiple Incomplete / Active Study Sessions
```sql
SELECT 
    s.UserId,
    u.Email,
    u.DisplayName,
    COUNT(*) AS ActiveSessionCount,
    MIN(s.StartTime) AS OldestSessionStartTime,
    MAX(s.StartTime) AS NewestSessionStartTime,
    GROUP_CONCAT(s.Id ORDER BY s.StartTime DESC SEPARATOR ', ') AS SessionIds
FROM StudySessions s
LEFT JOIN Users u ON s.UserId = u.Id
WHERE s.IsCompleted = 0 AND s.EndTime IS NULL
GROUP BY s.UserId, u.Email, u.DisplayName
HAVING COUNT(*) > 1;
```
- **What the Result Means:**
  - *0 Rows:* No users have concurrent unclosed sessions. Migration 5 will backfill and create `IX_StudySessions_ActiveUserId` cleanly.
  - *$\ge 1$ Rows:* Violations exist. The backfill statement in Migration 5 would populate duplicate non-null `ActiveUserId` values, aborting migration with error 1062.
- **Decision Needed Before Remediation:** The operator must review the returned `SessionIds` to determine which session represents genuine active focus versus abandoned sessions.

#### Preflight Query 2: Detect Duplicate Habit Completions on the Same Calendar Date
```sql
SELECT 
    c.HabitId,
    h.Title AS HabitTitle,
    h.UserId,
    c.Date,
    COUNT(*) AS DuplicateCount,
    GROUP_CONCAT(c.Id ORDER BY c.Id ASC SEPARATOR ', ') AS CompletionIds
FROM HabitCompletions c
LEFT JOIN Habits h ON c.HabitId = h.Id
GROUP BY c.HabitId, h.Title, h.UserId, c.Date
HAVING COUNT(*) > 1;
```
- **What the Result Means:**
  - *0 Rows:* All habit completions are unique by date. Migration 7 will create `IX_HabitCompletions_HabitId_Date` cleanly.
  - *$\ge 1$ Rows:* Historical duplicate completions exist for the same habit and date. Migration 7 will abort with error 1062.
- **Decision Needed Before Remediation:** Operator must decide which specific completion record to preserve and verify whether duplicates artificially impacted user streaks.

#### Preflight Query 3: Detect Multiple Participants Claiming the Same Seat in a Classroom
```sql
SELECT 
    RoomName,
    SeatNumber,
    COUNT(*) AS OccupantCount,
    GROUP_CONCAT(Username SEPARATOR ', ') AS Usernames,
    GROUP_CONCAT(UserId SEPARATOR ', ') AS UserIds,
    GROUP_CONCAT(Id SEPARATOR ', ') AS ParticipantIds
FROM RoomParticipants
GROUP BY RoomName, SeatNumber
HAVING COUNT(*) > 1;
```
- **What the Result Means:**
  - *0 Rows:* All assigned seats in all classrooms are distinct. Migration 6 will create `IX_RoomParticipants_RoomName_SeatNumber` cleanly.
  - *$\ge 1$ Rows:* Multiple participant records claim the identical seat number in a room. Migration 6 will abort with error 1062.
- **Decision Needed Before Remediation:** Check if any of the participants are actively connected or if they are stale ghost sessions.

#### Preflight Query 4: Detect a Single User Occupying Multiple Seats in the Same Classroom
```sql
SELECT 
    RoomName,
    UserId,
    COUNT(*) AS UserSeatCount,
    GROUP_CONCAT(SeatNumber SEPARATOR ', ') AS SeatNumbers,
    GROUP_CONCAT(Id SEPARATOR ', ') AS ParticipantIds
FROM RoomParticipants
GROUP BY RoomName, UserId
HAVING COUNT(*) > 1;
```
- **What the Result Means:**
  - *0 Rows:* No user holds multiple seats in the same room. Migration 6 will create `IX_RoomParticipants_RoomName_UserId` cleanly.
  - *$\ge 1$ Rows:* A user has multiple concurrent participant rows in one room. Migration 6 will abort with error 1062.
- **Decision Needed Before Remediation:** Identify which seat belongs to the active connection.

#### Preflight Query 5: Detect Classroom Column Values Exceeding 100 Characters
```sql
SELECT 
    Id,
    RoomName,
    LENGTH(RoomName) AS RoomNameBytes,
    CHAR_LENGTH(RoomName) AS RoomNameChars,
    Username,
    LENGTH(Username) AS UsernameBytes,
    CHAR_LENGTH(Username) AS UsernameChars
FROM RoomParticipants
WHERE CHAR_LENGTH(RoomName) > 100 OR CHAR_LENGTH(Username) > 100;
```
- **What the Result Means:**
  - *0 Rows:* Altering `Username` and `RoomName` to `varchar(100)` in Migration 6 will succeed without truncation.
  - *$\ge 1$ Rows:* Data exceeds 100 characters; altering column type will fail with data truncation error.
- **Decision Needed Before Remediation:** Inspect and trim offending strings before migration.

#### Preflight Query 6: Relational Integrity & Orphan Record Check
```sql
-- 6A: StudySessions referencing non-existent Users
SELECT s.Id AS OrphanSessionId, s.UserId, s.StartTime 
FROM StudySessions s
LEFT JOIN Users u ON s.UserId = u.Id
WHERE u.Id IS NULL;

-- 6B: HabitCompletions referencing non-existent Habits
SELECT c.Id AS OrphanCompletionId, c.HabitId, c.Date 
FROM HabitCompletions c
LEFT JOIN Habits h ON c.HabitId = h.Id
WHERE h.Id IS NULL;

-- 6C: Habits referencing non-existent Users
SELECT h.Id AS OrphanHabitId, h.UserId, h.Title 
FROM Habits h
LEFT JOIN Users u ON h.UserId = u.Id
WHERE u.Id IS NULL;

-- 6D: DailyTasks referencing non-existent Users
SELECT t.Id AS OrphanTaskId, t.UserId, t.Title 
FROM Tasks t
LEFT JOIN Users u ON t.UserId = u.Id
WHERE u.Id IS NULL;
```
- **What the Result Means:**
  - *0 Rows:* All existing foreign key relationships are strictly consistent.
  - *$\ge 1$ Rows:* Orphan records exist in legacy data.
- **Decision Needed Before Remediation:** Investigate source of orphan records before applying foreign key cascades in new tables.

---

### 4. Challenge and Critique of Previous Cleanup Recommendations

The previous release-readiness audit proposed three database cleanup operations. Under rigorous review, **none of the three operations should be accepted as safe default actions.**

#### Challenge 1: Marking Duplicate Active Sessions as `IsCompleted = 1, EndTime = StartTime`
- **Previous Proposal:** `UPDATE StudySessions SET IsCompleted = 1, EndTime = StartTime WHERE ...`
- **Critical Defects & Adverse Impacts:**
  1. *Analytics Falsification:* In Focus Analytics (`GetFocusAnalyticsSummaryQuery`), `CompletedSessionsCount` counts all sessions where `s.EndTime != null && s.IsCompleted`. Marking abandoned sessions as `IsCompleted = 1` artificially inflates `CompletedSessionsCount`, which severely depresses `AverageSessionMinutes` (`TotalFocusMinutes / CompletedSessionsCount`).
  2. *Data Inaccuracy:* Abandoned sessions were *never* completed. Forcing `IsCompleted = 1` corrupts the historical domain audit trail.
- **Preservation-First Remediation Strategy:**
  - **Do NOT mark `IsCompleted = 1`!**
  - Instead, mark abandoned older sessions as **abandoned/closed without completion**:
    `SET EndTime = StartTime, IsCompleted = 0, ActiveUserId = NULL`
  - *Why this is superior:*
    - `EndTime IS NULL` is now false, so Migration 5's backfill ignores the abandoned row and does NOT assign `ActiveUserId`.
    - `IsCompleted` remains `0`, so Focus Analytics correctly classifies it as an incomplete session and does not distort `CompletedSessionsCount` or `AverageSessionMinutes`.
    - Only the single genuine active session (the most recent one) retains `EndTime = NULL` and receives `ActiveUserId = UserId`.

#### Challenge 2: Deleting Duplicate Habit Completions by `Id > c2.Id`
- **Previous Proposal:** `DELETE c1 FROM HabitCompletions c1 INNER JOIN HabitCompletions c2 WHERE c1.HabitId = c2.HabitId AND c1.Date = c2.Date AND c1.Id > c2.Id;`
- **Critical Defects & Adverse Impacts:**
  1. *Arbitrary Deletion:* `Id` is a `char(36)` GUID generated by `Guid.NewGuid()`. Comparing `c1.Id > c2.Id` compares random hexadecimal characters, NOT timestamps or sequence numbers. This deletes an arbitrary record.
  2. *Irreversible Data Loss without Audit Trail:* Deleting directly from `HabitCompletions` destroys historical completion records without preserving proof of what was removed.
- **Preservation-First Remediation Strategy:**
  - **Step 1 (Pre-Archival):** Before touching any record, copy all identified duplicate completions to an audit archive table:
    ```sql
    CREATE TABLE IF NOT EXISTS `_Archive_Duplicate_HabitCompletions` LIKE HabitCompletions;
    INSERT INTO `_Archive_Duplicate_HabitCompletions`
    SELECT * FROM HabitCompletions WHERE Id IN (...duplicate IDs identified by Preflight Query 2...);
    ```
  - **Step 2 (Human Verification):** Operator reviews the archive rows and verifies user streak impacts.
  - **Step 3 (Targeted Deduplication):** Explicitly retain one confirmed ID per `(HabitId, Date)` and delete only the archived redundant IDs.

#### Challenge 3: Truncating the Entire `RoomParticipants` Table
- **Previous Proposal:** `TRUNCATE TABLE RoomParticipants;`
- **Critical Defects & Adverse Impacts:**
  1. *Active User Disruption:* `TRUNCATE` unilaterally kicks out any student currently participating in a live virtual study classroom, causing socket drops and video disconnects.
  2. *Non-Transactional DDL:* In MySQL/MariaDB, `TRUNCATE TABLE` is a DDL statement that causes an implicit commit and cannot be rolled back inside a transaction.
- **Preservation-First Remediation Strategy:**
  - Check Preflight Queries 3, 4, and 5 first.
  - If Preflight Queries 3, 4, and 5 return 0 rows, **no cleanup of `RoomParticipants` is needed at all!** The migration will succeed with zero data alteration.
  - If conflicts exist, surgically remove only the stale records (participants with no heartbeat in the last 10 minutes) or resolve duplicate seat numbers without clearing active participants.
  - A full clear of `RoomParticipants` should only occur during a formal, announced maintenance window when the application is in maintenance mode.

---

### 5. Staging Deployment Plan (Step-by-Step Runbook)

```
[Phase 1: Environment Isolation & Verification]
  Step 1: Verify staging database connection string is isolated from production.
  Step 2: Verify single API instance configuration (replicas: 1).
  Step 3: Perform full logical backup of staging database.

[Phase 2: Preflight Inspection & Gated Remediation]
  Step 4: Execute Read-Only Preflight Queries 1 through 6 against staging DB.
  Step 5: If any query returns rows, execute preservation-first remediation with audit archiving.
  Step 6: Re-run Preflight Queries to confirm 0 conflicts remain.

[Phase 3: Migration Execution]
  Step 7: Generate migration script locally: `dotnet ef migrations script 20261006064106 ...`
  Step 8: Operator inspects generated SQL and executes migration update against staging DB.
  Step 9: Verify `__EFMigrationsHistory` contains all 9 migrations.

[Phase 4: API Deployment & Verification]
  Step 10: Deploy staging API container with `APPLY_MIGRATIONS_ON_STARTUP=false`.
  Step 11: Execute automated smoke test suite (Auth, Tasks, Habits, Sessions, Analytics, XP, Achievements).
  Step 12: Verify rollback runbook and document staging sign-off.
```

#### Detailed Step Specifications:

1. **Confirm Environment Isolation:**
   - Verify `DB_CONNECTION_STRING` on the staging host points to the dedicated staging database instance.
   - Verify that `DefaultConnection` in `appsettings.json` is not referenced by the staging environment.
2. **Execute Full Database Backup:**
   - Run full logical dump: `mysqldump -h <staging-host> -u <user> -p --single-transaction --routines --triggers <staging-db> > mot_staging_pre_migration_backup.sql`.
   - Verify backup file size and integrity.
3. **Execute Read-Only Preflight Queries:**
   - Run Preflight Queries 1, 2, 3, 4, 5, and 6. Record query outputs in deployment log.
4. **Gated Remediation (If and Only If Conflicts Found):**
   - If Query 1 returns rows: apply preservation-first update setting `EndTime = StartTime, IsCompleted = 0` on older abandoned sessions.
   - If Query 2 returns rows: archive duplicate rows to `_Archive_Duplicate_HabitCompletions` and remove redundant records.
   - If Query 3/4 returns rows: remove stale participants with expired heartbeats.
   - Re-run preflight queries to confirm 0 rows returned.
5. **Generate & Inspect Migration SQL:**
   - Run local script generation without connecting to database:
     `dotnet ef migrations script 20261006064106 --project backend/MOT.Infrastructure/MOT.Infrastructure.csproj --startup-project backend/MOT.Api/MOT.Api.csproj -o staging_migrations.sql`
   - Operator reviews `staging_migrations.sql` to confirm DDL matches expected operations.
6. **Apply Migrations Explicitly:**
   - Execute migration via migration bundle or `dotnet ef database update` with staging connection string.
   - Query `SELECT MigrationId FROM __EFMigrationsHistory;` to verify all 5 pending migrations are recorded.
7. **Deploy Single API Instance:**
   - Deploy API container configured with:
     - `APPLY_MIGRATIONS_ON_STARTUP=false`
     - Replicas / Instance count = `1`
     - `ASPNETCORE_ENVIRONMENT=Staging`
8. **Staging Smoke Test Verification:**
   - Verify API health endpoint responds HTTP 200.
   - Verify login, registration, and JWT token issuance.
   - Start and end a study session; verify single active session constraint.
   - Toggle daily task; verify 15 XP award in `XpTransactions` and `User.XP`.
   - Toggle habit completion; verify 20 XP award and streak evaluation.
   - Verify `GET /api/gamification/summary` reflects exact level formula.
   - Verify `GET /api/gamification/achievements` returns 8 catalog badges.
   - Verify `GET /api/analytics/summary` focus minutes parity.
9. **Rollback & Database Recovery Limitations:**
   - **Important Constraint:** In MySQL/MariaDB, DDL statements cause an implicit commit. A failed DDL migration cannot be rolled back via an ambient SQL transaction.
   - If a migration fails mid-way:
     - Do not re-run blindly.
     - Inspect `__EFMigrationsHistory` to determine which migrations committed.
     - If schema state is inconsistent, restore from `mot_staging_pre_migration_backup.sql`.
   - **Post-Go-Live Rollback Warning:** Rolling back Migration 8 (`XpTransactions`) or Migration 9 (`UserAchievements`) executes `DROP TABLE`, permanently destroying all user XP transactions and achievement unlocks accumulated during staging.

---

### 6. Remaining Blockers and Required Approvals

Prior to executing the staging deployment runbook, explicit human operator approval is required for:

| Approval Item | Description | Required Sign-Off |
|---|---|---|
| **Approval A: Startup Decoupling** | Approval to update `Program.cs` to gate `Database.Migrate()` behind `APPLY_MIGRATIONS_ON_STARTUP` and enforce non-dev `DB_CONNECTION_STRING`. | Lead Engineer / DevOps |
| **Approval B: Staging DB Isolation** | Confirmation that staging database host/credentials are 100% physically isolated from production. | DevOps / DBA |
| **Approval C: Preflight SQL Results** | Review of output from Read-Only Preflight Queries 1–6 against staging database. | Lead Engineer / DBA |
| **Approval D: Remediation Plan (If Needed)** | Record-level review of any data corrections if Preflight Queries 1–6 identify conflicts. | Lead Engineer / Product Owner |
| **Approval E: Migration Execution** | Authorization to execute `dotnet ef database update` on staging database. | Release Manager |

---




## Comprehensive Backend Readiness Audit (October 14, 2026)

**Audit Role:** Senior ASP.NET Core Backend Architect, QA Engineer, Database Engineer, Application Security Reviewer  
**Audit Objective:** Independent, evidence-based readiness evaluation across Architecture, Security, Data Layer, Domain Logic, API Contracts, Test Quality, and Operational Deployment.  
**Stack Invariant Preserved:** Flutter, ASP.NET Core (.NET 8.0), EF Core 8.0, Pomelo MySQL/MariaDB provider (target MariaDB 10.11), Clean Architecture, CQRS/MediatR, FluentValidation.  
**Execution Policy:** Strict Zero-Database Mutation (No remote queries, no schema changes, no migration applications, zero secrets exposed).

---

### 1. Executive Verdict

### **Verdict: READY FOR CONTROLLED STAGING (BLOCKED FROM PRODUCTION)**

#### Detailed Rationale:
1. **Why Local and Controlled Staging are Ready:**
   - **Solid Domain and Clean Architecture Foundation:** Core domain entities, MediatR handlers, validation behaviors, and repositories are fully implemented for Authentication, Tasks, Habits, Study Sessions, Focus Analytics, and Gamification (XP Ledger + Achievements).
   - **100% Mocked Test Pass Rate:** 462/462 backend unit tests pass without errors or regressions.
   - **Clean EF Core Model Synchronization:** `dotnet ef migrations has-pending-model-changes` reports 0 pending model changes. The C# domain model and EF Core model snapshot match completely.
   - **Strict Ownership and Authorization:** Every authenticated endpoint resolves tenant identity exclusively from `ICurrentUserService.UserId` (claims-based JWT). Cross-tenant queries and mutations are isolated.

2. **Why Production Deployment is Blocked (P0 Blockers):**
   - **CRITICAL / P0-1: Secrets Committed in Source Control:** Database connection credentials and JWT signing secret are stored directly in `backend/MOT.Api/appsettings.json` lines 10 and 13.
   - **CRITICAL / P0-2: Automatic Unconditional Startup Migrations:** `Program.cs` lines 116–120 executes `dbContext.Database.Migrate()` on application boot. If uncleaned duplicate data exists in MariaDB, the container crashes in a fatal restart loop.
   - **CRITICAL / P0-3: Silent Fallback to Production Connection String:** `Program.cs` line 66 falls back to `appsettings.json` if `DB_CONNECTION_STRING` is unset, risking inadvertent mutation of production from staging.
   - **HIGH / P0-4: 5 Pending Remote Database Migrations:** Migrations 5 through 9 have never been applied to the remote MariaDB database. Unique indexes on `StudySessions`, `HabitCompletions`, and `RoomParticipants` require preflight data verification.
   - **HIGH / P0-5: In-Process Concurrency Barrier:** Concurrency serialization relies on `UserConcurrencyLock` (`SemaphoreSlim`), which is strictly in-process. Production must be pinned to `replicas: 1` until distributed locking or optimistic concurrency tokens are implemented.
   - **HIGH / P0-6: Missing Health Checks:** Zero health check endpoints exist (`/health`, `/healthz`), preventing load balancers and container orchestrators from validating service availability.

---

### 2. Backend Capability Summary

| Area | Implemented Capabilities | Baseline Architecture |
|---|---|---|
| **Authentication & Profile** | PBKDF2 password hashing with salt; JWT issuance (7-day validity); registration; login; current user query (`GET /api/auth/me`); authenticated display name update (`PUT /api/auth/profile`); authenticated password change with current password verification; authenticated account deletion with cascade cleanup; disabled public password reset. | `AuthController`, `AuthRepository`, `PasswordHasher`, `JwtProvider`, `CurrentUserService`. |
| **Daily Tasks** | Full CRUD: create task, list tasks (scoped to user), get task by ID, update title/description, delete task, toggle completion with idempotency and XP awards. | `TasksController`, `DailyTask`, MediatR commands/queries/validators. |
| **Habits & Streaks** | Full CRUD: create habit, list habits with dynamic streaks, get habit by ID, update title/description, delete habit, daily toggle with UTC date scoping and idempotency. | `HabitsController`, `Habit`, `HabitCompletion`, `HabitStreakCalculator`. |
| **Study Sessions** | Start session (enforcing single active session constraint), end session (calculates duration via server UTC timestamps, prevents negative duration, marks complete), list sessions, get active session. | `StudySessionsController`, `StudySession`, `StartStudySessionCommand`, `EndStudySessionCommand`. |
| **Focus Analytics** | Aggregated summary (total focus minutes, today/week focus minutes, total/completed/incomplete sessions, average session duration, task completion rate, habit completion rate, active streaks count); daily breakdown by date range; weekly breakdown (past N weeks). | `AnalyticsController`, `GetFocusAnalyticsSummaryQuery`, `GetDailyAnalyticsQuery`, `GetWeeklyAnalyticsQuery`. |
| **Gamification** | Durable XP ledger (`XpTransactions`), quadratic level formula (levels 1–50+), 8-item achievement catalog, idempotent unlock evaluation, ChangeTracker pre-commit inspection, Error 1062 collision recovery, read-only REST APIs (`/summary`, `/achievements`, `/history?limit=20`). | `GamificationController`, `GamificationService`, `AchievementService`, `LevelCalculator`, `AchievementCatalog`. |
| **Virtual Classrooms** | 50 virtual seats per room, deterministic lowest-seat allocation, collision retry on race conditions, 60-second stale participant eviction, heartbeat ping endpoint, leave room endpoint. | `ClassroomsController`, `ClassroomHandlers`, `RoomParticipant`. |

---

### 3. Feature-Completion Matrix

#### Status Classifications:
- **COMPLETE:** Requirements implemented, verified in source code, covered by automated tests.
- **PARTIAL:** Implemented in part, but core behaviors, state transitions, or fields are missing.
- **MISSING:** No meaningful domain, application, or controller implementation exists.
- **DEFECT:** Implementation exists but has an active correctness, data integrity, or logic flaw.
- **NOT REQUIRED FOR MVP:** Future product roadmap scope outside initial release.

#### A. Core MVP Modules

| Feature Module | Status | Important Classes / Endpoints | Satisfied Requirements | Missing Behavior / Defects | Test Coverage | MVP Blocker? |
|---|---|---|---|---|---|---|
| **Registration & Login** | **COMPLETE** | `AuthController`, `RegisterUserHandler`, `LoginUserHandler`, `PasswordHasher` | PBKDF2 hashing, email uniqueness, duplicate rejection (409), invalid credentials (401), JWT issuance. | None. | 20 unit tests in `AuthenticationTests.cs`. | No |
| **Authentication & Token Lifecycle** | **PARTIAL** | `JwtProvider`, `Program.cs` | JWT creation with `Sub`, `Email`, `DisplayName`, 7-day expiration, HMAC-SHA256 signature. | No refresh tokens, no token revocation mechanism, token valid for 7 days even after password change or account deletion. | Verified in `AuthenticationTests.cs`. | No (Acceptable for MVP; P1 for hardening) |
| **User Profile & Account Settings** | **PARTIAL / DEFECT** | `AuthController`, `GetCurrentUserQuery`, `UpdateProfileCommand`, `ChangePasswordCommand`, `DeleteAccountCommand` | Profile query, display name editing, password changing with PBKDF2 check, cascade account deletion. | **DEFECT:** `User.CurrentStreak` is never updated or computed on the User entity; profile screen always displays "0 Days". No profile avatars. | 14 tests in `ProfileManagementTests.cs`, `UserProfileHandlerTests.cs`. | **Yes (P2: Fix `User.CurrentStreak`)** |
| **Onboarding & Study Preferences** | **MISSING** | `frontend/.../onboarding_screen.dart` | Client-side 3-slide introduction with `has_seen_onboarding` local preference. | Completely missing in backend: no preferences entity, no study goals, no target settings. | 0 backend tests (Flutter UI test only). | No (Can rely on client-side onboarding for MVP) |
| **Subjects & Subject Management** | **MISSING** | None | None | No `Subject` entity, no subject tagging on study sessions, tasks, or habits. | 0 tests. | No (Deferred in MVP; sessions use string `Title`) |
| **Study Session Lifecycle** | **PARTIAL** | `StudySessionsController`, `StudySession`, `StartStudySessionCommand`, `EndStudySessionCommand` | Start session, end session, get active session, session history, single active session constraint. | **Missing pause and resume:** Study sessions only transition from active to completed. No pause timestamps or duration subtraction. | 23 tests in `StudySessionHandlerTests.cs`. | No (Continuous timers sufficient for MVP) |
| **Timer & Duration Correctness** | **COMPLETE** | `EndStudySessionCommandHandler`, `StudySession` | Server calculates duration strictly from `UtcNow - StartTime`, cannot be inflated by client, negative duration prevented (`Math.Max(0, ...)`). | Does not account for paused intervals (since pause is not supported). | Covered in `StudySessionHandlerTests.cs`. | No |
| **Daily Goals & Targets** | **MISSING** | None | None | No daily target minutes, no goal completion tracking entity or API. | 0 tests. | No (Future roadmap) |
| **Dashboard Data APIs** | **PARTIAL** | `AnalyticsController`, `StudySessionsController`, `TasksController`, `HabitsController` | Client composes dashboard via `/analytics/summary`, `/studysessions/active`, `/tasks`, and `/habits`. | No single composite `/dashboard` endpoint; requires 4 separate HTTP round-trips from client. | All sub-endpoints tested independently. | No |
| **Daily & Weekly Statistics** | **COMPLETE** | `AnalyticsController`, `GetDailyAnalyticsQuery`, `GetWeeklyAnalyticsQuery` | Contiguous daily breakdown, zero-filled inactive days, rolling weekly aggregation, input range validation. | None. | 22 tests in `AnalyticsTests.cs`. | No |
| **Daily Tasks CRUD** | **COMPLETE** | `TasksController`, `DailyTask`, MediatR Task Commands/Queries | Create, list, get by ID, update, delete, toggle completion, 15 XP award, idempotency. | No task due times, tags, or calendar planner scheduling. | 24 tests in `TaskHandlerTests.cs`. | No |
| **Habit Tracking & Streaks** | **COMPLETE** | `HabitsController`, `Habit`, `HabitCompletion`, `HabitStreakCalculator` | Create, list, get by ID, update, delete, daily toggle, UTC date normalization, dynamic streaks, 20 XP award. | Streak is computed dynamically for habits, but not persisted to `User.CurrentStreak`. | 30 tests in `HabitHandlerTests.cs`. | No |
| **XP, Levels & Achievements** | **COMPLETE** | `GamificationController`, `GamificationService`, `AchievementService`, `LevelCalculator`, `AchievementCatalog` | Durable ledger, quadratic formula ($L=1..50$), 8 catalog achievements, single unit of work commit, duplicate collision handling. | Concurrency protection across multiple server replicas requires distributed locks. | 98 tests across `Gamification*.cs`, `LevelCalculatorTests.cs`, `Achievement*.cs`. | No |

#### B. Existing Additional Features

| Feature Module | Status | Important Classes / Endpoints | Satisfied Requirements | Missing Behavior / Defects | Test Coverage | MVP Blocker? |
|---|---|---|---|---|---|---|
| **Virtual Classrooms** | **PARTIAL** | `ClassroomsController`, `ClassroomHandlers`, `RoomParticipant` | 50 seats per room, deterministic seat allocation, race collision retries, get participants. | **No live media/signaling:** No WebRTC signaling or SFU backend; Flutter uses REST polling every 10s and external links. | 18 tests in `ClassroomHandlerTests.cs`. | No (Sufficient for study lounge MVP) |
| **Classroom Participant Presence** | **COMPLETE** | `ClassroomHandlers` (`PingRoomCommand`, `GetRoomParticipantsQuery`) | Evicts participants inactive for $> 60$ seconds, explicit leave endpoint, user-isolated seat release. | Heartbeat relies on client active loop; background mobile OS sleep can cause premature eviction. | Tested in `ClassroomHandlerTests.cs`. | No |
| **Analytics Endpoints** | **COMPLETE** | `AnalyticsController` (`/summary`, `/daily`, `/weekly`) | Focus minutes, session counts, task rates, habit completions, active streaks, input validation. | None. | 22 tests in `AnalyticsTests.cs`. | No |
| **Gamification APIs** | **COMPLETE** | `GamificationController` (`/summary`, `/achievements`, `/history`) | Level progress fractions, catalog join with user unlocks, newest-first XP history, limit validation (1..100). | None. | 40 tests in `GamificationApiTests.cs`. | No |
| **Account Deletion Cleanup** | **COMPLETE** | `DeleteAccountCommandHandler` | Password verification, explicit deletion of unconstrained `RoomParticipants`, cascade deletion of Tasks, Habits, Sessions, XP, and Achievements. | Does not revoke already issued JWTs (tokens remain valid until 7-day expiration if not checked against DB). | Tested in `ProfileManagementTests.cs`. | No |

#### C. Future / Roadmap Modules (Outside MVP)

| Module | Status | Description / Assessment |
|---|---|---|
| **Exams & Syllabus Management** | **NOT REQUIRED FOR MVP** | No backend models or endpoints. Future phase. |
| **Study Groups & Membership** | **NOT REQUIRED FOR MVP** | No group entities or permissions. Future phase. |
| **Friends & Social Invitations** | **NOT REQUIRED FOR MVP** | No social graph or invite codes. Future phase. |
| **Leaderboards & Challenges** | **NOT REQUIRED FOR MVP** | No global or group leaderboard aggregation queries. Future phase. |
| **SignalR Real-Time Events** | **NOT REQUIRED FOR MVP** | No SignalR hubs or real-time websocket connections in backend. |
| **Push Notifications & Device Tokens**| **NOT REQUIRED FOR MVP** | No FCM / APNs integration or device token storage. |
| **Camera-Study Metadata APIs** | **NOT REQUIRED FOR MVP** | No AI camera presence or posture tracking endpoints. |
| **AI Assistant Backend Integration** | **NOT REQUIRED FOR MVP** | No LLM integration or study prompt endpoints. |
| **Notes & Document Scanning** | **NOT REQUIRED FOR MVP** | No OCR or document upload endpoints. |
| **Subscriptions & In-App Purchases** | **NOT REQUIRED FOR MVP** | No billing, Stripe, or StoreKit integration. Free-tier MVP. |
| **Offline Synchronization Engine** | **NOT REQUIRED FOR MVP** | Synchronization relies on online REST calls. Offline conflict resolution is deferred. |

---

### 4. API Endpoint Inventory and Contract Analysis

The backend exposes **22 total HTTP endpoints** across 7 controllers:

| Method | Route | Controller & Action | Auth | Tenant Isolation | Validation | Success Response | Error Codes | Tests | Flutter Integration Match? |
|---|---|---|---|---|---|---|---|---|---|
| `POST` | `/api/auth/register` | `AuthController.Register` | Anon | N/A | `RegisterRequestValidator` | `200 OK` (`AuthResponse`) | 400, 409, 500 | `AuthenticationTests` | Yes (`auth_repository.dart`) |
| `POST` | `/api/auth/login` | `AuthController.Login` | Anon | N/A | `LoginRequestValidator` | `200 OK` (`AuthResponse`) | 400, 401, 500 | `AuthenticationTests` | Yes (`auth_repository.dart`) |
| `GET` | `/api/auth/me` | `AuthController.GetCurrentUser` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`UserProfileDto`) | 401, 404, 500 | `UserProfileHandlerTests` | Yes (`profile_repository.dart`) |
| `POST` | `/api/auth/reset-password` | `AuthController.ResetPassword` | Anon | N/A | Disabled | None | `400 Bad Request` | `ProfileManagementTests` | Yes (Handled as disabled) |
| `POST` | `/api/auth/change-password` | `AuthController.ChangePassword` | Bearer | `ICurrentUserService.UserId` | `ChangePasswordCommandValidator` | `200 OK` (`{ Message }`) | 400, 401, 404, 500 | `ProfileManagementTests` | Yes (`profile_repository.dart`) |
| `PUT` | `/api/auth/profile` | `AuthController.UpdateProfile` | Bearer | `ICurrentUserService.UserId` | `UpdateProfileCommandValidator` | `200 OK` (`UserProfileDto`) | 400, 401, 404, 500 | `ProfileManagementTests` | Yes (`profile_repository.dart`) |
| `PUT` | `/api/auth/me` | `AuthController.UpdateProfile` | Bearer | `ICurrentUserService.UserId` | `UpdateProfileCommandValidator` | `200 OK` (`UserProfileDto`) | 400, 401, 404, 500 | `ProfileManagementTests` | Yes (Alias route) |
| `DELETE`| `/api/auth/account` | `AuthController.DeleteAccount` | Bearer | `ICurrentUserService.UserId` | `DeleteAccountCommandValidator` | `200 OK` (`{ Message }`) | 400, 401, 404, 500 | `ProfileManagementTests` | Yes (`profile_repository.dart`) |
| `DELETE`| `/api/auth/me` | `AuthController.DeleteAccount` | Bearer | `ICurrentUserService.UserId` | `DeleteAccountCommandValidator` | `200 OK` (`{ Message }`) | 400, 401, 404, 500 | `ProfileManagementTests` | Yes (Alias route) |
| `GET` | `/api/tasks` | `TasksController.GetTasks` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`List<TaskDto>`) | 401, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `POST` | `/api/tasks` | `TasksController.CreateTask` | Bearer | `ICurrentUserService.UserId` | `CreateTaskCommandValidator` | `201 Created` (`{ id }`) | 400, 401, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `GET` | `/api/tasks/{id}` | `TasksController.GetTaskById` | Bearer | `ICurrentUserService.UserId` | Route GUID | `200 OK` (`TaskDto`) | 401, 404, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `PATCH`| `/api/tasks/{id}/toggle` | `TasksController.ToggleTask` | Bearer | `ICurrentUserService.UserId` | Route GUID | `200 OK` (`{ isCompleted }`) | 401, 404, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `PUT` | `/api/tasks/{id}` | `TasksController.UpdateTask` | Bearer | `ICurrentUserService.UserId` | `UpdateTaskCommandValidator` | `200 OK` (`TaskDto`) | 400, 401, 404, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `DELETE`| `/api/tasks/{id}` | `TasksController.DeleteTask` | Bearer | `ICurrentUserService.UserId` | Route GUID | `204 NoContent` | 401, 404, 500 | `TaskHandlerTests` | Yes (`task_repository.dart`) |
| `GET` | `/api/habits` | `HabitsController.GetHabits` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`List<HabitDto>`) | 401, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `POST` | `/api/habits` | `HabitsController.CreateHabit` | Bearer | `ICurrentUserService.UserId` | `CreateHabitCommandValidator` | `200 OK` (`{ Id }`) | 400, 401, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `GET` | `/api/habits/{id}` | `HabitsController.GetHabitById` | Bearer | `ICurrentUserService.UserId` | Route GUID | `200 OK` (`HabitDto`) | 401, 404, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `POST` | `/api/habits/{id}/toggle` | `HabitsController.ToggleHabit` | Bearer | `ICurrentUserService.UserId` | Route GUID | `200 OK` (`{ IsCompletedToday }`) | 400, 401, 404, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `PUT` | `/api/habits/{id}` | `HabitsController.UpdateHabit` | Bearer | `ICurrentUserService.UserId` | `UpdateHabitCommandValidator` | `200 OK` (`HabitDto`) | 400, 401, 404, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `DELETE`| `/api/habits/{id}` | `HabitsController.DeleteHabit` | Bearer | `ICurrentUserService.UserId` | Route GUID | `204 NoContent` | 401, 404, 500 | `HabitHandlerTests` | Yes (`habit_repository.dart`) |
| `GET` | `/api/studysessions` | `StudySessionsController.GetStudySessions` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`List<StudySessionDto>`) | 401, 500 | `StudySessionHandlerTests` | Yes (`study_session_repository.dart`) |
| `GET` | `/api/studysessions/active` | `StudySessionsController.GetActiveStudySession` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` or `204 NoContent` | 401, 500 | `StudySessionHandlerTests` | Yes (`study_session_repository.dart`) |
| `POST` | `/api/studysessions/start` | `StudySessionsController.StartSession` | Bearer | `ICurrentUserService.UserId` | `StartStudySessionCommandValidator` | `200 OK` (`StudySessionDto`) | 400, 401, 409, 500 | `StudySessionHandlerTests` | Yes (`study_session_repository.dart`) |
| `POST` | `/api/studysessions/{id}/end` | `StudySessionsController.EndSession` | Bearer | `ICurrentUserService.UserId` | Route GUID | `200 OK` (`StudySessionDto`) | 400, 401, 404, 500 | `StudySessionHandlerTests` | Yes (`study_session_repository.dart`) |
| `GET` | `/api/analytics/summary` | `AnalyticsController.GetSummary` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`FocusAnalyticsSummaryDto`) | 401, 500 | `AnalyticsTests` | Yes (`analytics_repository.dart`) |
| `GET` | `/api/analytics/daily` | `AnalyticsController.GetDaily` | Bearer | `ICurrentUserService.UserId` | `GetDailyAnalyticsQueryValidator` | `200 OK` (`DailyAnalyticsDto`) | 400, 401, 500 | `AnalyticsTests` | Yes (`analytics_repository.dart`) |
| `GET` | `/api/analytics/weekly` | `AnalyticsController.GetWeekly` | Bearer | `ICurrentUserService.UserId` | `GetWeeklyAnalyticsQueryValidator` | `200 OK` (`WeeklyAnalyticsDto`) | 400, 401, 500 | `AnalyticsTests` | Yes (`analytics_repository.dart`) |
| `GET` | `/api/gamification/summary` | `GamificationController.GetSummary` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`GamificationSummaryDto`) | 401, 404, 500 | `GamificationApiTests` | Yes (`gamification_repository.dart`) |
| `GET` | `/api/gamification/achievements` | `GamificationController.GetAchievements` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` (`List<AchievementDto>`) | 401, 404, 500 | `GamificationApiTests` | Yes (`gamification_repository.dart`) |
| `GET` | `/api/gamification/history` | `GamificationController.GetHistory` | Bearer | `ICurrentUserService.UserId` | `GetXpHistoryQueryValidator` (limit 1..100) | `200 OK` (`List<XpHistoryDto>`) | 400, 401, 404, 500 | `GamificationApiTests` | Yes (`gamification_repository.dart`) |
| `GET` | `/api/classrooms/{room}/participants`| `ClassroomsController.GetParticipants` | Bearer | N/A (Room public) | Route string | `200 OK` (`List<RoomParticipantDto>`) | 400, 401, 500 | `ClassroomHandlerTests` | Yes (`classroom_repository.dart`) |
| `POST` | `/api/classrooms/{room}/join` | `ClassroomsController.Join` | Bearer | `ICurrentUserService.UserId` | `JoinRoomCommandValidator` | `200 OK` (`RoomParticipantDto`) | 400, 401, 409, 500 | `ClassroomHandlerTests` | Yes (`classroom_repository.dart`) |
| `POST` | `/api/classrooms/{room}/ping` | `ClassroomsController.Ping` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` | 400, 401, 500 | `ClassroomHandlerTests` | Yes (`classroom_repository.dart`) |
| `POST` | `/api/classrooms/{room}/leave` | `ClassroomsController.Leave` | Bearer | `ICurrentUserService.UserId` | None | `200 OK` | 400, 401, 500 | `ClassroomHandlerTests` | Yes (`classroom_repository.dart`) |

#### Contract Consistency Gaps Identified:
1. **HTTP Verb Inconsistency on Toggles:** Daily Tasks uses `PATCH /api/tasks/{id}/toggle`, whereas Habits uses `POST /api/habits/{id}/toggle`. Flutter repositories match each route individually, so functionality works, but the REST semantics differ.
2. **Success Status Code Inconsistency on Creation:** Tasks `POST /api/tasks` returns `201 Created` with `{ id: ... }`, whereas Habits `POST /api/habits` returns `200 OK` with `{ Id: ... }`.
3. **Property Casing in Responses:** Tasks returns camelCase (`{ id, isCompleted }`), whereas Habits toggle returns PascalCase (`{ IsCompletedToday }`). Both are safely handled in Dart via explicit JSON keys, but contracts are asymmetric.

---

### 5. Application Security Review

| Severity | Vulnerability / Finding | Impact | Evidence in Code | Remediation Requirement |
|---|---|---|---|---|
| **CRITICAL** | **Plaintext Credentials & Secrets in Source Control** | Database credentials (`DefaultConnection`) and JWT secret (`Jwt:Secret`) are committed directly to `appsettings.json`. If the repository is exposed, the database and token signature are compromised. | `backend/MOT.Api/appsettings.json` lines 10 & 13. | Remove credentials from `appsettings.json`. Inject via environment variables (`DB_CONNECTION_STRING`, `JWT_SECRET`). |
| **HIGH** | **Missing Rate Limiting on Authentication Endpoints** | `POST /api/auth/login` and `POST /api/auth/register` have no rate limiting. Attackers can execute automated brute-force password guessing and credential stuffing attacks. | `Program.cs` contains no `AddRateLimiter` or rate-limiting middleware. | Implement ASP.NET Core 8 RateLimiter (`SlidingWindowLimiter` or `FixedWindowLimiter`) on auth endpoints. |
| **HIGH** | **Stateless JWT with No Revocation on Password Change or Account Deletion** | Tokens have a fixed 7-day expiration. If a user changes their password or deletes their account, an existing token remains cryptographically valid until expiration. | `JwtProvider.cs` lines 45–48 (`expires: DateTime.UtcNow.AddDays(7)`). No token blacklist or user security stamp verification. | Implement token versioning (`SecurityStamp` on `User`) or token revocation store. (Mitigated in part because DB queries fail on deleted users). |
| **HIGH** | **Unconditional Startup DDL Migration Execution** | `dbContext.Database.Migrate()` runs automatically during `Program.cs` startup. A crash during migration halts web container boot in a restart loop. | `Program.cs` lines 116–120. | Gate `Database.Migrate()` behind `APPLY_MIGRATIONS_ON_STARTUP=true` or separate into a pre-deployment migration job. |
| **MEDIUM** | **Overly Permissive CORS Configuration** | `AllowAll` CORS policy combined with manual wildcard headers (`Access-Control-Allow-Origin: *`) allows any web client to issue cross-origin requests. | `Program.cs` lines 57–63 and custom middleware lines 126–140. | Restrict origins in production to configured frontend domain URLs. |
| **MEDIUM** | **In-Process Concurrency Serialization Limits Horizontal Scaling** | `UserConcurrencyLock` uses in-process `ConcurrentDictionary<Guid, SemaphoreSlim>`. Across multiple server instances, concurrent awards for different activities can cause lost updates on `User.XP`. | `UserConcurrencyLock.cs` lines 10–17. | Enforce `replicas: 1` in deployment. For multi-replica scaling, implement distributed locks (`GET_LOCK`) or atomic SQL increments. |
| **LOW** | **Missing Forwarded Headers Middleware** | `app.UseHttpsRedirection()` is active without `UseForwardedHeaders()`. When deployed behind reverse proxies (Render, AWS ALB, Cloudflare), requests can fail or trigger redirect loops. | `Program.cs` line 122. | Add `app.UseForwardedHeaders()` with `X-Forwarded-Proto`. |
| **LOW** | **Disabled Password Recovery** | `POST /api/auth/reset-password` unconditionally returns 400. While secure against user enumeration, legitimate users cannot reset forgotten passwords without administrator intervention. | `AuthController.cs` lines 98–108. | Implement secure email-based tokenized password recovery in future phase. |

---

### 6. Study-Session Correctness Audit

- **Resource Ownership Enforcement:** Handlers strictly query `s.UserId == userId`. Accessing another user's session throws `StudySessionNotFoundException` (HTTP 404 Not Found), preventing enumeration.
- **State Transition Integrity:** Only two states exist: Active (`!IsCompleted && EndTime == null`) and Completed (`IsCompleted && EndTime != null`). Invalid transitions (e.g. attempting to start when an active session already exists) are rejected with `ActiveStudySessionAlreadyExistsException` (HTTP 409 Conflict).
- **Idempotency on Repeated End Calls:** `EndStudySessionCommandHandler` checks `if (!session.IsCompleted)`. Calling end on an already completed session safely returns the existing completed DTO without altering timestamps or duplicating XP awards.
- **Duration Non-Inflation & Server Authority:** Duration is strictly calculated server-side:
  `session.DurationMinutes = Math.Max(0, (int)(session.EndTime.Value - session.StartTime).TotalMinutes);`
  Client requests cannot supply, override, or inflate duration. Negative durations are clamped to 0.
- **Database-Level Active Session Uniqueness:** Migration `20261012110919_FixStudySessionActiveUserIdColumn` enforces a filtered unique index `IX_StudySessions_ActiveUserId` on `(ActiveUserId)`. If an active session exists, `ActiveUserId` equals `UserId`; when completed, `ActiveUserId` is set to `NULL`. MariaDB allows multiple `NULL` values while enforcing uniqueness for non-null active user IDs.
- **UTC Date Boundaries & Parity:** All timestamps (`StartTime`, `EndTime`) use `DateTime.UtcNow`. Analytics queries consistently filter by `s.StartTime >= todayUtc && s.StartTime < tomorrowUtc`.
- **Incomplete Sessions Handling:** Incomplete sessions (`!s.IsCompleted || s.EndTime == null`) earn 0 XP (`XpAwardResult.NotEligible`) and are cleanly excluded from `CompletedSessionsCount` in Focus Analytics.
- **Client Recovery after App Restart / Network Drop:** Flutter `TimerScreen` invokes `GET /api/studysessions/active` on initialization. If an active session is returned, the client timer calculates elapsed time from server `startTime` (`DateTime.now().difference(serverStartTime)`) and resumes countdown seamlessly without state loss.

---

### 7. Database and Migration Readiness

#### Migration Inventory and Classification

| Order | Migration File | Status | Schema Alterations | Breaking / Rollback Risks |
|---|---|---|---|---|
| **1** | `20260909161703_AddHabits.cs` | **Applied** | Creates `Habits`, `HabitCompletions` | None (historical baseline). |
| **2** | `20261002081502_AddStudySessions.cs` | **Applied** | Creates `StudySessions` with FK to `Users` | None (historical baseline). |
| **3** | `20261006061237_AddRoomParticipants.cs` | **Applied** | Creates `RoomParticipants` | None (historical baseline). |
| **4** | `20261006064106_AddSingleActiveStudySessionConstraint.cs` | **Applied** | Initial constraint attempt on `ActiveUserId` | Superseded by Migration 5. |
| **5** | `20261012110919_FixStudySessionActiveUserIdColumn.cs` | **PENDING** | Alters `ActiveUserId` to `char(36)`, backfills active sessions, creates unique index `IX_StudySessions_ActiveUserId`. | **HIGH:** Fails if any user has $\ge 2$ uncompleted sessions (`IsCompleted = 0 AND EndTime IS NULL`). |
| **6** | `20261012121819_AddRoomParticipantConstraints.cs` | **PENDING** | Alters `RoomName`, `Username` to `varchar(100)`, creates composite unique indexes on `(RoomName, SeatNumber)` and `(RoomName, UserId)`. | **LOW/MED:** Fails if room/user names $> 100$ characters or duplicate seats exist. |
| **7** | `20261012175608_AddUniqueIndexToHabitCompletions.cs` | **PENDING** | Creates unique index `IX_HabitCompletions_HabitId_Date`, drops redundant index `IX_HabitCompletions_HabitId`. | **HIGH:** Fails if duplicate habit completions exist for the same habit on the same date. |
| **8** | `20261013113448_AddXpTransactionsLedger.cs` | **PENDING** | Creates table `XpTransactions`, FK to `Users` (Cascade), unique index on `(UserId, SourceType, SourceId)`. | **CRITICAL ROLLBACK RISK:** `Down()` drops table, permanently destroying user XP history. |
| **9** | `20261013145328_AddUserAchievementsTable.cs` | **PENDING** | Creates table `UserAchievements`, FK to `Users` (Cascade), unique index on `(UserId, AchievementCode)`. | **CRITICAL ROLLBACK RISK:** `Down()` drops table, permanently destroying unlocked achievements. |

- **Model Snapshot Parity:** `dotnet ef migrations has-pending-model-changes` verified **0 pending model changes**.
- **Rollback Hazard:** Migrations 8 and 9 cannot be rolled back after go-live without complete loss of gamification history. In MySQL/MariaDB, DDL statements cause an implicit transaction commit. Schema restoration must be performed via full logical backups (`mysqldump`).

---

### 8. Gamification and Analytics Integrity

- **XP Calculation Rules:**
  - Daily Task completion: 15 XP (`GamificationOptions.TaskCompletionXp`).
  - Habit completion: 20 XP (`GamificationOptions.HabitCompletionXp`).
  - Study Session: 0 XP if duration $< 5$ minutes; otherwise $10 + \lfloor\text{duration}/5\rfloor$ XP, capped at 60 XP.
  - Achievements: Catalog rewards of 25, 50, 75, or 100 XP.
- **Idempotency & Duplicate Award Guards:**
  - Tasks: Checks `XpTransactions.AnyAsync(UserId, "TASK", taskId)`. Untoggling and re-checking awards 0 duplicate XP.
  - Habits: Checks `XpTransactions.AnyAsync(UserId, "HABIT", $"{habitId}:{yyyy-MM-dd}")`. Re-toggling on the same date awards 0 duplicate XP.
  - Study Sessions: Completed sessions check `session.IsCompleted == true` and skip awards.
  - Achievements: Unique constraint `IX_UserAchievements_UserId_AchievementCode` prevents double unlocks.
- **Balance Overflow Protection:** Handlers check `(long)user.XP + amount > int.MaxValue` before incrementing, throwing `OverflowException` rather than rolling over.
- **Atomicity via Single Unit of Work:** The activity status update, XP ledger row, user XP increment, user level update, and achievement rows are staged in the same ChangeTracker and committed atomically via a single `_context.SaveChangesAsync()` call.
- **Concurrency & MariaDB Error 1062 Recovery:** Handlers catch `DbUpdateException` with Error 1062, detach unpersisted entities, reload `User`, and return safely without duplicating XP.
- **Identified Defect — `User.CurrentStreak` Unpopulated:**
  - `User.cs` defines `public int CurrentStreak { get; set; } = 0;`.
  - While `HabitStreakCalculator` dynamically computes streaks per habit in `GetHabitsQuery` and `Analytics`, **`User.CurrentStreak` is never updated anywhere in the backend.**
  - `GET /api/auth/me` returns `user.CurrentStreak`, causing the Flutter Profile screen to permanently display "0 Days".

---

### 9. Test Suite Quality and Verification

#### Test Execution Summary:
```
Test run for MOT.Tests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 462, Skipped: 0, Total: 462, Duration: 5 s
```

#### Test Architecture and Coverage Analysis:

| Test Class | Tests | Type | Coverage Scope |
|---|---|---|---|
| `AuthenticationTests.cs` | 20 | Mocked Unit | Registration, login, password hashing, invalid credentials, duplicate email. |
| `ProfileManagementTests.cs` | 14 | Mocked Unit | Change password, update profile, account deletion, password verification. |
| `UserProfileHandlerTests.cs`| 6 | Mocked Unit | User profile query, total focus minutes calculation. |
| `TaskHandlerTests.cs` | 24 | Mocked Unit | Tasks CRUD, completion toggling, user isolation, XP award integration. |
| `HabitHandlerTests.cs` | 30 | Mocked Unit | Habits CRUD, daily toggling, streak calculation, XP award integration. |
| `StudySessionHandlerTests.cs`| 23 | Mocked Unit | Start/end sessions, active session query, duration calculation, XP integration. |
| `AnalyticsTests.cs` | 22 | Mocked Unit | Focus summary, daily contiguous breakdown, weekly intervals, input validation. |
| `ClassroomHandlerTests.cs` | 18 | Mocked Unit | Join room, seat assignment (0..49), stale participant eviction, ping, leave. |
| `AchievementCatalogTests.cs`| 6 | Domain Unit | Catalog count (8), definitions, XP reward sum (400 XP). |
| `AchievementServiceTests.cs`| 24 | Mocked Unit | Achievement evaluation across sessions, tasks, habits, idempotency, Error 1062. |
| `GamificationServiceTests.cs`| 23 | Mocked Unit | XP formulas, award rules, eligibility, ledger insertion, level calculation. |
| `GamificationApiTests.cs` | 40 | Mocked Unit | API summaries, achievement lists, history queries, limit validation, scoping. |
| `LevelCalculatorTests.cs` | 207 | Domain Unit | Quadratic formulas, thresholds, levels 1–50, boundary conditions. |
| `XpLedgerTests.cs` | 6 | Mocked Unit | Ledger idempotency, duplicate prevention, overflow guard. |

#### Testing Gaps and Blindspots:
1. **Zero Real Database Integration Tests:** All 462 tests run against in-memory mocks (`MockDbSetHelper`) or SQLite/in-memory structures. There are **no tests executed against a real MariaDB 10.11 instance**. Real MariaDB Error 1062 regex, collations, and foreign key cascade behaviors are unverified in automated CI.
2. **Zero End-to-End HTTP Tests (`WebApplicationFactory`):** Tests invoke command handlers and controllers directly via C# method calls. ASP.NET Core middleware (CORS, JWT bearer authentication, routing, JSON serialization) is not tested end-to-end.
3. **Multi-Instance Concurrency Tests:** Tests verify concurrency only within a single process via mocked `SemaphoreSlim`. True distributed race conditions across multiple processes are unverified.

---

### 10. Deployment and Operational Readiness

| Operational Area | Current Status | Assessment & Risk |
|---|---|---|
| **Startup Behavior** | **UNGUARDED** | Runs `Database.Migrate()` unconditionally. Crash on migration failure terminates container in restart loop. |
| **Connection String Fallback** | **HAZARDOUS** | Falls back to production host in `appsettings.json` if `DB_CONNECTION_STRING` is unset. |
| **Health Checks** | **MISSING** | No `/health` or `/healthz` endpoints. Orchestrators cannot monitor readiness. |
| **Logging** | **OPERATIONAL** | Serilog configured with Console sink and configuration enrichment. Safe exception masking prevents client leaks. |
| **Container Runtime** | **OPERATIONAL** | Multi-stage Dockerfile (`mcr.microsoft.com/dotnet/aspnet:8.0`) listening on configurable `PORT` (default 5000). |
| **CI/CD Automation** | **PARTIAL** | GitHub Actions (`backend.yml`) builds and tests on push/PR to `main`. Does not validate database migrations or deploy. |
| **Instance Scaling Policy**| **STRICT SINGLE-INSTANCE** | Architecture requires `replicas: 1`. Multi-replica auto-scaling is prohibited due to in-memory `UserConcurrencyLock`. |
| **Database Provider Compatibility** | **RESOLVED** | Configured for `ServerVersion.Parse("10.11.18-mariadb")` via Pomelo provider. |

---

### 11. Prioritized Action Plan

#### P0 — Must Fix Before Any Staging / Production Deployment
1. **Decouple Migrations from Startup (`Program.cs`):** Gate `Database.Migrate()` behind `APPLY_MIGRATIONS_ON_STARTUP=true` (defaulting to `false`) so containers boot safely without executing DDL.
2. **Prevent Cross-Environment Targeting (`Program.cs`):** Require `DB_CONNECTION_STRING` from the environment in non-development environments; throw a fatal startup exception if missing rather than falling back to `appsettings.json`.
3. **Purge Secrets from Source Control (`appsettings.json`):** Replace database credentials and JWT secret in `appsettings.json` with empty placeholder tokens; inject via secure environment variables in deployment environments.
4. **Implement Health Check Endpoints (`Program.cs`):** Add `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` to support load balancer probes.
5. **Execute Read-Only Preflight SQL on Staging:** Run Preflight Queries 1–6 against the target MariaDB database to confirm 0 data collisions before applying pending migrations.
6. **Enforce Single Container Replica:** Configure hosting environment (Render, ECS, or Kubernetes) to strictly run `replicas: 1`.

#### P1 — Must Fix Before Production Release
1. **Add Rate Limiting on Auth Routes:** Configure ASP.NET Core `RateLimiter` on `/api/auth/login` and `/api/auth/register` (e.g. 10 requests per minute per IP).
2. **Tighten CORS Policies:** Replace `AllowAll` wildcard with explicit allowed origins configured via environment variables.
3. **Add Forwarded Headers Middleware:** Enable `app.UseForwardedHeaders()` with `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`.
4. **Add Real MariaDB Integration Tests in CI:** Implement a test suite using Testcontainers MariaDB to verify real foreign keys, unique constraints, and Error 1062 collision recovery.

#### P2 — Required to Complete Core MVP
1. **Fix `User.CurrentStreak` Defect:** When habits are completed, calculate the user's active streak across habits using `HabitStreakCalculator` and persist it to `User.CurrentStreak`, or calculate it dynamically in `GetCurrentUserQueryHandler`.
2. **Standardize API Route Conventions:** Align `PATCH /api/tasks/{id}/toggle` and `POST /api/habits/{id}/toggle` to consistent HTTP verbs and response casing.

#### P3 — Future Roadmap Features (Post-MVP)
1. **Subjects & Subject Tagging:** Add `Subject` entity and associate with tasks and study sessions.
2. **Study Session Pause / Resume:** Completed in Phase 9 (see section below).
3. **Token Revocation & Refresh Tokens:** Implement refresh token rotation and security stamp invalidation.
4. **Distributed Concurrency:** Replace in-process `UserConcurrencyLock` with distributed locks or optimistic row versions for horizontal scaling.
5. **Classroom WebRTC / SFU:** Replace REST polling with SignalR real-time hubs or LiveKit media servers.

---

### 12. Verification & Closure Checklist

| Item | Status | Evidence Needed to Close |
|---|---|---|
| **Architecture & Dependency Boundaries** | **CLOSED** | Clean Architecture validated; no circular dependencies; build succeeds with 0 errors. |
| **Authentication & Password Security** | **CLOSED** | PBKDF2 hashing verified; uniform 401/409 responses proven in `AuthenticationTests.cs`. |
| **Tenant Isolation & Ownership** | **CLOSED** | All endpoints enforce `ICurrentUserService.UserId`; cross-tenant access returns 404. |
| **Tasks & Habits CRUD & XP Integration** | **CLOSED** | Handlers verified in `TaskHandlerTests.cs` and `HabitHandlerTests.cs`; XP awards proven. |
| **Study Sessions Timer Correctness** | **CLOSED** | Server-calculated duration verified; client inflation prevented; unique active session index proven. |
| **Gamification Ledger & Achievements** | **CLOSED** | 8 catalog items, level formulas, and collision handling proven in 98 gamification tests. |
| **EF Core Model Synchronization** | **CLOSED** | `dotnet ef migrations has-pending-model-changes` returns 0 pending model changes. |
| **User Streak Display Defect** | **OPEN (P2)** | Requires computing and populating `User.CurrentStreak` in `GetCurrentUserQueryHandler`. |
| **Startup Migration Decoupling** | **OPEN (P0)** | Requires editing `Program.cs` to gate `Database.Migrate()` and verify local build. |
| **Secrets Purging from `appsettings.json`**| **OPEN (P0)** | Requires replacing sensitive values with placeholders and injecting via environment. |
| **Health Check Implementation** | **OPEN (P0)** | Requires adding `AddHealthChecks()` and `MapHealthChecks("/health")` in `Program.cs`. |
| **Remote Database Preflight Execution** | **OPEN (P0)** | Requires human operator to execute Preflight Queries 1–6 against staging MariaDB. |
| **Pending Migrations Application** | **OPEN (P0)** | Requires operator to execute `dotnet ef database update` on staging after preflight review. |

---

### 13. Concise Final Answer

> **"Is the backend ready, and what exactly must happen next?"**
>
> **The backend is NOT YET READY for production, but it IS ARCHITECTURALLY READY FOR CONTROLLED STAGING DEPLOYMENT, subject to completing 4 immediate code guardrails:**
>
> 1. **Immediate Code Actions (P0):**
>    - In `Program.cs`: Gate `Database.Migrate()` behind `APPLY_MIGRATIONS_ON_STARTUP=true`, forbid fallback to production credentials in non-development environments, and add the `/health` endpoint.
>    - In `appsettings.json`: Purge credentials and JWT secret, replacing them with empty configuration placeholders.
>    - In `GetCurrentUserQueryHandler.cs`: Populate `User.CurrentStreak` dynamically from `HabitStreakCalculator` so the Flutter Profile screen displays accurate streaks.
> 2. **Immediate Operational Actions (P0):**
>    - Operator executes the 6 Read-Only SQL Preflight Queries against the staging database to verify zero data anomalies.
>    - Operator applies the 5 pending migrations to the staging MariaDB database.
>    - Deploy a single container instance (`replicas: 1`) to staging with `APPLY_MIGRATIONS_ON_STARTUP=false`.
>    - Run end-to-end smoke verification using the Flutter application against the staging API.

---

## Phase 9 — Complete Study Session Lifecycle

**Status: COMPLETED / VERIFIED**

Phase 9 completes the study session lifecycle in Mind On Track (MOT) by implementing pause, resume, precise active duration accounting (excluding paused intervals from focus time and XP awards), robust session-state validation, and full Flutter client integration.

### 1. Domain & Persistence Architecture
- **Entity Enhancements (`StudySession.cs`):**
  - `IsPaused` (bool, default `false`): Tracks whether the active session is currently paused.
  - `LastPausedAt` (DateTime?, null): UTC timestamp when the session was most recently paused.
  - `LastResumedAt` (DateTime?, null): UTC timestamp when the session was started or last resumed.
  - `AccumulatedSeconds` (int, default `0`): Server-authoritative running tally of focused seconds accrued across completed focus intervals.
- **Model Configuration (`AppDbContext.cs`):**
  - Configured `IsPaused` default `false` and `AccumulatedSeconds` default `0` in `OnModelCreating`.
  - Preserved filtered unique index `IX_StudySessions_ActiveUserId` on `(ActiveUserId)` where `ActiveUserId IS NOT NULL`.
  - Scaffolded EF Core migration `20261014095555_AddStudySessionPauseAndResume`.
  - `dotnet ef migrations has-pending-model-changes` verified: **0 pending model changes**.
  - **Zero remote database connections, queries, or migration executions.**

### 2. Precise Duration Accounting
- **Active Focus Duration Formula:**
  - While running (`!IsPaused`): `ElapsedSeconds = AccumulatedSeconds + (int)(DateTime.UtcNow - LastResumedAt).TotalSeconds`.
  - While paused (`IsPaused`): `ElapsedSeconds = AccumulatedSeconds`.
  - Paused periods are strictly excluded from `AccumulatedSeconds`.
  - On `EndStudySessionCommand`:
    - If running, remaining active interval is accumulated into `AccumulatedSeconds`.
    - If paused, `AccumulatedSeconds` is used directly without adding paused time.
    - Final `DurationMinutes = AccumulatedSeconds / 60`.
- **Gamification & Analytics Parity:**
  - Gamification XP awards and achievement evaluation evaluate against the accurate `DurationMinutes` (excluding paused time).
  - All existing focus analytics queries sum `DurationMinutes`, preserving zero-defect consistency across daily/weekly reports without requiring query modifications.

### 3. Application CQRS & API Endpoints
- **Pause Command (`PauseStudySessionCommand.cs`, `PauseStudySessionCommandValidator.cs`):**
  - Validates user ownership and non-empty GUID.
  - Acquires per-user concurrency lock (`UserConcurrencyLock`).
  - Throws `InvalidStudySessionStateException` if session is completed (`400 Bad Request`).
  - Idempotent if already paused.
  - Accumulates active seconds since `LastResumedAt` into `AccumulatedSeconds`, sets `IsPaused = true`, `LastPausedAt = UtcNow`, `LastResumedAt = null`.
- **Resume Command (`ResumeStudySessionCommand.cs`, `ResumeStudySessionCommandValidator.cs`):**
  - Validates user ownership and non-empty GUID.
  - Acquires per-user concurrency lock (`UserConcurrencyLock`).
  - Throws `InvalidStudySessionStateException` if session is completed (`400 Bad Request`).
  - Idempotent if already running.
  - Sets `IsPaused = false`, `LastResumedAt = UtcNow`, `LastPausedAt = null`.
- **DTO Extensions (`StudySessionExtensions.cs`):**
  - Maps `IsPaused`, `LastPausedAt`, `AccumulatedSeconds`, and dynamically computes server-authoritative `ElapsedSeconds` for running and paused sessions.
- **API Controller (`StudySessionsController.cs`):**
  - Exposed `POST /api/studysessions/{id}/pause`
  - Exposed `POST /api/studysessions/{id}/resume`
  - Handled `InvalidStudySessionStateException` mapped to HTTP 400 Bad Request.

### 4. Flutter Integration
- **Model (`study_session_model.dart`):**
  - Added `isPaused`, `lastPausedAt`, `accumulatedSeconds`, `elapsedSeconds`, and `toJson()`.
  - Implemented safe fallbacks for legacy payloads.
- **Repository (`study_session_repository.dart`):**
  - Added `Future<StudySession> pauseSession(String sessionId)` calling `POST studysessions/{id}/pause`.
  - Added `Future<StudySession> resumeSession(String sessionId)` calling `POST studysessions/{id}/resume`.
- **UI (`timer_screen.dart`):**
  - Integrated server-authoritative pause/resume actions with reactive button icons (`Icons.pause_circle_filled` / `Icons.play_circle_filled`).
  - Added "FOCUSING" vs "PAUSED" visual status pill and border color transition.
  - Added action loading guards (`_isActionLoading`) to prevent race conditions from repeated button clicks.
  - Added `WidgetsBindingObserver` to re-synchronize elapsed timer state upon `AppLifecycleState.resumed` (app minimized/restored).
  - Wrapped timer layout in `SingleChildScrollView` to prevent viewport overflow across diverse screen sizes.

### 5. Verification & Test Suite
- **Backend Tests:**
  - `MOT.Tests/StudySessions/StudySessionHandlerTests.cs`: 19 comprehensive unit and integration tests covering pause, resume, multi-cycle accumulation, ending while paused, completed session rejection, and HTTP controller responses.
  - Total backend test suite: **481/481 tests passing (100%)**.
- **Frontend Tests:**
  - `frontend/test/study_session_model_test.dart`: Model deserialization, fallback handling, and serialization tests.
  - `frontend/test/study_session_repository_and_timer_screen_test.dart`: Repository pause/resume endpoint tests and TimerScreen widget interaction tests.
  - Total Flutter test suite: **62/62 tests passing (100%)**.
  - `flutter analyze --no-fatal-infos`: **0 errors, 0 warnings**.
- **Migration & Database Safety:**
  - EF Core migration generated: `20261014095555_AddStudySessionPauseAndResume`.
  - Model snapshot verified: 0 pending model changes.
  - Live/remote database untouched; 0 migrations applied.

---

## Phase 10 — Subjects and Daily Study Goals

**Status: COMPLETED / VERIFIED**

Phase 10 completes Subjects and Daily Study Goals across the .NET 8 backend and Flutter frontend, adhering strictly to Clean Architecture, CQRS/MediatR, FluentValidation, Riverpod state management, and multi-tenant isolation.

### 1. Implemented Functionality & Architecture Decisions
- **Subject Management (`Subject.cs`):**
  - Unique subject entity with `UserId`, `Name` (max 100 chars), `Description` (optional max 500 chars), `ColorHex` (optional max 9 chars), `CreatedAt`, and `UpdatedAt`.
  - Normalization policy: subject names are trimmed and validated for case-insensitive uniqueness per user (`_context.Subjects.AnyAsync(s => s.UserId == userId && s.Name.ToLower() == normalizedName.ToLower())`).
  - Referential integrity: optional `SubjectId` added to `StudySession` and `DailyTask` with `DeleteBehavior.SetNull`. Deleting a subject safely nullifies `SubjectId` on associated records, guaranteeing zero data loss for past study sessions, focus metrics, tasks, or XP transactions.
- **Daily Study Goals (`DailyGoal.cs`):**
  - Smallest useful design: Users can customize their daily focus target (5 to 1440 minutes) for any calendar date, falling back to a sensible 60-minute default when unconfigured.
  - Entity contains `UserId`, `Date` (normalized to calendar date at midnight UTC), `TargetMinutes`, `CreatedAt`, and `UpdatedAt`.
  - Unique constraint on `(UserId, Date)`.
  - Duration accounting policy: Actual focused time is calculated strictly from completed study sessions (`IsCompleted == true` and `EndTime != null`), excluding paused intervals (using `DurationMinutes = AccumulatedSeconds / 60`). Incomplete or active sessions are strictly excluded.
  - Timezone policy: Accepts optional `TimeZoneOffsetMinutes` (-840 to +840, defaulting to 0 UTC), adjusting the date range window in UTC so sessions completed near local midnight boundaries are attributed to the correct local calendar day.

### 2. New & Changed API Endpoints
- **Subjects Controller (`api/subjects`):**
  - `GET /api/subjects`: Returns list of authenticated user's subjects ordered by name.
  - `POST /api/subjects`: Creates a new subject; returns HTTP 201 Created with `SubjectDto` (HTTP 400 on duplicate name or invalid length).
  - `GET /api/subjects/{id}`: Returns single `SubjectDto` (HTTP 404 if missing or belongs to another user).
  - `PUT /api/subjects/{id}`: Updates subject details; returns HTTP 200 `SubjectDto` (HTTP 400 on duplicate name, HTTP 404 if unauthorized).
  - `DELETE /api/subjects/{id}`: Safely deletes subject and sets references to null; returns HTTP 204 NoContent.
- **Daily Goals Controller (`api/dailygoals`):**
  - `GET /api/dailygoals/progress?date=yyyy-MM-dd&timeZoneOffsetMinutes=0`: Returns `DailyGoalProgressDto` with `targetMinutes`, `actualMinutes`, `remainingMinutes`, `progressPercentage`, `isAchieved`, and `completedSessionsCount`.
  - `POST /api/dailygoals`: Upserts daily target; returns updated `DailyGoalProgressDto`.

### 3. Schema & EF Core Migrations
- **Generated Migration:** `20261014102030_AddSubjectsAndDailyGoals` (scaffolded locally in `MOT.Infrastructure`).
- **Snapshot Verification:** `dotnet ef migrations has-pending-model-changes` verified: **0 pending model changes**.
- **Database Safety:** Zero remote database connections, queries, or migration executions.

### 4. Flutter Integration
- **Subjects Feature (`frontend/lib/features/subjects/`):**
  - Model: `Subject` with defensive null-safe parsing and `toJson()`.
  - Repository: `SubjectRepository` with full CRUD operations.
  - Provider: `subjectsProvider` (`FutureProvider<List<Subject>>`).
  - Screen: `SubjectsScreen` with subject list cards, color indicators, empty states, add/edit bottom sheets, and delete confirmation dialogs.
- **Daily Goals Feature (`frontend/lib/features/daily_goals/`):**
  - Model: `DailyGoalProgress` with progress calculation, achievement state, and session counts.
  - Repository: `DailyGoalRepository` with `getProgress` and `setGoal`.
  - Provider: `dailyGoalProgressProvider(date)` (`FutureProvider.family`).
  - Screen: `DailyGoalsScreen` featuring date navigation, circular progress ring with status pills, target/focused/remaining metrics tiles, and an adjust target bottom sheet with quick preset chips (30, 45, 60, 90, 120, 180 mins).
- **Navigation Integration:**
  - `ProfileScreen`: Added navigation cards for "Daily Study Goal" and "Manage Subjects".
  - `TimerScreen`: Added action bar button directly opening `DailyGoalsScreen`.

### 5. Verification & Test Suite
- **Backend Tests (`MOT.Tests`):**
  - `SubjectHandlerTests.cs`: 12 tests covering CRUD, case-insensitive duplicate name rejection, tenant isolation, length limits, hex color validation, and HTTP status codes.
  - `DailyGoalHandlerTests.cs`: 8 tests covering default targets, progress calculation from completed sessions, exclusion of paused/incomplete sessions, timezone boundary shifts, upsert behavior, and validation.
  - Total backend tests: **501 / 501 passed (100%)**.
- **Frontend Tests (`frontend/test`):**
  - `subject_model_and_repository_test.dart`: 8 tests covering JSON serialization, deserialization fallbacks, repository endpoints, and duplicate error extraction.
  - `daily_goal_model_and_repository_test.dart`: 6 tests covering model parsing, fallbacks, repository query parameters, and validation error extraction.
  - `subjects_and_daily_goals_ui_test.dart`: 5 widget tests covering empty states, card rendering, add subject sheets, progress ring displays, achievement badges, and target adjustment sheets.
  - Total Flutter tests: **81 / 81 passed (100%)**.
  - `flutter analyze --no-fatal-infos`: **0 errors, 0 warnings**.

### 6. Known Limitations & Follow-Up Work
- Subject selection dropdown during study session creation can be wired directly into `timer_screen.dart` in a future enhancement.
- Historical daily goal progress analytics (e.g. 7-day goal completion streaks) can be added to the analytics dashboard.
- No remote database updates or deployments occurred.

---

## Phase 11 — Dashboard Integration

**Status: COMPLETED / VERIFIED**

Phase 11 integrates all study productivity features into a unified, data-driven, and cohesive Dashboard in both the .NET 8 backend and Flutter frontend, strictly upholding Clean Architecture, CQRS/MediatR, Riverpod state management, and all safety constraints.

### 1. Implemented Architecture & Design Decisions
- **Unified Composite Aggregate Query (`GetDashboardSummaryQuery`):**
  - Prompt requirement: Prevent client round-trip thrashing and data inconsistency resulting from 7+ separate parallel network calls (daily goals, active session, tasks, habits, streak, gamification, subjects, recent sessions, weekly activity).
  - Single, high-performance aggregate endpoint `GET /api/dashboard?timeZoneOffsetMinutes=...` queries indexed user-owned records in a single transactional read.
  - Returns `DashboardSummaryDto` encapsulating:
    1. `DailyGoal`: Today's configured or default (60m) target, actual focused minutes, remaining minutes, progress percentage, achieved status, and completed sessions count.
    2. `ActiveSession`: Accurate elapsed time (`ElapsedSeconds`, `DurationMinutes`), subject name, and pause state (`IsPaused`), or `null` if idle.
    3. `Tasks`: Total tasks count, completed count, pending count, completion rate percentage, and top 3 pending tasks for quick view.
    4. `Habits`: Total habits count, completed today count, completion percentage, and habit list with individual streak counters.
    5. `Streak`: Reliable, deterministic streak calculation computed across all user habit completions via `HabitStreakCalculator.CalculateStreak(allCompletions, targetDate)`, plus calculated `LongestStreak`.
    6. `Gamification`: Current level, total XP, level thresholds, progress fraction/percentage, and unlocked vs total achievements count.
    7. `Subjects`: Full user subject list with custom hex colors.
    8. `RecentSessions`: Top 5 completed study sessions ordered by completion time with duration and subject metadata.
    9. `WeeklyActivity`: 7-day rolling window of daily focus minutes and completed session counts ending on the user's current local date.
- **Accurate Today Focus Time & Daily Goal Parity:**
  - Today's focused minutes are calculated strictly from completed study sessions (`IsCompleted == true` and `EndTime != null`), excluding paused intervals (`DurationMinutes = AccumulatedSeconds / 60`).
  - Active or incomplete sessions are strictly excluded from completed focus time.
  - Client timezone offset adjusts the 24-hour UTC window so sessions completed near local midnight boundaries are attributed to the correct calendar day.
- **Reliable Streak Calculation:**
  - Evaluates consecutive days of habit completions backwards from today (or yesterday if today is not yet completed).
  - Flags `IsActiveToday` so UI clearly indicates whether today's study activity is already recorded.
  - Zero placeholder or hardcoded streak values.
- **Database Safety:**
  - Zero schema changes or new migrations needed; pure aggregation over existing tables.
  - `dotnet ef migrations has-pending-model-changes` verified: **0 pending model changes**.
  - Zero remote database connections or migration commands executed.

### 2. Endpoints Implemented
- `GET /api/dashboard?timeZoneOffsetMinutes={offset}`: Returns `200 OK` with `DashboardSummaryDto`. Authenticated via JWT Bearer (`ICurrentUserService.UserId`).

### 3. Flutter Integration
- **Feature Structure (`frontend/lib/features/dashboard/`):**
  - Models (`data/models/dashboard_summary_model.dart`): `DashboardSummary`, `DashboardTaskSummary`, `DashboardHabitSummary`, `DashboardStreakSummary`, `DashboardDayActivity`.
  - Repository (`data/repositories/dashboard_repository.dart`): `DashboardRepository.getDashboardSummary({int? timeZoneOffsetMinutes})`.
  - Provider (`presentation/providers/dashboard_provider.dart`): `dashboardRepositoryProvider`, `dashboardSummaryProvider` (`FutureProvider.autoDispose`).
  - Screen (`presentation/screens/dashboard_screen.dart`):
    - **Header:** Welcome greeting, XP & Level card with progress bar (tappable to Achievements), and Streak flame badge with active state styling.
    - **Active Session Banner:** Glow card with pulse status ("IN PROGRESS" / "PAUSED"), elapsed timer display, and quick "Open Timer" button.
    - **Daily Study Goal Card:** Circular progress ring, actual vs target focus time, remaining minutes, goal achieved badge, and direct "Configure" link to `DailyGoalsScreen`.
    - **Weekly Activity Chart:** 7-day bar chart showing daily focus minutes with day labels and total weekly focus summary.
    - **Tasks & Habits Grid:** 2-column cards showing today's completion ratio, completion percentage, and one-tap navigation to `TaskDashboardScreen` and `HabitDashboardScreen`.
    - **Subjects Section:** Color-coded subject chips with "+ Manage" shortcut to `SubjectsScreen`.
    - **Recent Sessions Section:** List of recent completed study sessions with duration and timestamps.
    - **Pull-to-Refresh:** Pull down on list invokes `ref.refresh(dashboardSummaryProvider.future)`.
- **Navigation Update (`frontend/lib/core/presentation/screens/home_screen.dart`):**
  - Elevated `DashboardScreen` to primary Tab 0 ("Dashboard", `Icons.dashboard_rounded`).
  - Navigation tabs: `Dashboard` (0), `Tasks` (1), `Habits` (2), `Timer` (3), `Profile` (4).
  - Tapping cards on Dashboard switches tabs or opens feature screens seamlessly.
  - Classroom / Live Study room accessible directly from Dashboard's top bar action button.

### 4. Verification & Test Suite
- **Backend Tests (`MOT.Tests`):**
  - `DashboardHandlerTests.cs`: 5 tests covering:
    - `GetDashboardSummary_WhenUnauthenticated_ThrowsUnauthorized`
    - `GetDashboardSummary_WhenUserNotFound_ThrowsUserNotFound`
    - `GetDashboardSummary_WhenUserHasEmptyData_ReturnsHealthyDefaults`
    - `GetDashboardSummary_WithPopulatedData_ReturnsAccurateMetrics`
    - `DashboardController_GetDashboardSummary_ReturnsOkResult`
  - Total backend tests: **506 / 506 passed (100%)**.
- **Frontend Tests (`frontend/test`):**
  - `dashboard_model_and_repository_test.dart`: 4 tests covering full JSON deserialization, empty fallback resilience, repository network responses, and error handling.
  - `dashboard_screen_test.dart`: 4 widget tests covering loading state with spinner, error state with retry button, full populated UI rendering (header, streak, daily goal, active session banner, weekly activity, tasks, habits, subjects, recent sessions), and tab navigation callbacks.
  - Total Flutter tests: **89 / 89 passed (100%)**.
  - `flutter analyze --no-fatal-infos`: **0 errors, 0 warnings**.

---

## Phase 11A — Dashboard Integration Verification

**Status: COMPLETED / VERIFIED**

Phase 11A performed a comprehensive, evidence-based verification and bug-fixing audit of the Phase 11 Dashboard aggregate backend query and Flutter client integration across all 8 specified scenarios.

### 1. Verification Findings Across Core Scenarios

1. **Daily Goal and Focus Time Parity:**
   - **Verification:** Verified that today's focused minutes in `GetDashboardSummaryQuery` strictly match `/api/daily-goals/today` and `/api/daily-goals/progress`.
   - **Session Filtering:** Only sessions where `IsCompleted == true` and `EndTime != null` contribute to completed focus minutes.
   - **Duration Accounting:** Paused session intervals are strictly excluded; focus time is computed directly as `AccumulatedSeconds / 60`.
   - **Date Boundaries:** Local midnight shifts via `timeZoneOffsetMinutes` are applied uniformly across daily goal progress and today's session aggregation.
   - **Zero-Session Days:** Gracefully returns 0 minutes with 0% progress and healthy target defaults (defaulting to 60 minutes if unset).

2. **Active Study Session & Controls:**
   - **Verification:** Verified that active sessions (`!IsCompleted`) are cleanly isolated from completed metrics and surfaced in `DashboardSummaryDto.ActiveSession`.
   - **Pause State:** `IsPaused` state and elapsed study time (excluding pauses) are accurately reflected.
   - **Idle State:** When no session is active, `ActiveSession` is `null`, safely handled in both backend and frontend without null pointer exceptions.
   - **Navigation:** Tapping the active session banner navigates directly to the Timer screen.

3. **Tasks and Habits Progress Matching:**
   - **Verification:** Tasks total, completed, pending, and completion rate match `/api/tasks` counts exactly.
   - **Habits Alignment Fix:** Identified that `GetDashboardSummaryQuery` previously used `targetDate` (which shifted with timezone offset) when calculating habit streak, whereas `ToggleHabitCompletionCommand` and `GetHabitsQuery` record and evaluate habit completions against `DateTime.UtcNow.Date`. Standardized `GetDashboardSummaryQuery` to use `habitToday = DateTime.UtcNow.Date`, guaranteeing exact parity across all timezones.

4. **Streak Calculation Rules & Presentation:**
   - **Verification:** Verified that the streak calculation relies entirely on habit completion history via `HabitStreakCalculator`.
   - **Streak Gaps:** Tested and verified that missing days break streak continuity as expected.
   - **Clarity Hardening:** Added explicit `Habit Streak` tooltip to the header flame badge (`Habit Streak • Best: X days`) and an explicit subtitle in the Habits card (`Habit Streak: Xd`) so users are not misled into assuming it represents an independent study-session streak.

5. **Weekly Focus Activity & Recent Sessions:**
   - **Verification:** Verified 7-day rolling window ending on the user's local date. Each day's minutes and session counts reflect only completed sessions.
   - **Recent Sessions:** Limited to the 5 most recent completed sessions, sorted descending by end time, with subject names and durations correctly resolved.

6. **Gamification Parity:**
   - **Verification:** Verified that `gamification.level`, `totalXp`, current level floor, next level threshold, and unlocked achievements count perfectly match `/api/gamification/summary` and `/api/gamification/achievements`.

7. **Authentication & Tenant Isolation:**
   - **Verification:** Added multi-tenant isolation tests. Verified that queries reject unauthenticated requests (`401 Unauthorized`) and never leak another user's tasks, habits, sessions, subjects, or goals.

8. **Flutter UX, Responsiveness, & Auto-Refresh:**
   - **Auto-Refresh Fix:** Converted `HomeScreen` to `ConsumerStatefulWidget` and added `ref.invalidate(dashboardSummaryProvider)` when switching tabs to Dashboard, as well as upon returning from pushed sub-screens (`DailyGoalsScreen`, `SubjectsScreen`, `AchievementsScreen`, `TimerScreen`).
   - **Pull-to-Refresh:** Verified that swipe down invokes `ref.refresh(dashboardSummaryProvider.future)` and updates state.
   - **Responsive Layout Hardening:** Hardened `DashboardScreen` against horizontal flex overflows on narrow devices (360x640 px) by wrapping greeting headers, goal titles, active session banners, and weekly activity headers in `Expanded`/`Flexible` with text overflow ellipsis.

### 2. Defects Identified & Resolved

| Defect | Severity | Root Cause | Fix Applied |
| :--- | :--- | :--- | :--- |
| **Habit Streak Date Shift** | Medium | `GetDashboardSummaryQuery` passed timezone-adjusted `targetDate` to habit streak calculator while habits are recorded at UTC date. | Standardized habit streak evaluation in `GetDashboardSummaryQuery.cs` to `DateTime.UtcNow.Date`. |
| **Streak Domain Ambiguity** | Low | Streak flame badge in header could be misinterpreted as a general study streak. | Added explicit tooltip (`Habit Streak • Best: X days`) and habit card subtitle (`Habit Streak: Xd`). |
| **Stale Dashboard State** | Medium | Navigating to sub-screens or switching tabs didn't invalidate `dashboardSummaryProvider`, leaving metrics stale. | Added `ref.invalidate(dashboardSummaryProvider)` on tab switch and after `await Navigator.push(...)` in `home_screen.dart` and `dashboard_screen.dart`. |
| **Narrow Screen Layout Overflow** | Low | Long greeting names or titles could trigger horizontal flex overflow on screens <= 360px wide. | Wrapped text elements in `Expanded`/`Flexible` with `TextOverflow.ellipsis` and fixed graph height. |

### 3. Verification Test Suite & Execution Results

- **Backend Build:**
  - Command: `dotnet build backend/MOT.Api/MOT.Api.csproj`
  - Result: **Build succeeded. 0 Warning(s), 0 Error(s)**.
- **Backend Tests (`backend/MOT.Tests`):**
  - Command: `dotnet test backend/MOT.Tests/MOT.Tests.csproj`
  - Added 4 new regression tests in `DashboardHandlerTests.cs`:
    1. `GetDashboardSummary_ExcludesIncompleteSessionsFromCompletedFocusTime`
    2. `GetDashboardSummary_ActiveSessionPaused_CalculatesCorrectElapsedStudyTime`
    3. `GetDashboardSummary_HabitStreak_DetectsGapsCorrectly`
    4. `GetDashboardSummary_StrictTenantIsolation_DoesNotLeakOtherUserData`
  - Result: **510 passed, 0 failed (100% passing)**.
- **EF Core Model Snapshot:**
  - Command: `dotnet ef migrations has-pending-model-changes --project backend/MOT.Infrastructure/MOT.Infrastructure.csproj --startup-project backend/MOT.Api/MOT.Api.csproj`
  - Result: **No changes have been made to the model since the last migration**.
- **Frontend Code Quality:**
  - Command: `flutter analyze --no-fatal-infos`
  - Result: **0 errors, 0 warnings** (20 non-fatal style infos).
- **Frontend Widget & Unit Tests (`frontend/test`):**
  - Command: `flutter test`
  - Added 3 new regression tests in `dashboard_screen_test.dart`:
    1. Narrow screen rendering without overflow on 360x640 display
    2. Explicit habit streak labeling and tooltip rendering
    3. Pull-to-refresh execution
  - Result: **91 passed, 0 failed (100% passing)**.

### 4. Database & Safety Notice
- **Mock vs. Live Database Note:** All backend unit tests run against EF Core in-memory mocks / lists (`MockDbSetHelper`). They do **not** connect to a live MariaDB database. Real MariaDB integration testing remains a separate preflight requirement before production deployment.
- **Database Safety:** Zero migrations created or executed. Zero remote database connections.


