import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'signalr_client.dart';
import 'signalr_events.dart';

/// Payload de `OrderStatusChanged` (`{ orderId, status }`) —
/// `docs/FRONTEND_GUIDE.md#signalr`.
class OrderStatusChangedEvent extends Equatable {
  const OrderStatusChangedEvent({required this.orderId, required this.status});

  final String orderId;
  final String status;

  @override
  List<Object?> get props => [orderId, status];
}

/// Stream de eventos `OrderStatusChanged` recebidos do hub — quem quiser
/// reagir (tela de acompanhamento, histórico) escuta este provider e filtra
/// pelo `orderId` de interesse. Ao reconectar, cada tela ainda deve buscar o
/// estado atual via REST (eventos perdidos durante desconexão não são
/// reenviados — `docs/FRONTEND_GUIDE.md#signalr`); este stream só evita
/// polling desnecessário enquanto conectado.
final orderStatusChangedStreamProvider =
    StreamProvider<OrderStatusChangedEvent>((ref) {
      final client = ref.watch(signalRClientProvider);
      final controller = StreamController<OrderStatusChangedEvent>.broadcast();

      client.on(SignalREvents.orderStatusChanged, (arguments) {
        if (arguments == null || arguments.isEmpty) return;
        final payload = arguments.first;
        if (payload is Map) {
          final orderId = payload['orderId']?.toString();
          final status = payload['status']?.toString();
          if (orderId != null && status != null) {
            controller.add(
              OrderStatusChangedEvent(orderId: orderId, status: status),
            );
          }
        }
      });

      ref.onDispose(() {
        client.off(SignalREvents.orderStatusChanged);
        controller.close();
      });

      return controller.stream;
    });
