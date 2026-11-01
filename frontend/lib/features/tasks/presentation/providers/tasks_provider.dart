import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/daily_task.dart';
import '../../data/repositories/task_repository.dart';

final taskRepositoryProvider = Provider<TaskRepository>((ref) {
  return TaskRepository(NetworkClient());
});

final tasksProvider = FutureProvider.autoDispose<List<DailyTask>>((ref) async {
  final repository = ref.watch(taskRepositoryProvider);
  return repository.getTasks();
});
