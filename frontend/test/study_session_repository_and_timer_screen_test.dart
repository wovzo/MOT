import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/timer/data/models/study_session_model.dart';
import 'package:mind_on_track/features/timer/data/repositories/study_session_repository.dart';
import 'package:mind_on_track/features/timer/presentation/screens/timer_screen.dart';

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

class FakeStudySessionRepository extends StudySessionRepository {
  StudySession? activeSession;
  List<StudySession> sessions;
  bool pauseCalled = false;
  bool resumeCalled = false;
  bool endCalled = false;

  FakeStudySessionRepository({
    this.activeSession,
    this.sessions = const [],
  }) : super(NetworkClient());

  @override
  Future<StudySession?> getActiveSession() async => activeSession;

  @override
  Future<List<StudySession>> getStudySessions() async => sessions;

  @override
  Future<StudySession> pauseSession(String id) async {
    pauseCalled = true;
    activeSession = StudySession(
      id: id,
      title: activeSession?.title ?? 'Test Session',
      startTime: activeSession?.startTime ?? DateTime.now(),
      durationMinutes: 2,
      isCompleted: false,
      isPaused: true,
      lastPausedAt: DateTime.now().toUtc(),
      accumulatedSeconds: 120,
      elapsedSeconds: 120,
    );
    return activeSession!;
  }

  @override
  Future<StudySession> resumeSession(String id) async {
    resumeCalled = true;
    activeSession = StudySession(
      id: id,
      title: activeSession?.title ?? 'Test Session',
      startTime: activeSession?.startTime ?? DateTime.now(),
      durationMinutes: 2,
      isCompleted: false,
      isPaused: false,
      lastPausedAt: null,
      accumulatedSeconds: 120,
      elapsedSeconds: 120,
    );
    return activeSession!;
  }

  @override
  Future<StudySession> endSession(String id) async {
    endCalled = true;
    final ended = StudySession(
      id: id,
      title: activeSession?.title ?? 'Test Session',
      startTime: activeSession?.startTime ?? DateTime.now(),
      endTime: DateTime.now(),
      durationMinutes: 2,
      isCompleted: true,
      isPaused: false,
      lastPausedAt: null,
      accumulatedSeconds: 120,
      elapsedSeconds: 120,
    );
    activeSession = null;
    return ended;
  }
}

