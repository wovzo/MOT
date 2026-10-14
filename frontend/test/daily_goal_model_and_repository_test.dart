import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/daily_goals/data/models/daily_goal_progress_model.dart';
import 'package:mind_on_track/features/daily_goals/data/repositories/daily_goal_repository.dart';

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
  group('DailyGoalProgress Model Tests', () {
    test('fromJson parses complete daily goal progress JSON correctly', () {
      final json = {
        'date': '2026-10-14',
        'targetMinutes': 90,
        'actualMinutes': 60,
        'remainingMinutes': 30,
        'progressPercentage': 66.7,
        'isAchieved': false,
        'isCustomTarget': true,
        'completedSessionsCount': 2,
      };

      final progress = DailyGoalProgress.fromJson(json);

      expect(progress.date, '2026-10-14');
      expect(progress.targetMinutes, 90);
      expect(progress.actualMinutes, 60);
      expect(progress.remainingMinutes, 30);
      expect(progress.progressPercentage, 66.7);
      expect(progress.isAchieved, false);
      expect(progress.isCustomTarget, true);
      expect(progress.completedSessionsCount, 2);
    });

    test('fromJson falls back gracefully when optional fields are missing', () {
      final json = {
        'date': '2026-10-14',
      };

      final progress = DailyGoalProgress.fromJson(json);

      expect(progress.date, '2026-10-14');
      expect(progress.targetMinutes, 60);
      expect(progress.actualMinutes, 0);
      expect(progress.remainingMinutes, 60);
      expect(progress.progressPercentage, 0.0);
      expect(progress.isAchieved, false);
      expect(progress.isCustomTarget, false);
    });

    test('toJson serializes daily goal progress faithfully', () {
      final progress = DailyGoalProgress(
        date: '2026-10-14',
        targetMinutes: 120,
        actualMinutes: 120,
        remainingMinutes: 0,
        progressPercentage: 100.0,
        isAchieved: true,
        isCustomTarget: true,
        completedSessionsCount: 3,
      );

      final json = progress.toJson();

      expect(json['date'], '2026-10-14');
      expect(json['targetMinutes'], 120);
      expect(json['actualMinutes'], 120);
      expect(json['isAchieved'], true);
    });
  });

  group('DailyGoalRepository Tests', () {
    test('getProgress sends GET with date and timezone offset query parameters', () async {
      String? requestedPath;
      Map<String, dynamic>? queryParams;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        queryParams = options.queryParameters;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'date': '2026-10-14',
            'targetMinutes': 60,
            'actualMinutes': 45,
            'remainingMinutes': 15,
            'progressPercentage': 75.0,
            'isAchieved': false,
            'isCustomTarget': false,
            'completedSessionsCount': 1,
          },
        );
      });

      final repo = DailyGoalRepository(mockClient);
      final progress = await repo.getProgress(date: '2026-10-14', timeZoneOffsetMinutes: 330);

      expect(requestedPath, 'dailygoals/progress');
      expect(queryParams?['date'], '2026-10-14');
      expect(queryParams?['timeZoneOffsetMinutes'], 330);
      expect(progress.actualMinutes, 45);
      expect(progress.progressPercentage, 75.0);
    });

    test('setGoal sends POST with target minutes and returns updated progress', () async {
      String? requestedPath;
      String? requestedMethod;
      dynamic requestBody;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        requestedMethod = options.method;
        requestBody = options.data;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'date': '2026-10-14',
            'targetMinutes': 100,
            'actualMinutes': 45,
            'remainingMinutes': 55,
            'progressPercentage': 45.0,
            'isAchieved': false,
            'isCustomTarget': true,
            'completedSessionsCount': 1,
          },
        );
      });

      final repo = DailyGoalRepository(mockClient);
      final result = await repo.setGoal(
        targetMinutes: 100,
        date: '2026-10-14',
        timeZoneOffsetMinutes: 0,
      );

      expect(requestedPath, 'dailygoals');
      expect(requestedMethod, 'POST');
      expect(requestBody['targetMinutes'], 100);
      expect(result.targetMinutes, 100);
      expect(result.isCustomTarget, true);
    });

    test('setGoal surfaces validation error message on 400', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 400,
            data: {'error': 'TargetMinutes must be between 5 and 1440 minutes.'},
          ),
        );
      });

      final repo = DailyGoalRepository(mockClient);
      expect(
        () => repo.setGoal(targetMinutes: 2),
        throwsA(predicate((e) => e.toString().contains('TargetMinutes must be between 5 and 1440'))),
      );
    });
  });
}
