import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:dio/dio.dart';
import 'package:mind_on_track/core/network_client.dart';
import 'package:mind_on_track/features/subjects/data/models/subject_model.dart';
import 'package:mind_on_track/features/subjects/data/repositories/subject_repository.dart';

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
  group('Subject Model Tests', () {
    test('fromJson parses subject correctly', () {
      final json = {
        'id': 'sub-123',
        'name': 'Mathematics',
        'description': 'Linear Algebra & Calculus',
        'colorHex': '#00D9C0',
        'createdAt': '2026-10-14T10:00:00Z',
        'updatedAt': '2026-10-14T12:00:00Z',
      };

      final subject = Subject.fromJson(json);

      expect(subject.id, 'sub-123');
      expect(subject.name, 'Mathematics');
      expect(subject.description, 'Linear Algebra & Calculus');
      expect(subject.colorHex, '#00D9C0');
      expect(subject.updatedAt, isNotNull);
    });

    test('fromJson handles null and missing fields gracefully', () {
      final json = {
        'id': 'sub-456',
        'name': 'History',
      };

      final subject = Subject.fromJson(json);

      expect(subject.id, 'sub-456');
      expect(subject.name, 'History');
      expect(subject.description, isNull);
      expect(subject.colorHex, '#6C5CE7');
      expect(subject.updatedAt, isNull);
    });

    test('toJson serializes subject correctly', () {
      final subject = Subject(
        id: 'sub-789',
        name: 'Chemistry',
        description: 'Organic & Inorganic',
        colorHex: '#FF7675',
        createdAt: DateTime.parse('2026-10-14T08:00:00Z'),
      );

      final json = subject.toJson();

      expect(json['id'], 'sub-789');
      expect(json['name'], 'Chemistry');
      expect(json['description'], 'Organic & Inorganic');
      expect(json['colorHex'], '#FF7675');
    });
  });

  group('SubjectRepository Tests', () {
    test('getSubjects parses list of subjects from endpoint', () async {
      String? requestedPath;
      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: [
            {
              'id': 'sub-1',
              'name': 'Physics',
              'colorHex': '#6C5CE7',
              'createdAt': '2026-10-14T10:00:00Z',
            },
            {
              'id': 'sub-2',
              'name': 'Biology',
              'colorHex': '#00D9C0',
              'createdAt': '2026-10-14T10:30:00Z',
            },
          ],
        );
      });

      final repo = SubjectRepository(mockClient);
      final subjects = await repo.getSubjects();

      expect(requestedPath, 'subjects');
      expect(subjects.length, 2);
      expect(subjects[0].name, 'Physics');
      expect(subjects[1].name, 'Biology');
    });

    test('createSubject sends POST with payload and parses created subject', () async {
      String? requestedMethod;
      dynamic requestBody;

      final mockClient = MockNetworkClient((options) async {
        requestedMethod = options.method;
        requestBody = options.data;
        return Response(
          requestOptions: options,
          statusCode: 201,
          data: {
            'id': 'sub-new',
            'name': 'Literature',
            'description': 'World Classics',
            'colorHex': '#FDCB6E',
            'createdAt': '2026-10-14T11:00:00Z',
          },
        );
      });

      final repo = SubjectRepository(mockClient);
      final result = await repo.createSubject(
        name: 'Literature',
        description: 'World Classics',
        colorHex: '#FDCB6E',
      );

      expect(requestedMethod, 'POST');
      expect(requestBody['name'], 'Literature');
      expect(result.id, 'sub-new');
      expect(result.name, 'Literature');
    });

    test('updateSubject sends PUT and parses updated subject', () async {
      String? requestedPath;
      String? requestedMethod;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        requestedMethod = options.method;
        return Response(
          requestOptions: options,
          statusCode: 200,
          data: {
            'id': 'sub-edit',
            'name': 'Advanced Math',
            'colorHex': '#00D9C0',
            'createdAt': '2026-10-14T09:00:00Z',
            'updatedAt': '2026-10-14T11:30:00Z',
          },
        );
      });

      final repo = SubjectRepository(mockClient);
      final result = await repo.updateSubject(
        id: 'sub-edit',
        name: 'Advanced Math',
        colorHex: '#00D9C0',
      );

      expect(requestedPath, 'subjects/sub-edit');
      expect(requestedMethod, 'PUT');
      expect(result.name, 'Advanced Math');
    });

    test('deleteSubject sends DELETE to subjects/{id}', () async {
      String? requestedPath;
      String? requestedMethod;

      final mockClient = MockNetworkClient((options) async {
        requestedPath = options.path;
        requestedMethod = options.method;
        return Response(
          requestOptions: options,
          statusCode: 204,
          data: null,
        );
      });

      final repo = SubjectRepository(mockClient);
      await repo.deleteSubject('sub-delete');

      expect(requestedPath, 'subjects/sub-delete');
      expect(requestedMethod, 'DELETE');
    });

    test('createSubject surfaces duplicate error message on 400', () async {
      final mockClient = MockNetworkClient((options) async {
        throw DioException(
          requestOptions: options,
          response: Response(
            requestOptions: options,
            statusCode: 400,
            data: {'error': "A subject with the name 'Physics' already exists."},
          ),
        );
      });

      final repo = SubjectRepository(mockClient);
      expect(
        () => repo.createSubject(name: 'Physics'),
        throwsA(predicate((e) => e.toString().contains("already exists"))),
      );
    });
  });
}
