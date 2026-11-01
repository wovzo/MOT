# Mind On Track (MOT) - Prioritized Development Backlog

**Audit Date:** October 31, 2026  
**Auditor:** Senior Software Architect, Technical Lead & Project Auditor  
**Scope:** Remediation, MVP Completion, and Future Feature Expansion  

---

## 1. Backlog Prioritization Schema

* **P0 — Critical Release Blockers:** Security vulnerabilities, compilation breaks, and core blockers preventing MVP validation.
* **P1 — Core MVP Polish:** Completing missing mobile CRUD workflows, subject integration, and state management consistency.
* **P2 — Version 1.0 Hardening:** Dedicated analytics UI, refresh tokens, offline resilience, and notifications.
* **P3 — Advanced Expansion:** Realtime SignalR classroom, exams & syllabus, leaderboards, and AI assistant.

---

## 2. P0: Critical Release Blockers (Immediate Execution)

### [TASK-001] Fix Flutter SDK `CardTheme` Compilation Error
* **Feature:** Core Mobile UI / Theme
* **Status:** Incomplete / Bug
* **Priority:** **P0**
* **Dependencies:** None
* **Implementation Steps:**
  1. Open `frontend/lib/core/theme.dart`.
  2. Replace `cardTheme: CardTheme(...)` with `cardTheme: CardThemeData(...)` on line 47.
  3. Run `flutter analyze` and `flutter test` to verify zero compile errors.
* **Backend Changes:** None.
* **Flutter Changes:** Change 1 type name in `lib/core/theme.dart`.
* **Database Changes:** None.
* **Testing Requirements:** Verify `dashboard_screen_test.dart` and `gamification_ui_and_provider_test.dart` compile and pass 100%.
* **Estimated Effort:** 0.5 hours.
* **Acceptance Criteria:** `flutter test` completes with 100% tests passing and `flutter analyze` reports 0 errors.

---

### [TASK-002] Sanitize Version-Controlled Secrets & Rotate Credentials
* **Feature:** Application Security & Infrastructure
* **Status:** Incomplete / Vulnerability
* **Priority:** **P0**
* **Dependencies:** Cloud Hosting (Render / Database Admin)
* **Implementation Steps:**
  1. Rotate MariaDB database password on `mysql.gb.stackcp.com`.
  2. Generate a new cryptographically secure 256-bit JWT Secret key.
  3. Rotate SMTP password on Stackmail.
  4. Replace active credentials in `backend/MOT.Api/appsettings.json` with empty placeholder strings (`"DefaultConnection": ""`, `"Secret": ""`).
  5. Configure environment variables in the Render / Docker deployment dashboard.
* **Backend Changes:** Ensure `Program.cs` fails fast with an informative exception if environment variables are not supplied.
* **Flutter Changes:** None.
* **Database Changes:** None (credential rotation only).
* **Testing Requirements:** Run backend test suite; verify local dev works with local `appsettings.Development.json`.
* **Estimated Effort:** 2 hours.
* **Acceptance Criteria:** Zero credentials committed in Git; backend starts successfully when provided environment variables.

---

### [TASK-003] Align EF Core NuGet Package Reference Versions
* **Feature:** Backend Build & Dependencies
* **Status:** Incomplete / Warning
* **Priority:** **P0**
* **Dependencies:** None
* **Implementation Steps:**
  1. Update `backend/MOT.Tests/MOT.Tests.csproj` and `MOT.IntegrationTests/MOT.IntegrationTests.csproj`.
  2. Unify `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational` to matching version `8.0.4`.
  3. Rebuild solution.
* **Backend Changes:** Update `.csproj` PackageReference tags.
* **Flutter Changes:** None.
* **Database Changes:** None.
* **Testing Requirements:** Run `dotnet build backend/MOT.Api/MOT.Api.csproj` and `dotnet test`; ensure warning MSB3277 is resolved.
* **Estimated Effort:** 1 hour.
* **Acceptance Criteria:** 0 build warnings during `dotnet test`.

---

## 3. P1: Core MVP Polish & Integrity (Pre-Launch Sprint)

### [TASK-004] Subject Selection in Study Timer Screen
* **Feature:** Study Timer & Subjects
* **Status:** Partially Complete (Backend Ready, UI Disconnected)
* **Priority:** **P1**
* **Dependencies:** [TASK-001]
* **Implementation Steps:**
  1. Update `frontend/lib/features/timer/data/repositories/study_session_repository.dart` method `startSession(String title, {String? subjectId})` to pass `subjectId` in payload.
  2. Update `timer_screen.dart` to read `subjectsProvider`.
  3. Add a subject selector dropdown/chip list on `TimerScreen` when idle.
  4. Display selected subject badge during active session and send `subjectId` on session start.
