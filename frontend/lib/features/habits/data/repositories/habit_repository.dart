import '../../../../core/network_client.dart';
import '../models/habit_model.dart';

class HabitRepository {
  final NetworkClient _client;

  HabitRepository(this._client);

  Future<List<Habit>> getHabits() async {
    final response = await _client.dio.get('/habits');
    final List<dynamic> data = response.data;
    return data.map((json) => Habit.fromJson(json)).toList();
  }

  Future<void> createHabit(String title, String description) async {
    await _client.dio.post('/habits', data: {
      'title': title,
      'description': description,
    });
  }

  Future<bool> toggleHabit(String habitId) async {
    final response = await _client.dio.post('/habits/$habitId/toggle');
    return response.data['isCompletedToday'] ?? false;
  }
}
