import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/app_exception.dart';
import '../../cart/application/cart_controller.dart';
import '../../cart/domain/cart_state.dart';
import '../data/orders_repository.dart';
import '../domain/order.dart';
import '../domain/order_preview.dart';

const _uuid = Uuid();

List<CreateOrderItem> _itemsFromCart(CartState cart) {
  return [
    for (final item in cart.items)
      CreateOrderItem(
        productId: item.productId,
        quantity: item.quantity,
        optionIds: item.selectedOptions.map((o) => o.optionId).toList(),
      ),
  ];
}

/// Preview do checkout (`POST /api/orders/preview`).
class CheckoutPreviewController extends AsyncNotifier<OrderPreview?> {
  @override
  Future<OrderPreview?> build() async => null;

  void reset() {
    state = const AsyncValue.data(null);
  }

  Future<void> load({
    required OrderFulfillmentType orderType,
    String? addressId,
    required PaymentMethodOption paymentMethod,
    double? changeForAmount,
    String? notes,
  }) async {
    final cart = ref.read(cartControllerProvider);
    final establishmentId = cart.establishmentId;

    if (establishmentId == null || cart.items.isEmpty) {
      state = AsyncValue.error(
        const AppException(
          code: 'CART_EMPTY',
          message: 'Seu carrinho está vazio.',
        ),
        StackTrace.current,
      );
      return;
    }

    if (orderType == OrderFulfillmentType.delivery &&
        (addressId == null || addressId.isEmpty)) {
      state = const AsyncValue.data(null);
      return;
    }

    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() {
      return ref
          .read(ordersRepositoryProvider)
          .preview(
            establishmentId: establishmentId,
            addressId: orderType == OrderFulfillmentType.pickup
                ? null
                : addressId,
            paymentMethod: paymentMethod.apiValue,
            changeForAmount: changeForAmount,
            items: _itemsFromCart(cart),
            orderType: orderType.apiValue,
            notes: notes,
          );
    });
  }
}

final checkoutPreviewControllerProvider =
    AsyncNotifierProvider.autoDispose<CheckoutPreviewController, OrderPreview?>(
      CheckoutPreviewController.new,
    );

/// Finaliza a compra (`POST /api/orders`).
class CheckoutController extends AsyncNotifier<Order?> {
  String? _idempotencyKey;

  @override
  Future<Order?> build() async => null;

  String _ensureIdempotencyKey() => _idempotencyKey ??= _uuid.v4();

  void resetIdempotencyKey() => _idempotencyKey = null;

  Future<void> submit({
    required OrderFulfillmentType orderType,
    String? addressId,
    required PaymentMethodOption paymentMethod,
    double? changeForAmount,
    String? notes,
  }) async {
    final cart = ref.read(cartControllerProvider);
    final establishmentId = cart.establishmentId;

    if (establishmentId == null || cart.items.isEmpty) {
      state = AsyncValue.error(
        const AppException(
          code: 'CART_EMPTY',
          message: 'Seu carrinho está vazio.',
        ),
        StackTrace.current,
      );
      return;
    }

    if (orderType == OrderFulfillmentType.delivery &&
        (addressId == null || addressId.isEmpty)) {
      state = AsyncValue.error(
        const AppException(
          code: 'ADDRESS_REQUIRED',
          message: 'Selecione um endereço de entrega.',
        ),
        StackTrace.current,
      );
      return;
    }

    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      final order = await ref
          .read(ordersRepositoryProvider)
          .create(
            establishmentId: establishmentId,
            addressId: orderType == OrderFulfillmentType.pickup
                ? null
                : addressId,
            paymentMethod: paymentMethod.apiValue,
            changeForAmount: changeForAmount,
            items: _itemsFromCart(cart),
            idempotencyKey: _ensureIdempotencyKey(),
            orderType: orderType.apiValue,
            notes: notes,
          );

      await ref.read(cartControllerProvider.notifier).clear();
      _idempotencyKey = null;
      return order;
    });
  }
}

final checkoutControllerProvider =
    AsyncNotifierProvider.autoDispose<CheckoutController, Order?>(
      CheckoutController.new,
    );
