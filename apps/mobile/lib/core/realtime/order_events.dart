import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'hub_payload.dart';
import 'signalr_client.dart';
import 'signalr_events.dart';

class OrderStatusChangedEvent extends Equatable {
  const OrderStatusChangedEvent({required this.orderId, required this.status});

  final String orderId;
  final String status;

  @override
  List<Object?> get props => [orderId, status];
}

final orderStatusChangedStreamProvider =
    StreamProvider<OrderStatusChangedEvent>((ref) {
      final client = ref.watch(signalRClientProvider);
      final controller = StreamController<OrderStatusChangedEvent>.broadcast();

      client.on(SignalREvents.orderStatusChanged, (arguments) {
        final payload = readHubPayload(arguments);
        if (payload == null) return;
        final orderId = readHubString(payload, 'orderId');
        final status = readHubString(payload, 'status');
        if (orderId != null && status != null) {
          controller.add(
            OrderStatusChangedEvent(orderId: orderId, status: status),
          );
        }
      });

      ref.onDispose(() {
        client.off(SignalREvents.orderStatusChanged);
        controller.close();
      });

      return controller.stream;
    });
