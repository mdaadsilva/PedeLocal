import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../session/application/session_controller.dart';
import '../data/me_repository.dart';

/// Exclusão de conta (`DELETE /api/me/account` — anonimiza e desativa,
/// `docs/API_CONTRACT.md#minha-conta`). Ação irreversível — a confirmação
/// explícita é responsabilidade da UI antes de chamar [deleteAccount].
class DeleteAccountController extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<void> deleteAccount() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      await ref.read(meRepositoryProvider).deleteAccount();
      await ref.read(sessionControllerProvider.notifier).logout();
    });
  }
}

final deleteAccountControllerProvider =
    AsyncNotifierProvider.autoDispose<DeleteAccountController, void>(
      DeleteAccountController.new,
    );
