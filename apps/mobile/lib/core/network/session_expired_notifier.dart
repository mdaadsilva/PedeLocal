import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Canal desacoplado para o [AuthInterceptor] avisar a camada de sessão que
/// o refresh token também falhou (sessão definitivamente expirada —
/// `docs/AUTH_FLOW.md`: "trate isso como sessão expirada e mande o usuário
/// para o login").
///
/// Existe como um provider isolado (sem depender de `apiClientProvider` nem
/// de `sessionControllerProvider`) justamente para não criar uma dependência
/// circular entre os dois: `apiClientProvider` escreve neste canal,
/// `sessionControllerProvider` escuta.
final sessionExpiredControllerProvider = Provider<StreamController<void>>((
  ref,
) {
  final controller = StreamController<void>.broadcast();
  ref.onDispose(controller.close);
  return controller;
});

final sessionExpiredStreamProvider = StreamProvider<void>((ref) {
  return ref.watch(sessionExpiredControllerProvider).stream;
});
