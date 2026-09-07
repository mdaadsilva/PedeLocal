import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';

/// `POST /api/auth/forgot-password` sempre retorna sucesso (204), exista ou
/// não o e-mail (`docs/AUTH_FLOW.md` — evita enumeração de contas). A tela
/// deve mostrar a mesma mensagem de confirmação em qualquer caso de sucesso.
class ForgotPasswordController extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<void> submit(String email) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(authRepositoryProvider).forgotPassword(email),
    );
  }
}

final forgotPasswordControllerProvider =
    AsyncNotifierProvider.autoDispose<ForgotPasswordController, void>(
      ForgotPasswordController.new,
    );
