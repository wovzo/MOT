import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/dashboard/data/models/dashboard_summary_model.dart';
import 'package:mind_on_track/features/dashboard/data/repositories/dashboard_repository.dart';

class MockNetworkClient extends NetworkClient {
  final Future<Response> Function(RequestOptions options) onRequest;

  MockNetworkClient(this.onRequest);

  @override
  Dio get dio {
    final dio = Dio();
    dio.httpClientAdapter = _MockHttpClientAdapter(onRequest);
    return dio;
  }
}

class _MockHttpClientAdapter implements HttpClientAdapter {
  final Future<Response> Function(RequestOptions options) handler;

  _MockHttpClientAdapter(this.handler);

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    try {
      final response = await handler(options);
      return ResponseBody.fromString(
        response.data is String ? response.data : jsonEncode(response.data),
        response.statusCode ?? 200,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        },
      );
    } on DioException catch (e) {
      if (e.response != null) {
        return ResponseBody.fromString(
          e.response!.data is String ? e.response!.data : jsonEncode(e.response!.data),
          e.response!.statusCode ?? 400,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType],
          },
        );
      }
      rethrow;
    }
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  group('DashboardSummary Model Tests', () {
    test('fromJson parses fully populated payload correctly', () {
      final json = {
        'dailyGoal': {
          'date': '2026-10-14',
          'targetMinutes': 90,
          'actualMinutes': 45,
          'remainingMinutes': 45,
          'progressPercentage': 50.0,
          'isAchieved': false,
          'isCustomTarget': true,
          'completedSessionsCount': 1,
        },
        'activeSession': {
          'id': 'sess-123',
          'title': 'Linear Algebra Deep Dive',
          'startTime': '2026-10-14T09:00:00Z',
          'durationMinutes': 30,
          'isCompleted': false,
          'isPaused': false,
          'accumulatedSeconds': 1800,
          'elapsedSeconds': 1800,
        },
        'tasks': {
          'totalCount': 5,
          'completedCount': 3,
          'pendingCount': 2,
          'completionPercentage': 60.0,
          'topPendingTasks': [
            {
              'id': 'task-1',
              'title': 'Chapter 5 summary',
              'description': 'Notes',
              'isCompleted': false,
              'createdAt': '2026-10-14T08:00:00Z',
            }
          ],
        },
        'habits': {
          'totalCount': 3,
          'completedTodayCount': 2,
          'completionPercentage': 66.7,
          'habits': [
            {
              'id': 'habit-1',
              'title': 'Morning Flashcards',
              'description': 'Review Anki',
              'isCompletedToday': true,
              'currentStreak': 4,
            }
          ],
        },
        'gamification': {
          'level': 2,
          'totalXp': 350,
          'currentLevelThresholdXp': 200,
          'nextLevelThresholdXp': 500,
          'xpEarnedInCurrentLevel': 150,
          'xpRequiredForNextLevel': 300,
          'progressFraction': 0.5,
          'progressPercentage': 50.0,
          'totalAchievements': 8,
          'unlockedAchievements': 3,
        },
        'streak': {
          'currentStreak': 5,
          'isActiveToday': true,
          'longestStreak': 7,
        },
        'subjects': [
          {
            'id': 'sub-1',
            'name': 'Mathematics',
            'description': 'Calculus & Algebra',
            'colorHex': '#4A90E2',
            'createdAt': '2026-10-14T08:00:00Z',
          }
        ],
        'recentSessions': [
          {
            'id': 'sess-prev',
            'title': 'Calculus Session 1',
            'startTime': '2026-10-14T07:00:00Z',
            'endTime': '2026-10-14T07:45:00Z',
            'durationMinutes': 45,
            'isCompleted': true,
            'isPaused': false,
          }
        ],
        'weeklyActivity': [
          {
            'date': '2026-10-14',
            'dayLabel': 'Wed',
            'focusMinutes': 45,
            'completedSessionsCount': 1,
          }
        ],
      };

      final summary = DashboardSummary.fromJson(json);

      expect(summary.dailyGoal.targetMinutes, 90);
      expect(summary.dailyGoal.actualMinutes, 45);
      expect(summary.dailyGoal.progressPercentage, 50.0);
      expect(summary.activeSession, isNotNull);
      expect(summary.activeSession!.title, 'Linear Algebra Deep Dive');
      expect(summary.tasks.totalCount, 5);
      expect(summary.tasks.completedCount, 3);
      expect(summary.tasks.topPendingTasks.length, 1);
      expect(summary.habits.totalCount, 3);
      expect(summary.habits.completedTodayCount, 2);
      expect(summary.habits.habits.length, 1);
      expect(summary.streak.currentStreak, 5);
      expect(summary.streak.isActiveToday, isTrue);
      expect(summary.streak.longestStreak, 7);
      expect(summary.gamification.level, 2);
      expect(summary.gamification.totalXp, 350);
      expect(summary.subjects.length, 1);
      expect(summary.subjects.first.name, 'Mathematics');
      expect(summary.recentSessions.length, 1);
      expect(summary.weeklyActivity.length, 1);
      expect(summary.weeklyActivity.first.dayLabel, 'Wed');
    });

    test('fromJson handles empty or null fields safely', () {
      final summary = DashboardSummary.fromJson({});

      expect(summary.dailyGoal.targetMinutes, 60);
      expect(summary.dailyGoal.actualMinutes, 0);
      expect(summary.activeSession, isNull);
      expect(summary.tasks.totalCount, 0);
      expect(summary.tasks.topPendingTasks, isEmpty);
      expect(summary.habits.totalCount, 0);
      expect(summary.habits.habits, isEmpty);
      expect(summary.streak.currentStreak, 0);
      expect(summary.streak.isActiveToday, isFalse);
      expect(summary.gamification.level, 1);
      expect(summary.subjects, isEmpty);
      expect(summary.recentSessions, isEmpty);
      expect(summary.weeklyActivity, isEmpty);
    });
  });

  group('DashboardRepository Tests', () {
    test('getDashboardSummary returns DashboardSummary on success', () async {
      final mockClient = MockNetworkClient((options) async {
        expect(options.path, 'dashboard');
        expect(options.queryParameters['timeZoneOffsetMinutes'], isNotNull);

        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'dailyGoal': {'targetMinutes': 60, 'actualMinutes': 30},
            'tasks': {'totalCount': 2, 'completedCount': 1},
            'habits': {'totalCount': 1, 'completedTodayCount': 1},
            'streak': {'currentStreak': 3, 'isActiveToday': true, 'longestStreak': 5},
            'gamification': {'level': 1, 'totalXp': 50},
            'subjects': [],
            'recentSessions': [],
            'weeklyActivity': [],
          },
        );
      });

      final repo = DashboardRepository(mockClient);
      final summary = await repo.getDashboardSummary(timeZoneOffsetMinutes: 0);

      expect(summary.dailyGoal.actualMinutes, 30);
      expect(summary.streak.currentStreak, 3);
      expect(summary.streak.isActiveToday, isTrue);
    });

    test('getDashboardSummary extracts API error on failure', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 401,
            data: {'error': 'Session expired. Please log in again.'},
          ),
        );
      });

      final repo = DashboardRepository(mockClient);
      expect(
        () => repo.getDashboardSummary(timeZoneOffsetMinutes: 0),
        throwsA(predicate((e) => e.toString().contains('Session expired'))),
      );
    });
  });
}
