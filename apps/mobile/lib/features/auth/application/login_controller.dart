import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../session/application/session_controller.dart';
import '../data/auth_repository.dart';

/// Controller de ação da tela de Login. Estado `idle` (nunca lido antes do
/// primeiro `login`), `loading` durante a chamada, `data(null)` em sucesso
/// (a tela reage navegando) e `error` com o [AppException] amigável.
class LoginController extends AsyncNotifier<void> {
  @override
  Future<void> build() async {
    // Idle — nada a carregar na entrada da tela.
  }

  Future<void> login({required String email, required String password}) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      await ref
          .read(authRepositoryProvider)
          .login(email: email, password: password);
      await ref.read(sessionControllerProvider.notifier).refreshAfterLogin();
    });
  }
}

final loginControllerProvider =
    AsyncNotifierProvider.autoDispose<LoginController, void>(
      LoginController.new,
    );
