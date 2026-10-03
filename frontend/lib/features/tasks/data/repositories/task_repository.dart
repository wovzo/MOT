import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/daily_task.dart';

class TaskRepository {
  final NetworkClient _networkClient;

  TaskRepository(this._networkClient);

  Future<List<DailyTask>> getTasks() async {
    try {
      final response = await _networkClient.dio.get('tasks');
      final List<dynamic> data = response.data;
      return data.map((json) => DailyTask.fromJson(json)).toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load tasks'));
    }
  }

  Future<void> createTask(String title, String? description) async {
    try {
      await _networkClient.dio.post(
        'tasks',
        data: {
          'title': title,
          'description': description,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to create task'));
    }
  }

  Future<bool> toggleTask(String id) async {
    try {
      final response = await _networkClient.dio.patch('tasks/$id/toggle');
      final data = response.data;
      if (data is Map && data['isCompleted'] is bool) {
        return data['isCompleted'] as bool;
      }
      return false;
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to toggle task'));
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