void main() {
  group('StudySessionRepository Tests', () {
    test('pauseSession sends POST to /studysessions/{id}/pause and returns updated session', () async {
      String? requestedPath;
      String? requestedMethod;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        requestedMethod = options.method;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'id': 'sess-123',
            'title': 'Sprint Review',
            'startTime': '2026-10-14T10:00:00Z',
            'endTime': null,
            'durationMinutes': 0,
            'isCompleted': false,
            'isPaused': true,
            'lastPausedAt': '2026-10-14T10:15:00Z',
            'accumulatedSeconds': 900,
            'elapsedSeconds': 900,
          },
        );
      });

      final repository = StudySessionRepository(mockClient);
      final result = await repository.pauseSession('sess-123');

      expect(requestedPath, 'studysessions/sess-123/pause');
      expect(requestedMethod, 'POST');
      expect(result.id, 'sess-123');
      expect(result.isPaused, true);
      expect(result.accumulatedSeconds, 900);
      expect(result.elapsedSeconds, 900);
    });

    test('resumeSession sends POST to /studysessions/{id}/resume and returns updated session', () async {
      String? requestedPath;
      String? requestedMethod;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        requestedMethod = options.method;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'id': 'sess-456',
            'title': 'Deep Code Review',
            'startTime': '2026-10-14T11:00:00Z',
            'endTime': null,
            'durationMinutes': 0,
            'isCompleted': false,
            'isPaused': false,
            'lastPausedAt': null,
            'accumulatedSeconds': 600,
            'elapsedSeconds': 600,
          },
        );
      });

      final repository = StudySessionRepository(mockClient);
      final result = await repository.resumeSession('sess-456');

      expect(requestedPath, 'studysessions/sess-456/resume');
      expect(requestedMethod, 'POST');
      expect(result.id, 'sess-456');
      expect(result.isPaused, false);
      expect(result.accumulatedSeconds, 600);
    });

    test('pauseSession throws friendly exception on API error', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 400,
            data: {'error': 'Session is already paused.'},
          ),
        );
      });

      final repository = StudySessionRepository(mockClient);
      expect(
        () => repository.pauseSession('sess-error'),
        throwsA(predicate((e) => e.toString().contains('Session is already paused.'))),
      );
    });
  });

  group('TimerScreen Widget Tests', () {
    testWidgets('Renders idle timer state when no active session', (tester) async {
      final fakeRepo = FakeStudySessionRepository();

      await tester.pumpWidget(
        MaterialApp(
          home: TimerScreen(repository: fakeRepo),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Focus Timer'), findsOneWidget);
      expect(find.text('START FOCUS'), findsOneWidget);
      expect(find.text('Stopwatch'), findsOneWidget);
      expect(find.text('Pomodoro (25m)'), findsOneWidget);
      expect(find.text('No sessions yet.'), findsOneWidget);
    });

    testWidgets('Renders active running session with FOCUSING chip and pause button', (tester) async {
      final active = StudySession(
        id: 'sess-running',
        title: 'Algorithms Practice',
        startTime: DateTime.now().subtract(const Duration(minutes: 2)),
        durationMinutes: 2,
        isCompleted: false,
        isPaused: false,
        accumulatedSeconds: 120,
        elapsedSeconds: 120,
      );
      final fakeRepo = FakeStudySessionRepository(activeSession: active);

      await tester.pumpWidget(
        MaterialApp(
          home: TimerScreen(repository: fakeRepo),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('FOCUSING'), findsOneWidget);
      expect(find.byKey(const Key('pauseResumeButton')), findsOneWidget);
      expect(find.byIcon(Icons.pause_circle_filled), findsOneWidget);
      expect(find.byKey(const Key('stopButton')), findsOneWidget);
    });

    testWidgets('Tapping pause calls pauseSession and toggles to PAUSED state', (tester) async {
      final active = StudySession(
        id: 'sess-running',
        title: 'Algorithms Practice',
        startTime: DateTime.now().subtract(const Duration(minutes: 2)),
        durationMinutes: 2,
        isCompleted: false,
        isPaused: false,
        accumulatedSeconds: 120,
        elapsedSeconds: 120,
      );
      final fakeRepo = FakeStudySessionRepository(activeSession: active);

      await tester.pumpWidget(
        MaterialApp(
          home: TimerScreen(repository: fakeRepo),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('FOCUSING'), findsOneWidget);

      await tester.tap(find.byKey(const Key('pauseResumeButton')));
      await tester.pumpAndSettle();

      expect(fakeRepo.pauseCalled, isTrue);
      expect(find.text('PAUSED'), findsOneWidget);
      expect(find.byIcon(Icons.play_circle_filled), findsOneWidget);

      // Tap resume
      await tester.tap(find.byKey(const Key('pauseResumeButton')));
      await tester.pumpAndSettle();

      expect(fakeRepo.resumeCalled, isTrue);
      expect(find.text('FOCUSING'), findsOneWidget);
      expect(find.byIcon(Icons.pause_circle_filled), findsOneWidget);
    });

    testWidgets('Tapping stop calls endSession and returns to idle screen', (tester) async {
      final active = StudySession(
        id: 'sess-running',
        title: 'Algorithms Practice',
        startTime: DateTime.now().subtract(const Duration(minutes: 2)),
        durationMinutes: 2,
        isCompleted: false,
        isPaused: false,
        accumulatedSeconds: 120,
        elapsedSeconds: 120,
      );
      final fakeRepo = FakeStudySessionRepository(activeSession: active);

      await tester.pumpWidget(
        MaterialApp(
          home: TimerScreen(repository: fakeRepo),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('stopButton')));
      await tester.pumpAndSettle();

      expect(fakeRepo.endCalled, isTrue);
      expect(find.text('START FOCUS'), findsOneWidget);
    });
  });
}
