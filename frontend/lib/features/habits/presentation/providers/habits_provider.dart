import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/habit_model.dart';
import '../../data/repositories/habit_repository.dart';

final habitRepositoryProvider = Provider<HabitRepository>((ref) {
  return HabitRepository(NetworkClient());
});

final habitsProvider = FutureProvider.autoDispose<List<Habit>>((ref) async {
  final repository = ref.watch(habitRepositoryProvider);
  return repository.getHabits();
});
