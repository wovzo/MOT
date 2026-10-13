import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/achievement_item.dart';
import '../../data/models/gamification_summary.dart';
import '../../data/models/xp_transaction_item.dart';
import '../../data/repositories/gamification_repository.dart';

final gamificationRepositoryProvider = Provider<GamificationRepository>((ref) {
  final networkClient = ref.watch(networkClientProvider);
  return GamificationRepository(networkClient);
});

final gamificationSummaryProvider = FutureProvider<GamificationSummary>((ref) async {
  final repo = ref.watch(gamificationRepositoryProvider);
  return await repo.getSummary();
});

final achievementsProvider = FutureProvider<List<AchievementItem>>((ref) async {
  final repo = ref.watch(gamificationRepositoryProvider);
  return await repo.getAchievements();
});

final xpHistoryProvider = FutureProvider<List<XpTransactionItem>>((ref) async {
  final repo = ref.watch(gamificationRepositoryProvider);
  return await repo.getHistory(limit: 20);
});
