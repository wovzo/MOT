import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mind_on_track/features/auth/presentation/providers/auth_provider.dart';
import 'package:mind_on_track/features/gamification/data/models/achievement_item.dart';
import 'package:mind_on_track/features/gamification/data/models/gamification_summary.dart';
import 'package:mind_on_track/features/gamification/data/models/xp_transaction_item.dart';
import 'package:mind_on_track/features/gamification/presentation/providers/gamification_providers.dart';
import 'package:mind_on_track/features/gamification/presentation/screens/achievements_screen.dart';
import 'package:mind_on_track/features/profile/data/models/user_profile.dart';
import 'package:mind_on_track/features/profile/presentation/screens/profile_screen.dart';

void main() {
  final testProfile = UserProfile(
    id: 'user-1',
    email: 'test@example.com',
    displayName: 'Alex Rider',
    createdAt: DateTime.now(),
    currentStreak: 5,
    level: 2,
    xp: 250,
    totalFocusMinutes: 150,
  );

  final testSummary = GamificationSummary(
    level: 2,
    totalXp: 250,
    currentLevelThresholdXp: 100,
    nextLevelThresholdXp: 300,
    xpEarnedInCurrentLevel: 150,
    xpRequiredForNextLevel: 200,
    progressFraction: 0.75,
    progressPercentage: 75.0,
    totalAchievements: 8,
    unlockedAchievements: 3,
  );

  final testAchievements = [
    AchievementItem(
      code: 'FIRST_STEP',
      title: 'First Step',
      description: 'Complete your first daily task',
      iconName: 'check_circle',
      xpReward: 25,
      isUnlocked: true,
      unlockedAt: DateTime.utc(2026, 10, 13, 12, 0),
    ),
    AchievementItem(
      code: 'FOCUS_INITIATE',
      title: 'Focus Initiate',
      description: 'Complete your first study session of at least 15 minutes',
      iconName: 'timer',
      xpReward: 25,
      isUnlocked: false,
      unlockedAt: null,
    ),
    AchievementItem(
      code: 'UNKNOWN_CODE',
      title: 'Mystery Quest',
      description: 'A secret achievement with unknown icon',
      iconName: 'custom_unknown_icon_321',
      xpReward: 50,
      isUnlocked: false,
      unlockedAt: null,
    ),
  ];

  final testHistory = [
    XpTransactionItem(
      id: 'tx-1',
      amount: 25,
      sourceType: 'TASK',
      description: 'Completed task: Physics homework',
      createdAt: DateTime.now().subtract(const Duration(minutes: 10)),
    ),
    XpTransactionItem(
      id: 'tx-2',
      amount: 20,
      sourceType: 'HABIT',
      description: 'Completed habit: Drink water',
      createdAt: DateTime.now().subtract(const Duration(hours: 2)),
    ),
  ];

  Widget createTestWidget({
    UserProfile? profile,
    GamificationSummary? summary,
    List<AchievementItem>? achievements,
    List<XpTransactionItem>? history,
    bool isLoadingProfile = false,
    bool isErrorProfile = false,
    bool isErrorHistory = false,
    bool isEmptyHistory = false,
  }) {
    return ProviderScope(
      overrides: [
        userProfileProvider.overrideWith((ref) async {
          if (isLoadingProfile) throw Exception('loading');
          if (isErrorProfile) throw Exception('Profile load failed');
          return profile ?? testProfile;
        }),
        gamificationSummaryProvider.overrideWith((ref) async {
          return summary ?? testSummary;
        }),
        achievementsProvider.overrideWith((ref) async {
          return achievements ?? testAchievements;
        }),
        xpHistoryProvider.overrideWith((ref) async {
          if (isErrorHistory) throw Exception('History error');
          if (isEmptyHistory) return <XpTransactionItem>[];
          return history ?? testHistory;
        }),
      ],
      child: const MaterialApp(
        home: ProfileScreen(),
      ),
    );
  }

  group('ProfileScreen Gamification Integration Tests', () {
    testWidgets('renders Level Progress card with level, total XP, and progress fraction', (tester) async {
      await tester.pumpWidget(createTestWidget());
      await tester.pumpAndSettle();

      expect(find.text('Alex Rider'), findsOneWidget);
      expect(find.text('Level 2'), findsOneWidget);
      expect(find.text('250 XP'), findsOneWidget);
      expect(find.text('75%'), findsOneWidget);
      expect(find.text('150 / 200 XP'), findsOneWidget);
      expect(find.text('Next: Level 3'), findsOneWidget);
    });

    testWidgets('renders zero progress values at level 1 boundary safely', (tester) async {
      final zeroSummary = GamificationSummary(
        level: 1,
        totalXp: 0,
        currentLevelThresholdXp: 0,
        nextLevelThresholdXp: 100,
        xpEarnedInCurrentLevel: 0,
        xpRequiredForNextLevel: 100,
        progressFraction: 0.0,
        progressPercentage: 0.0,
        totalAchievements: 8,
        unlockedAchievements: 0,
      );

      await tester.pumpWidget(createTestWidget(summary: zeroSummary));
      await tester.pumpAndSettle();

      expect(find.text('Level 1'), findsOneWidget);
      expect(find.text('0 XP'), findsOneWidget);
      expect(find.text('0%'), findsOneWidget);
      expect(find.text('0 / 100 XP'), findsOneWidget);
    });

    testWidgets('renders achievements navigation entry and navigates to AchievementsScreen', (tester) async {
      await tester.pumpWidget(createTestWidget());
      await tester.pumpAndSettle();

      expect(find.text('Achievements'), findsOneWidget);
      expect(find.text('3 of 8 Unlocked'), findsOneWidget);

      // Tap on achievements card
      await tester.tap(find.text('Achievements'));
      await tester.pumpAndSettle();

      // Should be on AchievementsScreen
      expect(find.byType(AchievementsScreen), findsOneWidget);
      expect(find.text('First Step'), findsOneWidget);
      expect(find.text('Focus Initiate'), findsOneWidget);

      // Back navigation to Profile
      await tester.tap(find.byType(BackButton));
      await tester.pumpAndSettle();

      expect(find.byType(ProfileScreen), findsOneWidget);
    });

    testWidgets('renders recent XP activity items with amounts and descriptions', (tester) async {
      await tester.pumpWidget(createTestWidget());
      await tester.pumpAndSettle();

      expect(find.text('Recent XP Activity'), findsOneWidget);
      expect(find.text('Completed task: Physics homework'), findsOneWidget);
      expect(find.text('+25 XP'), findsOneWidget);
      expect(find.text('Completed habit: Drink water'), findsOneWidget);
      expect(find.text('+20 XP'), findsOneWidget);
    });

    testWidgets('renders empty state when XP history has zero transactions', (tester) async {
      await tester.pumpWidget(createTestWidget(isEmptyHistory: true));
      await tester.pumpAndSettle();

      expect(find.text('No XP activity recorded yet'), findsOneWidget);
    });

    testWidgets('renders error state on profile error with retry button', (tester) async {
      await tester.pumpWidget(createTestWidget(isErrorProfile: true));
      await tester.pumpAndSettle();

      expect(find.text('Failed to load profile'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });

  group('AchievementsScreen Widget Tests', () {
    Widget createAchievementsScreenWidget({
      List<AchievementItem>? achievements,
      bool isError = false,
      bool isEmpty = false,
    }) {
      return ProviderScope(
        overrides: [
          achievementsProvider.overrideWith((ref) async {
            if (isError) throw Exception('Failed to load achievements');
            if (isEmpty) return <AchievementItem>[];
            return achievements ?? testAchievements;
          }),
        ],
        child: const MaterialApp(
          home: AchievementsScreen(),
        ),
      );
    }

    testWidgets('renders progress summary banner and achievement items', (tester) async {
      await tester.pumpWidget(createAchievementsScreenWidget());
      await tester.pumpAndSettle();

      expect(find.text('Achievements'), findsOneWidget);
      expect(find.text('Unlocked 1 of 3'), findsOneWidget);
      expect(find.text('33%'), findsOneWidget);

      // Unlocked achievement
      expect(find.text('First Step'), findsOneWidget);
      expect(find.text('Complete your first daily task'), findsOneWidget);
      expect(find.text('+25 XP'), findsNWidgets(2)); // First step + Focus initiate
      expect(find.text('Unlocked on Oct 13, 2026'), findsOneWidget);

      // Locked achievement
      expect(find.text('Focus Initiate'), findsOneWidget);
      expect(find.text('Locked'), findsNWidgets(2)); // Focus initiate + Mystery quest
    });

    testWidgets('falls back safely to default trophy icon for unknown icon identifier', (tester) async {
      await tester.pumpWidget(createAchievementsScreenWidget());
      await tester.pumpAndSettle();

      expect(find.text('Mystery Quest'), findsOneWidget);
      expect(find.byIcon(Icons.emoji_events_rounded), findsWidgets);
    });

    testWidgets('renders error state with retry button on failure', (tester) async {
      await tester.pumpWidget(createAchievementsScreenWidget(isError: true));
      await tester.pumpAndSettle();

      expect(find.text('Failed to load achievements'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('renders empty state when catalog is empty', (tester) async {
      await tester.pumpWidget(createAchievementsScreenWidget(isEmpty: true));
      await tester.pumpAndSettle();

      expect(find.text('No achievements available'), findsOneWidget);
    });
  });
}
