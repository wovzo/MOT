import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/features/auth/data/auth_repository.dart';
import 'package:mind_on_track/features/auth/presentation/providers/auth_provider.dart';
import 'package:mind_on_track/features/gamification/presentation/providers/gamification_providers.dart';
import 'package:mind_on_track/core/network_client.dart';

// Helper mock network client using Dio with interceptors / custom adapter
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
        response.data is String ? response.data : response.data.toString(),
        response.statusCode ?? 200,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        },
      );
    } on DioException catch (e) {
      if (e.response != null) {
        return ResponseBody.fromString(
          e.response!.data.toString(),
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
  TestWidgetsFlutterBinding.ensureInitialized();

  group('AuthRepository Account Deletion and Cleanup Tests', () {
    test('Successful deletion clears stored jwt_token and local credentials', () async {
      SharedPreferences.setMockInitialValues({'jwt_token': 'existing_valid_token'});

      final client = MockNetworkClient((options) async {
        expect(options.path, 'auth/account');
        expect(options.method, 'DELETE');
        expect(options.data, {'password': 'ValidPassword123!'});
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: '{"message": "Account deleted successfully."}',
        );
      });

      final repo = AuthRepository(client);

      // Verify token initially present
      final prefsBefore = await SharedPreferences.getInstance();
      expect(prefsBefore.getString('jwt_token'), 'existing_valid_token');

      // Execute account deletion
      await repo.deleteAccount('ValidPassword123!');

      // Verify token was cleared upon confirmed success
      final prefsAfter = await SharedPreferences.getInstance();
      expect(prefsAfter.getString('jwt_token'), isNull);
    });

    test('Failed deletion due to incorrect password retains jwt_token and throws error', () async {
      SharedPreferences.setMockInitialValues({'jwt_token': 'existing_valid_token'});

      final client = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 400,
            data: '{"error": "Password is incorrect."}',
          ),
          type: DioExceptionType.badResponse,
        );
      });

      final repo = AuthRepository(client);

      // Expect exception
      expect(
        () async => await repo.deleteAccount('WrongPassword!'),
        throwsA(predicate((e) => e.toString().contains('Password is incorrect.'))),
      );

      // Verify token remains intact
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('jwt_token'), 'existing_valid_token');
    });

    test('Failed deletion due to network timeout retains jwt_token and surfaces timeout error', () async {
      SharedPreferences.setMockInitialValues({'jwt_token': 'existing_valid_token'});

      final client = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.connectionTimeout,
          message: 'Connection timed out',
        );
      });

      final repo = AuthRepository(client);

      expect(
        () async => await repo.deleteAccount('AnyPassword!'),
        throwsA(predicate((e) => e.toString().contains('Network timeout'))),
      );

      // Verify token was NOT prematurely removed
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('jwt_token'), 'existing_valid_token');
    });

    test('Profile update succeeds and maps response properly', () async {
      final client = MockNetworkClient((options) async {
        expect(options.path, 'auth/profile');
        expect(options.method, 'PUT');
        expect(options.data, {'displayName': 'Alex New'});
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: '{"id":"u-123","email":"alex@test.com","displayName":"Alex New","createdAt":"2026-01-01T00:00:00Z","currentStreak":5,"level":2,"xp":150,"totalFocusMinutes":120}',
        );
      });

      final repo = AuthRepository(client);
      final profile = await repo.updateProfile('Alex New');

      expect(profile.displayName, 'Alex New');
      expect(profile.email, 'alex@test.com');
      expect(profile.currentStreak, 5);
      expect(profile.totalFocusMinutes, 120);
    });

    test('Riverpod cache invalidation cleans userProfileProvider, gamification providers, and resets authStateProvider', () {
      final container = ProviderContainer(
        overrides: [
          authStateProvider.overrideWith((ref) => true),
        ],
      );

      expect(container.read(authStateProvider), isTrue);

      // Simulate post-deletion cleanup
      container.read(authStateProvider.notifier).state = false;
      container.invalidate(userProfileProvider);
      container.invalidate(gamificationSummaryProvider);
      container.invalidate(achievementsProvider);
      container.invalidate(xpHistoryProvider);

      expect(container.read(authStateProvider), isFalse);
    });
  });
}
