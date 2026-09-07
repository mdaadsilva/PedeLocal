import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Incrementa um contador toda vez que o app volta do background
/// (`AppLifecycleState.resumed`). Telas de acompanhamento/entregador
/// escutam isso para refetch HTTP — eventos SignalR perdidos durante a
/// desconexão não são reenviados (`docs/FRONTEND_GUIDE.md#signalr`).
class AppLifecycleController extends Notifier<int> {
  AppLifecycleObserver? _observer;

  @override
  int build() {
    _observer = AppLifecycleObserver(onResumed: () => state++);
    WidgetsBinding.instance.addObserver(_observer!);
    ref.onDispose(() {
      final observer = _observer;
      if (observer != null) {
        WidgetsBinding.instance.removeObserver(observer);
      }
    });
    return 0;
  }
}

class AppLifecycleObserver extends WidgetsBindingObserver {
  AppLifecycleObserver({required this.onResumed});

  final VoidCallback onResumed;

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) onResumed();
  }
}

final appLifecycleControllerProvider =
    NotifierProvider<AppLifecycleController, int>(AppLifecycleController.new);
