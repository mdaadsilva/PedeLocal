import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/network/session_expired_notifier.dart';
import '../../../core/notifications/push_notification_service.dart';
import '../../../core/storage/secure_storage_service.dart';
import '../../auth/data/auth_repository.dart';
import '../../me/data/me_repository.dart';
import '../domain/session_state.dart';
import '../domain/user_role.dart';

/// Fonte única de verdade sobre "estou logado? com qual papel?" no
/// aplicativo. A Splash Screen observa este provider: enquanto está em
/// `AsyncLoading`, mostra o indicador de carregamento; quando resolve,
/// decide a rota inicial a partir de [SessionState].
///
/// Resolução (`build`): existe token salvo? Se sim, confirma com o backend
/// via `GET /api/me/profile` (nunca confia apenas em dados locais/JWT) e
/// deriva a role. Se a confirmação falhar mesmo após a tentativa automática
/// de refresh do `AuthInterceptor`, trata como sessão inválida.
class SessionController extends AsyncNotifier<SessionState> {
  @override
  Future<SessionState> build() async {
    // Qualquer notificação de "refresh token também falhou" (emitida pelo
    // AuthInterceptor) deve colapsar a sessão imediatamente, de qualquer
    // tela em que o usuário estiver.
    ref.listen(sessionExpiredStreamProvider, (previous, next) {
      if (next is AsyncData<void>) {
        state = const AsyncValue.data(SessionState.unauthenticated());
      }
    });

    return _resolveSession();
  }

  Future<SessionState> _resolveSession() async {
    final hasSession = await ref
        .read(secureStorageServiceProvider)
        .hasSession();
    if (!hasSession) return const SessionState.unauthenticated();

    try {
      final profile = await ref.read(meRepositoryProvider).getProfile();
      final role = UserRoleResolution.fromApiRoles(profile.roles);
      return SessionState.authenticated(role: role, userName: profile.name);
    } on AppException {
      await ref.read(secureStorageServiceProvider).clearTokens();
      return const SessionState.unauthenticated();
    }
  }

  /// Chamado após login/registro bem-sucedido (tokens já salvos pelo
  /// `AuthRepository`) para recalcular a sessão com a role correta.
  Future<void> refreshAfterLogin() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_resolveSession);
  }

  /// Logout explícito (ação do usuário). Remove o device token no backend
  /// **antes** de revogar a sessão local, enquanto o JWT ainda vale.
  Future<void> logout() async {
    try {
      await ref.read(pushTokenLifecycleProvider).unregisterCurrentToken();
    } catch (_) {
      // Falha de push não pode impedir o logout local + SignalR.
    }
    await ref.read(authRepositoryProvider).logout();
    state = const AsyncValue.data(SessionState.unauthenticated());
  }
}

final sessionControllerProvider =
    AsyncNotifierProvider<SessionController, SessionState>(
      SessionController.new,
    );
