import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/models/dashboard_summary_model.dart';
import '../../data/repositories/dashboard_repository.dart';

final dashboardRepositoryProvider = Provider<DashboardRepository>((ref) {
  return DashboardRepository(NetworkClient());
});

final dashboardSummaryProvider = FutureProvider.autoDispose<DashboardSummary>((ref) async {
  final repo = ref.watch(dashboardRepositoryProvider);
  final offset = DateTime.now().timeZoneOffset.inMinutes;
  return repo.getDashboardSummary(timeZoneOffsetMinutes: offset);
});
