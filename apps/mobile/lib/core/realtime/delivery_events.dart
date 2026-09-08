import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'hub_payload.dart';
import 'signalr_client.dart';
import 'signalr_events.dart';

class DeliveryAvailableEvent extends Equatable {
  const DeliveryAvailableEvent({required this.deliveryId});

  final String deliveryId;

  @override
  List<Object?> get props => [deliveryId];
}

final deliveryAvailableStreamProvider = StreamProvider<DeliveryAvailableEvent>((
  ref,
) {
  final client = ref.watch(signalRClientProvider);
  final controller = StreamController<DeliveryAvailableEvent>.broadcast();

  client.on(SignalREvents.deliveryAvailable, (arguments) {
    final payload = readHubPayload(arguments);
    if (payload == null) return;
    final deliveryId = readHubString(payload, 'deliveryId');
    if (deliveryId != null) {
      controller.add(DeliveryAvailableEvent(deliveryId: deliveryId));
    }
  });

  ref.onDispose(() {
    client.off(SignalREvents.deliveryAvailable);
    controller.close();
  });

  return controller.stream;
});
