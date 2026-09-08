import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/notifications/push_notification_coordinator.dart';
import 'core/realtime/app_lifecycle_controller.dart';
import 'core/realtime/realtime_connection_controller.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';

/// Widget raiz do PedeLocal — Material 3 + go_router configurados a partir
/// dos providers do Riverpod.
class PedeLocalApp extends ConsumerWidget {
  const PedeLocalApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Mantém a conexão SignalR sincronizada com o estado de sessão durante
    // toda a vida do app (conecta ao autenticar, desconecta no logout).
    ref.watch(realtimeConnectionControllerProvider);
    ref.watch(appLifecycleControllerProvider);
    ref.watch(pushNotificationCoordinatorProvider);
    final router = ref.watch(goRouterProvider);
    final messengerKey = ref.watch(rootScaffoldMessengerKeyProvider);

    return MaterialApp.router(
      title: 'PedeLocal',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      scaffoldMessengerKey: messengerKey,
      routerConfig: router,
    );
  }
}
