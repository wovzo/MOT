import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/network_client.dart';
import '../../data/auth_repository.dart';

final networkClientProvider = Provider((ref) => NetworkClient());

final authRepositoryProvider = Provider((ref) {
  final client = ref.watch(networkClientProvider);
  return AuthRepository(client);
});

final authStateProvider = StateProvider<bool>((ref) => false);
