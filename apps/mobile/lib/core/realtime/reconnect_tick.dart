import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Incrementado quando o hub reconecta. Telas usam isso para refetch REST
/// (eventos perdidos na janela desconectada não são reenviados).
class RealtimeReconnectTick extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final realtimeReconnectTickProvider =
    NotifierProvider<RealtimeReconnectTick, int>(RealtimeReconnectTick.new);