* **Backend Changes:** None (`StartStudySessionCommand` already accepts `SubjectId`).
* **Flutter Changes:** Update `StudySessionRepository` and `TimerScreen`.
* **Database Changes:** None (`StudySessions.SubjectId` foreign key already exists).
* **Testing Requirements:** Unit test in `study_session_repository_test.dart` and widget test in `timer_screen_test.dart`.
* **Estimated Effort:** 4 hours.
* **Acceptance Criteria:** Starting a timer with a subject tags the created session with that `SubjectId` in the database.

---

### [TASK-005] Complete Mobile Task CRUD Workflows (Edit & Delete)
* **Feature:** Daily Tasks
* **Status:** Partially Complete (Backend Complete, UI Partial)
* **Priority:** **P1**
* **Dependencies:** None
* **Implementation Steps:**
  1. Add `updateTask(String id, String title, String? description)` and `deleteTask(String id)` to `frontend/lib/features/tasks/data/repositories/task_repository.dart`.
  2. Add swipe-to-delete or popup action menu on each task ListTile in `task_dashboard_screen.dart`.
  3. Add an "Edit Task" modal bottom sheet.
* **Backend Changes:** None (`PUT /api/tasks/{id}` and `DELETE /api/tasks/{id}` already exist).
* **Flutter Changes:** Add repository methods and UI interactions.
* **Database Changes:** None.
* **Testing Requirements:** Flutter repository and widget tests for edit and delete actions.
* **Estimated Effort:** 5 hours.
* **Acceptance Criteria:** User can edit task titles/descriptions and permanently delete tasks from the mobile app.

---

### [TASK-006] Complete Mobile Habit CRUD Workflows (Edit & Delete)
* **Feature:** Habits & Streaks
* **Status:** Partially Complete (Backend Complete, UI Partial)
* **Priority:** **P1**
* **Dependencies:** None
* **Implementation Steps:**
  1. Add `updateHabit(String id, String title, String description)` and `deleteHabit(String id)` to `frontend/lib/features/habits/data/repositories/habit_repository.dart`.
  2. Add edit/delete contextual actions on each habit card in `habit_dashboard_screen.dart`.
  3. Include confirmation dialog when deleting a habit to explain streak removal.
* **Backend Changes:** None (`PUT /api/habits/{id}` and `DELETE /api/habits/{id}` already exist).
* **Flutter Changes:** Add repository methods and UI interactions.
* **Database Changes:** None.
* **Testing Requirements:** Flutter repository and widget tests for edit and delete actions.
* **Estimated Effort:** 5 hours.
* **Acceptance Criteria:** User can edit habit details and delete habits with immediate UI feedback.

---

### [TASK-007] Refactor Tasks and Habits to Riverpod State Management
* **Feature:** Architecture & State Management
* **Status:** Needs Improvement
* **Priority:** **P1**
* **Dependencies:** [TASK-005], [TASK-006]
* **Implementation Steps:**
  1. Create `tasks_provider.dart` and `habits_provider.dart` using Riverpod `AsyncNotifier` or `FutureProvider`.
  2. Replace raw `StatefulWidget` and inline repository instantiations in `TaskDashboardScreen` and `HabitDashboardScreen`.
  3. Connect cache invalidation so modifying tasks or habits immediately updates Dashboard cards.
* **Backend Changes:** None.
* **Flutter Changes:** Refactor screen state management to Riverpod.
* **Database Changes:** None.
* **Testing Requirements:** Verify reactive cache updates across tab switches.
* **Estimated Effort:** 6 hours.
* **Acceptance Criteria:** Zero direct repository instantiations in widget states; UI reacts automatically to state changes.

---

### [TASK-008] User Profile Editing & Change Password Modals
* **Feature:** Profile & Account Management
* **Status:** Partially Complete (Backend Ready, UI Missing)
* **Priority:** **P1**
* **Dependencies:** None
* **Implementation Steps:**
  1. Add `updateProfile(String displayName)` and `changePassword(String oldPassword, String newPassword)` to `AuthRepository`.
  2. Add an "Edit Profile" bottom sheet in `ProfileScreen` to update Display Name.
  3. Add a "Change Password" dialog in `SettingsScreen`.
* **Backend Changes:** None (`PUT /api/auth/profile` and `POST /api/auth/change-password` already exist).
* **Flutter Changes:** Add repository methods and dialog UI.
* **Database Changes:** None.
* **Testing Requirements:** Flutter repository and widget tests.
* **Estimated Effort:** 4 hours.
* **Acceptance Criteria:** User can update their display name and change their password directly in the mobile UI.

---

## 4. P2: Version 1.0 Hardening & Extended Polish

