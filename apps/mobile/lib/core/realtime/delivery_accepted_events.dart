import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'hub_payload.dart';
import 'signalr_client.dart';
import 'signalr_events.dart';

/// Payload de `DeliveryAccepted` para entregadores da cidade:
/// `{ deliveryId }` — sem `driverId` (`docs/REALTIME_EVENTS.md`).
class DeliveryAcceptedEvent extends Equatable {
  const DeliveryAcceptedEvent({required this.deliveryId, this.driverId});

  final String deliveryId;
  final String? driverId;

  @override
  List<Object?> get props => [deliveryId, driverId];
}

final deliveryAcceptedStreamProvider = StreamProvider<DeliveryAcceptedEvent>((
  ref,
) {
  final client = ref.watch(signalRClientProvider);
  final controller = StreamController<DeliveryAcceptedEvent>.broadcast();

  client.on(SignalREvents.deliveryAccepted, (arguments) {
    final payload = readHubPayload(arguments);
    if (payload == null) return;
    final deliveryId = readHubString(payload, 'deliveryId');
    if (deliveryId == null) return;
    controller.add(
      DeliveryAcceptedEvent(
        deliveryId: deliveryId,
        driverId: readHubString(payload, 'driverId'),
      ),
    );
  });

  ref.onDispose(() {
    client.off(SignalREvents.deliveryAccepted);
    controller.close();
  });

  return controller.stream;
});
