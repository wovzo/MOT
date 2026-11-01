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

  Future<DailyTask> createTask(String title, String? description) async {
    try {
      final response = await _networkClient.dio.post(
        'tasks',
        data: {
          'title': title,
          'description': description,
        },
      );
      final data = response.data;
      if (data is Map<String, dynamic>) {
        return DailyTask.fromJson(data);
      }
      return DailyTask(
        id: data is Map && data['id'] != null ? data['id'].toString() : '',
        title: title,
        description: description,
        isCompleted: false,
        createdAt: DateTime.now(),
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to create task'));
    }
  }

  Future<DailyTask> updateTask(String id, String title, String? description, {bool? isCompleted}) async {
    try {
      final payload = <String, dynamic>{
        'title': title,
        'description': description,
      };
      if (isCompleted != null) {
        payload['isCompleted'] = isCompleted;
      }
      final response = await _networkClient.dio.put(
        'tasks/$id',
        data: payload,
      );
      return DailyTask.fromJson(response.data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to update task'));
    }
  }

  Future<void> deleteTask(String id) async {
    try {
      await _networkClient.dio.delete('tasks/$id');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to delete task'));
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
