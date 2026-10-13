import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/analytics/data/models/analytics_summary.dart';
import 'package:mind_on_track/features/analytics/data/models/daily_analytics.dart';
import 'package:mind_on_track/features/analytics/data/repositories/analytics_repository.dart';

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
  group('FocusAnalyticsSummary Model Tests', () {
    test('fromJson correctly parses complete summary JSON', () {
      final json = {
        'totalFocusMinutes': 180,
        'todayFocusMinutes': 60,
        'thisWeekFocusMinutes': 120,
        'totalSessionsCount': 5,
        'completedSessionsCount': 3,
        'incompleteSessionsCount': 2,
        'averageSessionMinutes': 60.0,
        'tasks': {
          'totalTasks': 10,
          'completedTasks': 7,
          'pendingTasks': 3,
          'completionRatePercentage': 70.0,
        },
        'habits': {
          'totalHabits': 4,
          'totalCompletions': 25,
          'completionsToday': 3,
          'completionsThisWeek': 15,
          'activeStreaksCount': 2,
        }
      };

      final summary = FocusAnalyticsSummary.fromJson(json);

      expect(summary.totalFocusMinutes, 180);
      expect(summary.todayFocusMinutes, 60);
      expect(summary.thisWeekFocusMinutes, 120);
      expect(summary.totalSessionsCount, 5);
      expect(summary.completedSessionsCount, 3);
      expect(summary.incompleteSessionsCount, 2);
      expect(summary.averageSessionMinutes, 60.0);
      expect(summary.tasks.totalTasks, 10);
      expect(summary.tasks.completedTasks, 7);
      expect(summary.tasks.pendingTasks, 3);
      expect(summary.tasks.completionRatePercentage, 70.0);
      expect(summary.habits.totalHabits, 4);
      expect(summary.habits.totalCompletions, 25);
      expect(summary.habits.completionsToday, 3);
      expect(summary.habits.activeStreaksCount, 2);
    });

    test('fromJson safely falls back to defaults when fields are missing or null', () {
      final summary = FocusAnalyticsSummary.fromJson({});

      expect(summary.totalFocusMinutes, 0);
      expect(summary.todayFocusMinutes, 0);
      expect(summary.totalSessionsCount, 0);
      expect(summary.tasks.totalTasks, 0);
      expect(summary.habits.totalHabits, 0);
    });
  });

  group('DailyAnalytics Model Tests', () {
    test('fromJson parses complete daily analytics JSON', () {
      final json = {
        'startDate': '2026-10-10',
        'endDate': '2026-10-12',
        'totalFocusMinutes': 105,
        'totalCompletedSessions': 2,
        'totalHabitCompletions': 3,
        'dailyBreakdown': [
          {
            'date': '2026-10-10',
            'focusMinutes': 45,
            'completedSessionsCount': 1,
            'habitCompletionsCount': 1,
          },
          {
            'date': '2026-10-11',
            'focusMinutes': 0,
            'completedSessionsCount': 0,
            'habitCompletionsCount': 1,
          },
          {
            'date': '2026-10-12',
            'focusMinutes': 60,
            'completedSessionsCount': 1,
            'habitCompletionsCount': 1,
          }
        ]
      };

      final daily = DailyAnalytics.fromJson(json);

      expect(daily.startDate, '2026-10-10');
      expect(daily.endDate, '2026-10-12');
      expect(daily.totalFocusMinutes, 105);
      expect(daily.totalCompletedSessions, 2);
      expect(daily.totalHabitCompletions, 3);
      expect(daily.dailyBreakdown.length, 3);
      expect(daily.dailyBreakdown[0].date, '2026-10-10');
      expect(daily.dailyBreakdown[0].focusMinutes, 45);
      expect(daily.dailyBreakdown[1].focusMinutes, 0);
      expect(daily.dailyBreakdown[2].focusMinutes, 60);
    });
  });

  group('AnalyticsRepository Tests', () {
    test('getSummary fetches and maps analytics summary', () async {
      final client = MockNetworkClient((options) async {
        expect(options.path, 'analytics/summary');
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'totalFocusMinutes': 120,
            'todayFocusMinutes': 60,
            'thisWeekFocusMinutes': 120,
            'totalSessionsCount': 2,
            'completedSessionsCount': 2,
            'incompleteSessionsCount': 0,
            'averageSessionMinutes': 60.0,
            'tasks': {'totalTasks': 1, 'completedTasks': 1, 'pendingTasks': 0, 'completionRatePercentage': 100.0},
            'habits': {'totalHabits': 1, 'totalCompletions': 1, 'completionsToday': 1, 'completionsThisWeek': 1, 'activeStreaksCount': 1}
          },
        );
      });

      final repo = AnalyticsRepository(client);
      final summary = await repo.getSummary();

      expect(summary.totalFocusMinutes, 120);
      expect(summary.todayFocusMinutes, 60);
      expect(summary.tasks.totalTasks, 1);
    });

    test('getDaily passes query parameters and maps daily analytics', () async {
      final client = MockNetworkClient((options) async {
        expect(options.path, 'analytics/daily');
        expect(options.queryParameters['startDate'], '2026-10-01');
        expect(options.queryParameters['endDate'], '2026-10-07');
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'startDate': '2026-10-01',
            'endDate': '2026-10-07',
            'totalFocusMinutes': 90,
            'totalCompletedSessions': 1,
            'totalHabitCompletions': 2,
            'dailyBreakdown': []
          },
        );
      });

      final repo = AnalyticsRepository(client);
      final daily = await repo.getDaily(
        startDate: DateTime(2026, 10, 1),
        endDate: DateTime(2026, 10, 7),
      );

      expect(daily.startDate, '2026-10-01');
      expect(daily.endDate, '2026-10-07');
      expect(daily.totalFocusMinutes, 90);
    });
  });
}
