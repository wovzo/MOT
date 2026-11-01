import 'package:dio/dio.dart';
import '../../../../core/network_client.dart';
import '../models/habit_model.dart';

class HabitRepository {
  final NetworkClient _client;

  HabitRepository(this._client);

  Future<List<Habit>> getHabits() async {
    try {
      final response = await _client.dio.get('habits');
      final List<dynamic> data = response.data;
      return data.map((json) => Habit.fromJson(json)).toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to load habits'));
    }
  }

  Future<Habit> createHabit(String title, String description) async {
    try {
      final response = await _client.dio.post('habits', data: {
        'title': title,
        'description': description,
      });
      final data = response.data;
      if (data is Map<String, dynamic>) {
        return Habit.fromJson(data);
      }
      return Habit(
        id: data is Map && data['id'] != null ? data['id'].toString() : '',
        title: title,
        description: description,
        currentStreak: 0,
        isCompletedToday: false,
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to create habit'));
    }
  }

  Future<Habit> updateHabit(String id, String title, String description) async {
    try {
      final response = await _client.dio.put('habits/$id', data: {
        'title': title,
        'description': description,
      });
      return Habit.fromJson(response.data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to update habit'));
    }
  }

  Future<void> deleteHabit(String id) async {
    try {
      await _client.dio.delete('habits/$id');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to delete habit'));
    }
  }

  Future<bool> toggleHabit(String habitId) async {
    try {
      final response = await _client.dio.post('habits/$habitId/toggle');
      final data = response.data;
      if (data is Map && data['isCompletedToday'] is bool) {
        return data['isCompletedToday'] as bool;
      }
      return false;
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(e, 'Failed to toggle habit'));
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