### [TASK-009] Refresh Token Rotation & 401 Interceptor
* **Feature:** Authentication & Session Resilience
* **Priority:** **P2**
* **Dependencies:** [TASK-002]
* **Implementation Steps:**
  1. Create `RefreshToken` entity in `MOT.Domain` and migration in `MOT.Infrastructure`.
  2. Implement `RefreshTokenCommand` and `RevokeTokenCommand` in MediatR.
  3. Add `POST /api/auth/refresh` endpoint in `AuthController`.
  4. Add 401 retry interceptor in Flutter `NetworkClient` to refresh tokens transparently.
* **Backend Changes:** Entity, migration, MediatR handlers, controller endpoint.
* **Flutter Changes:** Dio interceptor and `flutter_secure_storage` integration.
* **Database Changes:** New `RefreshTokens` table with unique index on token hash.
* **Estimated Effort:** 12 hours.
* **Acceptance Criteria:** Seamless token refresh without abrupt logouts during active sessions.

---

### [TASK-010] Dedicated Mobile Focus Analytics Screen
* **Feature:** Analytics & Productivity Reports
* **Priority:** **P2**
* **Dependencies:** None
* **Implementation Steps:**
  1. Create `frontend/lib/features/analytics/presentation/screens/analytics_screen.dart`.
  2. Create Riverpod providers for `getSummary`, `getDailyAnalytics`, and `getWeeklyAnalytics`.
  3. Implement monthly focus line charts, subject time allocation pie charts, and task completion metrics.
  4. Add navigation link from Profile or Dashboard.
* **Backend Changes:** None (all analytics endpoints already implemented).
* **Flutter Changes:** New presentation screen, charts, and provider integration.
* **Database Changes:** None.
* **Estimated Effort:** 14 hours.
* **Acceptance Criteria:** Visual analytics screen displaying historical daily, weekly, and subject focus stats.

---

### [TASK-011] Local Notifications & Study Reminders
* **Feature:** Notifications & Habit Retention
* **Priority:** **P2**
* **Dependencies:** None
* **Implementation Steps:**
  1. Add `flutter_local_notifications` package to `frontend/pubspec.yaml`.
  2. Implement local notification service for timer completion alarms and daily study goal reminders.
  3. Add notification toggle controls in `SettingsScreen`.
* **Backend Changes:** None.
* **Flutter Changes:** Add notification service and platform permissions.
* **Database Changes:** None.
* **Estimated Effort:** 10 hours.
* **Acceptance Criteria:** Device rings/vibrates when timer completes; sends daily study reminder at user's preferred time.

---

### [TASK-012] Automated MariaDB Integration Testing in CI
* **Feature:** CI/CD & Testing Infrastructure
* **Priority:** **P2**
* **Dependencies:** None
* **Implementation Steps:**
  1. Configure GitHub Actions workflow with a `mariadb:10.11` service container.
  2. Enable `MOT_ENABLE_MYSQL_INTEGRATION_TESTS=true` and pass connection string.
  3. Execute `MOT.IntegrationTests` verifying all database constraints and migrations on MariaDB.
* **Backend Changes:** GitHub Actions `.github/workflows/ci.yml` configuration.
* **Flutter Changes:** None.
* **Database Changes:** None.
* **Estimated Effort:** 6 hours.
* **Acceptance Criteria:** CI passes 100% of MariaDB integration tests on every pull request.

---

## 5. P3: Advanced Post-MVP Roadmap Features

| Task ID | Feature Name | Description | Priority | Backend Effort | Flutter Effort | Total Effort |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| **TASK-013** | SignalR Virtual Classroom | Migrate from HTTP polling to WebSockets with native video | P3 | 24 hrs | 28 hrs | 52 hrs |
| **TASK-014** | Exams & Syllabus Planner | Exam countdown, topic checklist, and study revision planner | P3 | 20 hrs | 24 hrs | 44 hrs |
| **TASK-015** | Friends & Social Study Feed | Friend requests, online study status, and activity feed | P3 | 18 hrs | 22 hrs | 40 hrs |
| **TASK-016** | Global & Group Leaderboards | Weekly XP rankings, group competitions, and league tiers | P3 | 14 hrs | 18 hrs | 32 hrs |
| **TASK-017** | AI Study Assistant (LLM) | Flashcard generation, concept explanation, and study notes summarizer | P3 | 22 hrs | 26 hrs | 48 hrs |
| **TASK-018** | Premium Subscription (IAP) | RevenueCat / In-App Purchases for Pro features and cloud sync | P3 | 16 hrs | 20 hrs | 36 hrs |
| **TASK-019** | Document Scanner & OCR | Scan notes and convert study materials to digital text | P3 | 12 hrs | 26 hrs | 38 hrs |
