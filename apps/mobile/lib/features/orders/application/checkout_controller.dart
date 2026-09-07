import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/app_exception.dart';
import '../../cart/application/cart_controller.dart';
import '../data/orders_repository.dart';
import '../domain/order.dart';

const _uuid = Uuid();

/// Ação de finalizar a compra (`POST /api/orders`).
///
/// Pendência crítica documentada no relatório da Etapa 2: o backend não tem
/// nenhum endpoint de preview/cálculo de taxa de entrega antes da
/// confirmação — `deliveryFee`/`total` finais só existem na [Order] retornada
/// por esta chamada. O carrinho/checkout mostra apenas o subtotal (soma dos
/// itens) antes de enviar; o valor final com taxa é exibido no
/// acompanhamento do pedido, logo após a criação.
class CheckoutController extends AsyncNotifier<Order?> {
  String? _idempotencyKey;

  @override
  Future<Order?> build() async => null;

  String _ensureIdempotencyKey() => _idempotencyKey ??= _uuid.v4();

  /// Deve ser chamado se o usuário abandonar o checkout sem concluir, para
  /// que uma nova tentativa (carrinho diferente) gere uma chave nova.
  void resetIdempotencyKey() => _idempotencyKey = null;

  Future<void> submit({
    required String addressId,
    required PaymentMethodOption paymentMethod,
    double? changeForAmount,
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

    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      final order = await ref
          .read(ordersRepositoryProvider)
          .create(
            establishmentId: establishmentId,
            addressId: addressId,
            paymentMethod: paymentMethod.apiValue,
            changeForAmount: changeForAmount,
            items: [
              for (final item in cart.items)
                CreateOrderItem(
                  productId: item.productId,
                  quantity: item.quantity,
                  optionIds: item.selectedOptions
                      .map((o) => o.optionId)
                      .toList(),
                ),
            ],
            idempotencyKey: _ensureIdempotencyKey(),
          );

      // Só limpa o carrinho depois de sucesso confirmado pelo backend
      // (`docs` — "limpar carrinho somente após criação bem-sucedida").
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
