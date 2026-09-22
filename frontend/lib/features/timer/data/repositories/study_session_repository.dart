import '../../../../core/network_client.dart';
import '../models/study_session_model.dart';

class StudySessionRepository {
  final NetworkClient _networkClient;

  StudySessionRepository(this._networkClient);

  Future<List<StudySession>> getStudySessions() async {
    final response = await _networkClient.dio.get('/studysessions');
    final List<dynamic> data = response.data;
    return data.map((json) => StudySession.fromJson(json)).toList();
  }

  Future<StudySession> startSession(String title) async {
    final response = await _networkClient.dio.post(
      '/studysessions/start',
      data: {'title': title},
    );
    return StudySession.fromJson(response.data);
  }

  Future<StudySession> endSession(String sessionId) async {
    final response = await _networkClient.dio.post('/studysessions/$sessionId/end');
    return StudySession.fromJson(response.data);
  }
}
