import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/realtime/app_lifecycle_controller.dart';
import '../../../core/realtime/delivery_completed_events.dart';
import '../../../core/realtime/order_events.dart';
import '../data/orders_repository.dart';
import '../domain/order.dart';

/// Acompanhamento de um pedido específico — refaz o `GET /api/orders/{id}`
/// sempre que chega um `OrderStatusChanged` relevante via SignalR (nunca faz
/// polling contínuo), além de suportar pull-to-refresh manual.
class OrderTrackingController extends AsyncNotifier<Order> {
  OrderTrackingController(this.orderId);

  final String orderId;

  @override
  Future<Order> build() async {
    // Reage a mudanças de status em tempo real deste pedido específico —
    // eventos de outros pedidos são ignorados.
    ref.listen(orderStatusChangedStreamProvider, (previous, next) {
      next.whenData((event) {
        if (event.orderId == orderId) {
          refresh();
        }
      });
    });

    ref.listen(deliveryCompletedStreamProvider, (previous, next) {
      next.whenData((event) {
        if (event.orderId == orderId) {
          refresh();
        }
      });
    });

    ref.listen(appLifecycleControllerProvider, (previous, next) {
      if (previous != null && next > previous) {
        refresh();
      }
    });

    return ref.read(ordersRepositoryProvider).getById(orderId);
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
