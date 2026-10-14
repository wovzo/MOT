import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/subject_model.dart';
import '../../data/repositories/subject_repository.dart';

final subjectRepositoryProvider = Provider<SubjectRepository>((ref) {
  return SubjectRepository(NetworkClient());
});

final subjectsProvider = FutureProvider<List<Subject>>((ref) async {
  final repository = ref.watch(subjectRepositoryProvider);
  return repository.getSubjects();
});
