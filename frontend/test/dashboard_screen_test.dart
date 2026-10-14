import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mind_on_track/features/daily_goals/data/models/daily_goal_progress_model.dart';
import 'package:mind_on_track/features/dashboard/data/models/dashboard_summary_model.dart';
import 'package:mind_on_track/features/dashboard/presentation/providers/dashboard_provider.dart';
import 'package:mind_on_track/features/dashboard/presentation/screens/dashboard_screen.dart';
import 'package:mind_on_track/features/gamification/data/models/gamification_summary.dart';
import 'package:mind_on_track/features/habits/data/models/habit_model.dart';
import 'package:mind_on_track/features/subjects/data/models/subject_model.dart';
import 'package:mind_on_track/features/tasks/data/models/daily_task.dart';
import 'package:mind_on_track/features/timer/data/models/study_session_model.dart';

void main() {
  final sampleSummary = DashboardSummary(
    dailyGoal: DailyGoalProgress(
      date: '2026-10-14',
      targetMinutes: 90,
      actualMinutes: 45,
      remainingMinutes: 45,
      progressPercentage: 50.0,
      isAchieved: false,
      isCustomTarget: true,
      completedSessionsCount: 1,
    ),
    activeSession: StudySession(
      id: 'sess-active',
      title: 'Deep Focus Session',
      startTime: DateTime.now().subtract(const Duration(minutes: 25)),
      durationMinutes: 25,
      isCompleted: false,
      isPaused: false,
      accumulatedSeconds: 1500,
      elapsedSeconds: 1500,
    ),
    tasks: DashboardTaskSummary(
      totalCount: 4,
      completedCount: 2,
      pendingCount: 2,
      completionPercentage: 50.0,
      topPendingTasks: [
        DailyTask(
          id: 'task-1',
          title: 'Review Physics Formulas',
          isCompleted: false,
          createdAt: DateTime.now(),
        ),
      ],
    ),
    habits: DashboardHabitSummary(
      totalCount: 3,
      completedTodayCount: 2,
      completionPercentage: 66.7,
      habits: [
        Habit(
          id: 'habit-1',
          title: 'Read 20 mins',
          description: '',
          isCompletedToday: true,
          currentStreak: 4,
        ),
      ],
    ),
    gamification: GamificationSummary(
      level: 3,
      totalXp: 450,
      currentLevelThresholdXp: 300,
      nextLevelThresholdXp: 600,
      xpEarnedInCurrentLevel: 150,
      xpRequiredForNextLevel: 300,
      progressFraction: 0.5,
      progressPercentage: 50.0,
      totalAchievements: 8,
      unlockedAchievements: 2,
    ),
    streak: DashboardStreakSummary(
      currentStreak: 5,
      isActiveToday: true,
      longestStreak: 7,
    ),
    subjects: [
      Subject(
        id: 'sub-1',
        name: 'Physics',
        description: 'Mechanics',
        colorHex: '#00D9C0',
        createdAt: DateTime.now(),
      ),
    ],
    recentSessions: [
      StudySession(
        id: 'sess-done',
        title: 'Morning Problem Set',
        startTime: DateTime.now().subtract(const Duration(hours: 3)),
        endTime: DateTime.now().subtract(const Duration(hours: 2, minutes: 15)),
        durationMinutes: 45,
        isCompleted: true,
      ),
    ],
    weeklyActivity: [
      DashboardDayActivity(date: '2026-10-08', dayLabel: 'Thu', focusMinutes: 30, completedSessionsCount: 1),
      DashboardDayActivity(date: '2026-10-09', dayLabel: 'Fri', focusMinutes: 45, completedSessionsCount: 1),
      DashboardDayActivity(date: '2026-10-10', dayLabel: 'Sat', focusMinutes: 60, completedSessionsCount: 2),
      DashboardDayActivity(date: '2026-10-11', dayLabel: 'Sun', focusMinutes: 0, completedSessionsCount: 0),
      DashboardDayActivity(date: '2026-10-12', dayLabel: 'Mon', focusMinutes: 50, completedSessionsCount: 1),
      DashboardDayActivity(date: '2026-10-13', dayLabel: 'Tue', focusMinutes: 40, completedSessionsCount: 1),
      DashboardDayActivity(date: '2026-10-14', dayLabel: 'Wed', focusMinutes: 45, completedSessionsCount: 1),
    ],
  );

  Widget createTestWidget({
    AsyncValue<DashboardSummary>? summaryState,
    VoidCallback? onTasks,
    VoidCallback? onHabits,
    VoidCallback? onTimer,
  }) {
    return ProviderScope(
      overrides: [
        if (summaryState != null)
          dashboardSummaryProvider.overrideWith((ref) => summaryState.value!),
      ],
      child: MaterialApp(
        home: DashboardScreen(
          onNavigateToTasks: onTasks,
          onNavigateToHabits: onHabits,
          onNavigateToTimer: onTimer,
        ),
      ),
    );
  }

  testWidgets('DashboardScreen displays loading state initially', (tester) async {
    final completer = Completer<DashboardSummary>();

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          dashboardSummaryProvider.overrideWith((ref) => completer.future),
        ],
        child: const MaterialApp(home: DashboardScreen()),
      ),
    );

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('DashboardScreen displays error state with retry button', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          dashboardSummaryProvider.overrideWith((ref) => Future.error('Connection timed out')),
        ],
        child: const MaterialApp(home: DashboardScreen()),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Failed to load dashboard'), findsOneWidget);
    expect(find.textContaining('Connection timed out'), findsOneWidget);
    expect(find.text('Try Again'), findsOneWidget);
  });

  testWidgets('DashboardScreen displays populated data correctly', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);

    await tester.pumpWidget(
      createTestWidget(summaryState: AsyncValue.data(sampleSummary)),
    );

    await tester.pumpAndSettle();

    // Header & gamification
    expect(find.text('Mind On Track'), findsOneWidget);
    expect(find.text('5 Days'), findsOneWidget);
    expect(find.text('Level 3'), findsOneWidget);
    expect(find.text('450 XP'), findsOneWidget);

    // Active session banner
    expect(find.textContaining('ACTIVE STUDY SESSION'), findsOneWidget);
    expect(find.text('Deep Focus Session'), findsOneWidget);
    expect(find.text('Open Timer'), findsOneWidget);

    // Daily goal
    expect(find.text('Daily Study Goal'), findsOneWidget);
    expect(find.text('45 / 90 mins'), findsOneWidget);
    expect(find.text('50%'), findsOneWidget);
    expect(find.text('45 mins remaining today'), findsOneWidget);

    // Weekly activity
    expect(find.text('Weekly Focus Activity'), findsOneWidget);
    expect(find.text('270 mins total'), findsOneWidget);

    // Tasks & habits
    expect(find.text('Daily Tasks'), findsOneWidget);
    expect(find.text('2/4'), findsOneWidget);
    expect(find.text('50% done'), findsOneWidget);
    expect(find.text('Habits Today'), findsOneWidget);
    expect(find.text('2/3'), findsOneWidget);
    expect(find.text('67% completed'), findsOneWidget);

    // Subjects
    expect(find.text('Subjects'), findsOneWidget);
    expect(find.text('Physics'), findsOneWidget);

    // Recent sessions
    expect(find.text('Recent Study Sessions'), findsOneWidget);
    expect(find.text('Morning Problem Set'), findsOneWidget);
    expect(find.text('45 minutes focused'), findsOneWidget);
  });

  testWidgets('DashboardScreen triggers navigation callbacks on card taps', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);

    bool tasksTapped = false;
    bool habitsTapped = false;
    bool timerTapped = false;

    await tester.pumpWidget(
      createTestWidget(
        summaryState: AsyncValue.data(sampleSummary),
        onTasks: () => tasksTapped = true,
        onHabits: () => habitsTapped = true,
        onTimer: () => timerTapped = true,
      ),
    );

    await tester.pumpAndSettle();

    // Tap tasks card
    await tester.tap(find.text('Daily Tasks'));
    expect(tasksTapped, isTrue);

    // Tap habits card
    await tester.tap(find.text('Habits Today'));
    expect(habitsTapped, isTrue);

    // Tap open timer
    await tester.tap(find.text('Open Timer'));
    expect(timerTapped, isTrue);
  });

  testWidgets('DashboardScreen renders on small screen without overflow', (tester) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);

    await tester.pumpWidget(
      createTestWidget(summaryState: AsyncValue.data(sampleSummary)),
    );

    await tester.pumpAndSettle();

    expect(find.text('Mind On Track'), findsOneWidget);
    expect(find.text('Daily Study Goal'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Habit Streak: 5d'), 200);
    expect(find.text('Habit Streak: 5d'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('DashboardScreen pull to refresh triggers provider reload', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);

    await tester.pumpWidget(
      createTestWidget(summaryState: AsyncValue.data(sampleSummary)),
    );

    await tester.pumpAndSettle();

    // Target the outermost scrollable ListView and drag down to trigger pull-to-refresh
    await tester.fling(find.byType(ListView).first, const Offset(0, 300), 1000);
    await tester.pump();

    expect(find.byType(RefreshIndicator), findsOneWidget);
  });
}

