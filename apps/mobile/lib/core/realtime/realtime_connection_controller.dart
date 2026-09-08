import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/session/application/session_controller.dart';
import 'app_lifecycle_controller.dart';
import 'reconnect_tick.dart';
import 'signalr_client.dart';

/// Liga o ciclo de vida da conexão SignalR à sessão: conecta após
/// autenticação, reconecta ao voltar do background, desconecta no logout.
///
/// Observar uma única vez na raiz (`app.dart`) para não duplicar conexões.
final realtimeConnectionControllerProvider = Provider<void>((ref) {
  final client = ref.read(signalRClientProvider);

  void onReconnected() {
    ref.read(realtimeReconnectTickProvider.notifier).bump();
  }

  client.addReconnectedListener(onReconnected);
  ref.onDispose(() => client.removeReconnectedListener(onReconnected));

  ref.listen(sessionControllerProvider, (previous, next) {
    final isAuthenticated = next.value?.isAuthenticated ?? false;
    if (isAuthenticated) {
      client.connect().catchError((_) {});
    } else {
      client.disconnect();
    }
  }, fireImmediately: true);

  ref.listen(appLifecycleControllerProvider, (previous, next) {
    if (previous == null || next <= previous) return;
    final isAuthenticated =
        ref.read(sessionControllerProvider).value?.isAuthenticated ?? false;
    if (isAuthenticated) {
      ref.read(signalRClientProvider).connect().catchError((_) {});
    }
  });
});
