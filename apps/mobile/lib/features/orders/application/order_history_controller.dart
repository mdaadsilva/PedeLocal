import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/orders_repository.dart';
import '../domain/order.dart';

/// Histórico de pedidos do cliente (`GET /api/orders?page=` —
/// `docs/API_CONTRACT.md#pedidos`). Carrega a primeira página; "carregar
/// mais" busca páginas subsequentes e concatena.
class OrderHistoryController extends AsyncNotifier<List<Order>> {
  int _page = 1;
  int _totalPages = 1;

  @override
  Future<List<Order>> build() async {
    _page = 1;
    final result = await ref
        .read(ordersRepositoryProvider)
        .getMine(page: _page);
    _totalPages = result.totalPages;
    return result.items;
  }

  bool get hasMore => _page < _totalPages;

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      _page = 1;
      final result = await ref
          .read(ordersRepositoryProvider)
          .getMine(page: _page);
      _totalPages = result.totalPages;
      return result.items;
    });
  }

  Future<void> loadMore() async {
    if (!hasMore || state.isLoading) return;
    final current = state.value ?? const [];
    final nextPage = _page + 1;
    final result = await ref
        .read(ordersRepositoryProvider)
        .getMine(page: nextPage);
    _page = nextPage;
    _totalPages = result.totalPages;
    state = AsyncValue.data([...current, ...result.items]);
  }
}

final orderHistoryControllerProvider =
    AsyncNotifierProvider<OrderHistoryController, List<Order>>(
      OrderHistoryController.new,
    );
