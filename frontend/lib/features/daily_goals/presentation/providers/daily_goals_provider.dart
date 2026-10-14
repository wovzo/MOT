import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/daily_goal_progress_model.dart';
import '../../data/repositories/daily_goal_repository.dart';

final dailyGoalRepositoryProvider = Provider<DailyGoalRepository>((ref) {
  return DailyGoalRepository(NetworkClient());
});

final selectedGoalDateProvider = StateProvider<String>((ref) {
  return DateTime.now().toIso8601String().split('T').first;
});

final dailyGoalProgressProvider = FutureProvider.family<DailyGoalProgress, String>((ref, date) async {
  final repository = ref.watch(dailyGoalRepositoryProvider);
  return repository.getProgress(date: date);
});
