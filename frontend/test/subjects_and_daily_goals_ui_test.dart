import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mind_on_track/features/subjects/data/models/subject_model.dart';
import 'package:mind_on_track/features/subjects/presentation/providers/subjects_provider.dart';
import 'package:mind_on_track/features/subjects/presentation/screens/subjects_screen.dart';
import 'package:mind_on_track/features/daily_goals/data/models/daily_goal_progress_model.dart';
import 'package:mind_on_track/features/daily_goals/presentation/providers/daily_goals_provider.dart';
import 'package:mind_on_track/features/daily_goals/presentation/screens/daily_goals_screen.dart';

void main() {
  group('SubjectsScreen Widget Tests', () {
    testWidgets('renders empty state when subjects list is empty', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            subjectsProvider.overrideWith((ref) async => []),
          ],
          child: const MaterialApp(home: SubjectsScreen()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Manage Subjects'), findsOneWidget);
      expect(find.text('No subjects yet'), findsOneWidget);
      expect(find.byKey(const Key('emptyStateAddButton')), findsOneWidget);
    });

    testWidgets('renders subject cards and opens new subject bottom sheet', (tester) async {
      final sampleSubjects = [
        Subject(
          id: 'sub-1',
          name: 'Computer Science',
          description: 'Data Structures & Algorithms',
          colorHex: '#6C5CE7',
          createdAt: DateTime.now(),
        ),
        Subject(
          id: 'sub-2',
          name: 'Linear Algebra',
          description: 'Vector spaces & Matrices',
          colorHex: '#00D9C0',
          createdAt: DateTime.now(),
        ),
      ];

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            subjectsProvider.overrideWith((ref) async => sampleSubjects),
          ],
          child: const MaterialApp(home: SubjectsScreen()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Computer Science'), findsOneWidget);
      expect(find.text('Linear Algebra'), findsOneWidget);
      expect(find.byKey(const Key('editSubject_sub-1')), findsOneWidget);
      expect(find.byKey(const Key('deleteSubject_sub-1')), findsOneWidget);

      // Tap add subject button
      await tester.tap(find.byKey(const Key('addSubjectButton')));
      await tester.pumpAndSettle();

      expect(find.text('New Subject'), findsOneWidget);
      expect(find.byKey(const Key('subjectNameField')), findsOneWidget);
      expect(find.byKey(const Key('saveSubjectButton')), findsOneWidget);
    });
  });

  group('DailyGoalsScreen Widget Tests', () {
    testWidgets('renders progress ring, percentage, and metric tiles', (tester) async {
      final progress = DailyGoalProgress(
        date: DateTime.now().toIso8601String().split('T').first,
        targetMinutes: 60,
        actualMinutes: 45,
        remainingMinutes: 15,
        progressPercentage: 75.0,
        isAchieved: false,
        isCustomTarget: false,
        completedSessionsCount: 2,
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            dailyGoalProgressProvider.overrideWith((ref, date) async => progress),
          ],
          child: const MaterialApp(home: DailyGoalsScreen()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Daily Study Goal'), findsOneWidget);
      expect(find.text('75%'), findsOneWidget);
      expect(find.text('IN PROGRESS'), findsOneWidget);
      expect(find.text('60m'), findsOneWidget); // Target
      expect(find.text('45m'), findsOneWidget); // Focused
      expect(find.text('15m'), findsOneWidget); // Remaining
      expect(find.text('2'), findsOneWidget); // Sessions
      expect(find.byKey(const Key('adjustTargetButton')), findsOneWidget);
    });

    testWidgets('renders ACHIEVED status badge when goal met', (tester) async {
      final progress = DailyGoalProgress(
        date: DateTime.now().toIso8601String().split('T').first,
        targetMinutes: 60,
        actualMinutes: 70,
        remainingMinutes: 0,
        progressPercentage: 100.0,
        isAchieved: true,
        isCustomTarget: true,
        completedSessionsCount: 3,
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            dailyGoalProgressProvider.overrideWith((ref, date) async => progress),
          ],
          child: const MaterialApp(home: DailyGoalsScreen()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('ACHIEVED'), findsOneWidget);
      expect(find.text('100%'), findsOneWidget);
      expect(find.text('0m'), findsOneWidget);
    });

    testWidgets('tapping adjust target opens bottom sheet with presets', (tester) async {
      final progress = DailyGoalProgress(
        date: DateTime.now().toIso8601String().split('T').first,
        targetMinutes: 60,
        actualMinutes: 20,
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            dailyGoalProgressProvider.overrideWith((ref, date) async => progress),
          ],
          child: const MaterialApp(home: DailyGoalsScreen()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('adjustTargetButton')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('adjustTargetButton')));
      await tester.pumpAndSettle();

      expect(find.text('Set Daily Study Target'), findsOneWidget);
      expect(find.text('60 mins'), findsOneWidget);
      expect(find.byKey(const Key('customMinutesField')), findsOneWidget);
      expect(find.byKey(const Key('saveGoalButton')), findsOneWidget);
    });
  });
}
