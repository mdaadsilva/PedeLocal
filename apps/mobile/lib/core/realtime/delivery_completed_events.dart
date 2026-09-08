import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'hub_payload.dart';
import 'signalr_client.dart';
import 'signalr_events.dart';

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
    final payload = readHubPayload(arguments);
    if (payload == null) return;
    final orderId = readHubString(payload, 'orderId');
    if (orderId != null) {
      controller.add(DeliveryCompletedEvent(orderId: orderId));
    }
  });

  ref.onDispose(() {
    client.off(SignalREvents.deliveryCompleted);
    controller.close();
  });

  return controller.stream;
});
