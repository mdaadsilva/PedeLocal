import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/realtime/order_events.dart';
import '../../../core/realtime/order_status_progression.dart';
import '../../../core/realtime/reconnect_tick.dart';
import '../data/orders_repository.dart';
import '../domain/order.dart';

/// Histórico de pedidos do cliente (`GET /api/orders?page=`).
class OrderHistoryController extends AsyncNotifier<List<Order>> {
  int _page = 1;
  int _totalPages = 1;

  @override
  Future<List<Order>> build() async {
    ref.listen(orderStatusChangedStreamProvider, (previous, next) {
      next.whenData(_applyEvent);
    });
    ref.listen(realtimeReconnectTickProvider, (previous, next) {
      if (previous != null && next > previous) {
        silentRefresh();
      }
    });

    _page = 1;
    final result = await ref.read(ordersRepositoryProvider).getMine(page: _page);
    _totalPages = result.totalPages;
    return result.items;
  }

  void _applyEvent(OrderStatusChangedEvent event) {
    final items = state.value;
    if (items == null) return;
    final index = items.indexWhere((order) => order.id == event.orderId);
    if (index < 0) {
      silentRefresh();
      return;
    }
    final current = items[index];
    if (!shouldApplyOrderStatus(current.status, event.status)) return;
    final updated = [...items];
    updated[index] = current.copyWith(status: event.status);
    state = AsyncValue.data(updated);
    silentRefresh();
  }

  bool get hasMore => _page < _totalPages;

  Future<void> silentRefresh() async {
    try {
      final result = await ref.read(ordersRepositoryProvider).getMine(page: 1);
      _page = 1;
      _totalPages = result.totalPages;
      state = AsyncValue.data(result.items);
    } catch (_) {}
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      _page = 1;
      final result = await ref.read(ordersRepositoryProvider).getMine(page: _page);
      _totalPages = result.totalPages;
      return result.items;
    });
  }

  Future<void> loadMore() async {
    if (!hasMore || state.isLoading) return;
    final current = state.value ?? const [];
    final nextPage = _page + 1;
    final result = await ref.read(ordersRepositoryProvider).getMine(page: nextPage);
    _page = nextPage;
    _totalPages = result.totalPages;
    state = AsyncValue.data([...current, ...result.items]);
  }
}

final orderHistoryControllerProvider =
    AsyncNotifierProvider<OrderHistoryController, List<Order>>(
      OrderHistoryController.new,
    );
