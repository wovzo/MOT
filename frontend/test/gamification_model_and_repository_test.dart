import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/gamification/data/models/achievement_item.dart';
import 'package:mind_on_track/features/gamification/data/models/gamification_summary.dart';
import 'package:mind_on_track/features/gamification/data/models/xp_transaction_item.dart';
import 'package:mind_on_track/features/gamification/data/repositories/gamification_repository.dart';

// Mock NetworkClient using Dio with custom HttpClientAdapter
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
      final dataStr = response.data is String ? response.data : jsonEncode(response.data);
      return ResponseBody.fromString(
        dataStr,
        response.statusCode ?? 200,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        },
      );
    } on DioException catch (e) {
      if (e.response != null) {
        final dataStr = e.response!.data is String
            ? e.response!.data
            : jsonEncode(e.response!.data);
        return ResponseBody.fromString(
          dataStr,
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
  group('GamificationSummary Model Tests', () {
    test('fromJson correctly parses complete summary JSON', () {
      final json = {
        'level': 2,
        'totalXp': 250,
        'currentLevelThresholdXp': 100,
        'nextLevelThresholdXp': 300,
        'xpEarnedInCurrentLevel': 150,
        'xpRequiredForNextLevel': 200,
        'progressFraction': 0.75,
        'progressPercentage': 75.0,
        'totalAchievements': 8,
        'unlockedAchievements': 3,
      };

      final summary = GamificationSummary.fromJson(json);

      expect(summary.level, 2);
      expect(summary.totalXp, 250);
      expect(summary.currentLevelThresholdXp, 100);
      expect(summary.nextLevelThresholdXp, 300);
      expect(summary.xpEarnedInCurrentLevel, 150);
      expect(summary.xpRequiredForNextLevel, 200);
      expect(summary.progressFraction, 0.75);
      expect(summary.progressPercentage, 75.0);
      expect(summary.totalAchievements, 8);
      expect(summary.unlockedAchievements, 3);
    });

    test('fromJson handles zero progress and boundary values correctly', () {
      final json = {
        'level': 1,
        'totalXp': 0,
        'currentLevelThresholdXp': 0,
        'nextLevelThresholdXp': 100,
        'xpEarnedInCurrentLevel': 0,
        'xpRequiredForNextLevel': 100,
        'progressFraction': 0.0,
        'progressPercentage': 0.0,
        'totalAchievements': 8,
        'unlockedAchievements': 0,
      };

      final summary = GamificationSummary.fromJson(json);

      expect(summary.level, 1);
      expect(summary.totalXp, 0);
      expect(summary.progressFraction, 0.0);
      expect(summary.progressPercentage, 0.0);
      expect(summary.unlockedAchievements, 0);
    });

    test('fromJson clamps progressFraction to [0.0, 1.0]', () {
      final overclampedJson = {
        'level': 3,
        'totalXp': 500,
        'progressFraction': 1.25,
      };

      final summaryOver = GamificationSummary.fromJson(overclampedJson);
      expect(summaryOver.progressFraction, 1.0);

      final underclampedJson = {
        'level': 1,
        'totalXp': 0,
        'progressFraction': -0.5,
      };

      final summaryUnder = GamificationSummary.fromJson(underclampedJson);
      expect(summaryUnder.progressFraction, 0.0);
    });

    test('fromJson safely falls back to defaults when fields are missing or null', () {
      final summary = GamificationSummary.fromJson({});

      expect(summary.level, 1);
      expect(summary.totalXp, 0);
      expect(summary.currentLevelThresholdXp, 0);
      expect(summary.nextLevelThresholdXp, 100);
      expect(summary.xpEarnedInCurrentLevel, 0);
      expect(summary.xpRequiredForNextLevel, 100);
      expect(summary.progressFraction, 0.0);
      expect(summary.progressPercentage, 0.0);
      expect(summary.totalAchievements, 8);
      expect(summary.unlockedAchievements, 0);
    });

    test('fromJson parses string numeric representations safely', () {
      final json = {
        'level': '4',
        'totalXp': '650',
        'progressFraction': '0.5',
        'progressPercentage': '50',
      };

      final summary = GamificationSummary.fromJson(json);

      expect(summary.level, 4);
      expect(summary.totalXp, 650);
      expect(summary.progressFraction, 0.5);
      expect(summary.progressPercentage, 50.0);
    });
  });

  group('AchievementItem Model Tests', () {
    test('fromJson parses unlocked achievement with valid date correctly', () {
      final json = {
        'code': 'FIRST_STEP',
        'title': 'First Step',
        'description': 'Complete your first daily task',
        'iconName': 'check_circle',
        'xpReward': 25,
        'isUnlocked': true,
        'unlockedAt': '2026-10-13T12:00:00.000Z',
      };

      final item = AchievementItem.fromJson(json);

      expect(item.code, 'FIRST_STEP');
      expect(item.title, 'First Step');
      expect(item.description, 'Complete your first daily task');
      expect(item.iconName, 'check_circle');
      expect(item.xpReward, 25);
      expect(item.isUnlocked, isTrue);
      expect(item.unlockedAt, isNotNull);
      expect(item.unlockedAt!.toUtc().year, 2026);
    });

    test('fromJson parses locked achievement with null unlockedAt correctly', () {
      final json = {
        'code': 'FOCUS_INITIATE',
        'title': 'Focus Initiate',
        'description': 'Complete your first study session of at least 15 minutes',
        'iconName': 'timer',
        'xpReward': 25,
        'isUnlocked': false,
        'unlockedAt': null,
      };

      final item = AchievementItem.fromJson(json);

      expect(item.code, 'FOCUS_INITIATE');
      expect(item.isUnlocked, isFalse);
      expect(item.unlockedAt, isNull);
    });

    test('fromJson safely handles missing fields and invalid dates', () {
      final json = {
        'code': 'TEST',
        'unlockedAt': 'not-a-valid-date',
      };

      final item = AchievementItem.fromJson(json);

      expect(item.code, 'TEST');
      expect(item.title, '');
      expect(item.xpReward, 0);
      expect(item.isUnlocked, isFalse);
      expect(item.unlockedAt, isNull);
    });
  });

  group('XpTransactionItem Model Tests', () {
    test('fromJson parses normal transaction correctly', () {
      final json = {
        'id': 'abc-123',
        'amount': 35,
        'sourceType': 'STUDYSESSION',
        'description': 'Completed 50-minute study session',
        'createdAt': '2026-10-13T14:30:00.000Z',
      };

      final item = XpTransactionItem.fromJson(json);

      expect(item.id, 'abc-123');
      expect(item.amount, 35);
      expect(item.sourceType, 'STUDYSESSION');
      expect(item.description, 'Completed 50-minute study session');
      expect(item.createdAt.toUtc().year, 2026);
    });

    test('fromJson falls back gracefully when fields are missing or null', () {
      final item = XpTransactionItem.fromJson({});

      expect(item.id, '');
      expect(item.amount, 0);
      expect(item.sourceType, '');
      expect(item.description, '');
      expect(item.createdAt, isNotNull);
    });
  });

  group('GamificationRepository Tests', () {
    test('getSummary fetches and maps summary successfully', () async {
      final mockClient = MockNetworkClient((options) async {
        expect(options.path, 'gamification/summary');
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'level': 3,
            'totalXp': 450,
            'currentLevelThresholdXp': 300,
            'nextLevelThresholdXp': 600,
            'xpEarnedInCurrentLevel': 150,
            'xpRequiredForNextLevel': 300,
            'progressFraction': 0.5,
            'progressPercentage': 50.0,
            'totalAchievements': 8,
            'unlockedAchievements': 4,
          },
        );
      });

      final repo = GamificationRepository(mockClient);
      final summary = await repo.getSummary();

      expect(summary.level, 3);
      expect(summary.totalXp, 450);
      expect(summary.progressPercentage, 50.0);
      expect(summary.unlockedAchievements, 4);
    });

    test('getSummary throws on malformed response format', () async {
      final mockClient = MockNetworkClient((options) async {
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: 'Not a map object',
        );
      });

      final repo = GamificationRepository(mockClient);
      expect(() => repo.getSummary(), throwsA(isA<Exception>()));
    });

    test('getSummary handles network timeouts gracefully', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.connectionTimeout,
        );
      });

      final repo = GamificationRepository(mockClient);
      expect(
        () => repo.getSummary(),
        throwsA(predicate((e) => e.toString().contains('Network timeout'))),
      );
    });

    test('getAchievements fetches and maps list of achievements', () async {
      final mockClient = MockNetworkClient((options) async {
        expect(options.path, 'gamification/achievements');
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: [
            {
              'code': 'FIRST_STEP',
              'title': 'First Step',
              'description': 'Complete first task',
              'iconName': 'check_circle',
              'xpReward': 25,
              'isUnlocked': true,
              'unlockedAt': '2026-10-13T10:00:00Z',
            },
            {
              'code': 'FOCUS_INITIATE',
              'title': 'Focus Initiate',
              'description': 'Complete 15m session',
              'iconName': 'timer',
              'xpReward': 25,
              'isUnlocked': false,
              'unlockedAt': null,
            },
          ],
        );
      });

      final repo = GamificationRepository(mockClient);
      final achievements = await repo.getAchievements();

      expect(achievements.length, 2);
      expect(achievements[0].code, 'FIRST_STEP');
      expect(achievements[0].isUnlocked, isTrue);
      expect(achievements[1].code, 'FOCUS_INITIATE');
      expect(achievements[1].isUnlocked, isFalse);
    });

    test('getAchievements surfaces server error message on failure', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 500,
            data: {'error': 'Database query failed'},
          ),
        );
      });

      final repo = GamificationRepository(mockClient);
      expect(
        () => repo.getAchievements(),
        throwsA(predicate((e) => e.toString().contains('Database query failed'))),
      );
    });

    test('getHistory passes limit parameter and maps transactions', () async {
      final mockClient = MockNetworkClient((options) async {
        expect(options.path, 'gamification/history');
        expect(options.queryParameters['limit'], 10);
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: [
            {
              'id': 'tx-1',
              'amount': 20,
              'sourceType': 'HABIT',
              'description': 'Completed habit: Exercise',
              'createdAt': '2026-10-13T15:00:00Z',
            },
          ],
        );
      });

      final repo = GamificationRepository(mockClient);
      final history = await repo.getHistory(limit: 10);

      expect(history.length, 1);
      expect(history[0].id, 'tx-1');
      expect(history[0].amount, 20);
      expect(history[0].sourceType, 'HABIT');
    });

    test('getHistory returns empty list when user has zero transactions', () async {
      final mockClient = MockNetworkClient((options) async {
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: [],
        );
      });

      final repo = GamificationRepository(mockClient);
      final history = await repo.getHistory();

      expect(history, isEmpty);
    });

    test('getHistory surfaces validation error message on 400 Bad Request', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 400,
            data: {'error': 'Limit must be between 1 and 100.'},
          ),
        );
      });

      final repo = GamificationRepository(mockClient);
      expect(
        () => repo.getHistory(limit: 0),
        throwsA(predicate((e) => e.toString().contains('Limit must be between 1 and 100.'))),
      );
    });
  });
}
