import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/study_session_model.dart';

class StudySessionRepository {
  final NetworkClient _networkClient;

  StudySessionRepository(this._networkClient);

  Future<List<StudySession>> getStudySessions() async {
    try {
      final response = await _networkClient.dio.get('studysessions');
      final List<dynamic> data = response.data;
      return data.map((json) => StudySession.fromJson(json)).toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load study sessions'));
    }
  }

  Future<StudySession?> getActiveSession() async {
    try {
      final response = await _networkClient.dio.get('studysessions/active');
      if (response.statusCode == 204 || response.data == null || response.data == '') {
        return null;
      }
      return StudySession.fromJson(response.data);
    } on DioException catch (e) {
      if (e.response?.statusCode == 204) {
        return null;
      }
      throw Exception(_extractErrorMessage(e, 'Failed to retrieve active session'));
    }
  }

  Future<StudySession> startSession(String title) async {
    try {
      final response = await _networkClient.dio.post(
        'studysessions/start',
        data: {'title': title},
      );
      return StudySession.fromJson(response.data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to start study session'));
    }
  }

  Future<StudySession> endSession(String sessionId) async {
    try {
      final response = await _networkClient.dio.post('studysessions/$sessionId/end');
      return StudySession.fromJson(response.data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to end study session'));
    }
  }

  String _extractErrorMessage(DioException e, String defaultMessage) {
    final data = e.response?.data;
    if (data is Map && data['error'] != null && data['error'].toString().isNotEmpty) {
      return data['error'].toString();
    }
    return defaultMessage;
  }
}
