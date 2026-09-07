import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'signalr_client.dart';
import 'signalr_events.dart';

/// Payload de `DeliveryAvailable` (`{ deliveryId }`) — recebido pelos
/// entregadores online da cidade (`docs/DELIVERY_FLOW.md#tempo-real-signalr`).
class DeliveryAvailableEvent extends Equatable {
  const DeliveryAvailableEvent({required this.deliveryId});

  final String deliveryId;

  @override
  List<Object?> get props => [deliveryId];
}

/// Stream de eventos `DeliveryAvailable` — a Home do Entregador escuta para
/// atualizar a lista de entregas disponíveis sem polling contínuo. Ao
/// reconectar, a tela ainda deve buscar o estado atual via REST (eventos
/// perdidos durante desconexão não são reenviados —
/// `docs/FRONTEND_GUIDE.md#signalr`).
final deliveryAvailableStreamProvider = StreamProvider<DeliveryAvailableEvent>((
  ref,
) {
  final client = ref.watch(signalRClientProvider);
  final controller = StreamController<DeliveryAvailableEvent>.broadcast();

  client.on(SignalREvents.deliveryAvailable, (arguments) {
    if (arguments == null || arguments.isEmpty) return;
    final payload = arguments.first;
    if (payload is Map) {
      final deliveryId = payload['deliveryId']?.toString();
      if (deliveryId != null) {
        controller.add(DeliveryAvailableEvent(deliveryId: deliveryId));
      }
    }
  });

  ref.onDispose(() {
    client.off(SignalREvents.deliveryAvailable);
    controller.close();
  });

  return controller.stream;
});
