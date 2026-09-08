import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/realtime/app_lifecycle_controller.dart';
import '../../../core/realtime/delivery_completed_events.dart';
import '../../../core/realtime/order_events.dart';
import '../../../core/realtime/order_status_progression.dart';
import '../../../core/realtime/reconnect_tick.dart';
import '../data/orders_repository.dart';
import '../domain/order.dart';
import 'delivery_code_controller.dart';

/// Acompanhamento de um pedido: aplica `OrderStatusChanged` no estado na
/// hora e reconcilia via `GET /api/orders/{id}` (sem loading que apague a UI).
class OrderTrackingController extends AsyncNotifier<Order> {
  OrderTrackingController(this.orderId);

  final String orderId;

  @override
  Future<Order> build() async {
    ref.listen(orderStatusChangedStreamProvider, (previous, next) {
      next.whenData((event) {
        if (event.orderId == orderId) {
          applyStatus(event.status);
        }
      });
    });

    ref.listen(deliveryCompletedStreamProvider, (previous, next) {
      next.whenData((event) {
        if (event.orderId != orderId) return;
        final current = state.value;
        applyStatus(current?.isPickup == true ? 'PickupCompleted' : 'Delivered');
      });
    });

    ref.listen(appLifecycleControllerProvider, (previous, next) {
      if (previous != null && next > previous) {
        silentRefresh();
      }
    });

    ref.listen(realtimeReconnectTickProvider, (previous, next) {
      if (previous != null && next > previous) {
        silentRefresh();
      }
    });

    return ref.read(ordersRepositoryProvider).getById(orderId);
  }

  void applyStatus(String status) {
    final current = state.value;
    if (current == null) {
      silentRefresh();
      return;
    }
    if (!shouldApplyOrderStatus(current.status, status)) {
      return;
    }
    state = AsyncValue.data(current.copyWith(status: status));
    if (current.copyWith(status: status).shouldShowDeliveryCodeNotice) {
      ref.invalidate(deliveryCodeControllerProvider(orderId));
    }
    silentRefresh();
  }

  Future<void> silentRefresh() async {
    try {
      final order = await ref.read(ordersRepositoryProvider).getById(orderId);
      state = AsyncValue.data(order);
    } catch (_) {
      // REST offline: mantém o estado já aplicado pelo evento.
    }
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(ordersRepositoryProvider).getById(orderId),
    );
  }
}

final orderTrackingControllerProvider =
    AsyncNotifierProvider.family<OrderTrackingController, Order, String>(
      OrderTrackingController.new,
    );
