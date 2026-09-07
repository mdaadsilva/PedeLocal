import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/cities/application/selected_city_controller.dart';
import '../../features/session/application/session_controller.dart';

/// Ponte entre o estado do Riverpod (sessão + cidade selecionada) e o
/// `refreshListenable` do `GoRouter` — sempre que qualquer um dos dois
/// mudar, o `redirect` do router é reavaliado automaticamente.
class RouterRefreshNotifier extends ChangeNotifier {
  RouterRefreshNotifier(Ref ref) {
    ref.listen(sessionControllerProvider, (_, _) => notifyListeners());
    ref.listen(selectedCityControllerProvider, (_, _) => notifyListeners());
  }
}

final routerRefreshNotifierProvider = Provider<RouterRefreshNotifier>((ref) {
  final notifier = RouterRefreshNotifier(ref);
  ref.onDispose(notifier.dispose);
  return notifier;
});
