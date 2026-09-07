import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'signalr_client.dart';
import 'signalr_events.dart';

/// Payload de `DeliveryCompleted` (`{ orderId }`) —
/// `docs/FRONTEND_GUIDE.md#signalr`.
class DeliveryCompletedEvent extends Equatable {
  const DeliveryCompletedEvent({required this.orderId});

  final String orderId;

  @override
  List<Object?> get props => [orderId];
}

final deliveryCompletedStreamProvider = StreamProvider<DeliveryCompletedEvent>((
  ref,
) {
  final client = ref.watch(signalRClientProvider);
  final controller = StreamController<DeliveryCompletedEvent>.broadcast();

  client.on(SignalREvents.deliveryCompleted, (arguments) {
    if (arguments == null || arguments.isEmpty) return;
    final payload = arguments.first;
    if (payload is Map) {
      final orderId = payload['orderId']?.toString();
      if (orderId != null) {
        controller.add(DeliveryCompletedEvent(orderId: orderId));
      }
    }
  });

  ref.onDispose(() {
    client.off(SignalREvents.deliveryCompleted);
    controller.close();
  });

  return controller.stream;
});
