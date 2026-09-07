import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/session/application/session_controller.dart';
import 'app_lifecycle_controller.dart';
import 'signalr_client.dart';

/// Liga o ciclo de vida da conexão SignalR ao estado de sessão: conecta
/// (autenticado com o access token vigente) quando o usuário está logado,
/// desconecta no logout/expiração de sessão.
///
/// Deve ser observado uma única vez, perto da raiz do app (`app.dart`), para
/// que o listener permaneça vivo durante toda a vida do aplicativo.
final realtimeConnectionControllerProvider = Provider<void>((ref) {
  ref.listen(sessionControllerProvider, (previous, next) {
    final client = ref.read(signalRClientProvider);
    final isAuthenticated = next.value?.isAuthenticated ?? false;
    if (isAuthenticated) {
      // Erros de conexão (ex.: backend fora do ar) não devem derrubar o
      // app — o hub reconecta sozinho (`withAutomaticReconnect`) e cada
      // tela que depende de tempo real já faz fallback para REST.
      client.connect().catchError((_) {});
    } else {
      client.disconnect();
    }
  }, fireImmediately: true);

  // Ao voltar do background, tenta reconectar se a sessão ainda estiver
  // autenticada (eventos perdidos na desconexão não são reenviados).
  ref.listen(appLifecycleControllerProvider, (previous, next) {
    if (previous == null || next <= previous) return;
    final isAuthenticated =
        ref.read(sessionControllerProvider).value?.isAuthenticated ?? false;
    if (isAuthenticated) {
      ref.read(signalRClientProvider).connect().catchError((_) {});
    }
  });
});
