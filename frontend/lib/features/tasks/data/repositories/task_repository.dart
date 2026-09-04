import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import 'models/daily_task.dart';
import 'package:shared_preferences/shared_preferences.dart';

class TaskRepository {
  final NetworkClient _networkClient;

  TaskRepository(this._networkClient);

  Future<List<DailyTask>> getTasks() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('jwt_token');

    try {
      final response = await _networkClient.dio.get(
        'tasks',
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      
      final List<dynamic> data = response.data;
      return data.map((json) => DailyTask.fromJson(json)).toList();
    } on DioException catch (e) {
      throw Exception(e.response?.data?['error'] ?? 'Failed to load tasks');
    }
  }

  Future<void> createTask(String title, String? description) async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('jwt_token');

    try {
      await _networkClient.dio.post(
        'tasks',
        data: {
          'title': title,
          'description': description,
        },
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
    } on DioException catch (e) {
      throw Exception(e.response?.data?['error'] ?? 'Failed to create task');
    }
  }

  Future<void> toggleTask(String id) async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('jwt_token');

    try {
      await _networkClient.dio.patch(
        'tasks/$id/toggle',
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
    } on DioException catch (e) {
      throw Exception(e.response?.data?['error'] ?? 'Failed to toggle task');
    }
  }
}
