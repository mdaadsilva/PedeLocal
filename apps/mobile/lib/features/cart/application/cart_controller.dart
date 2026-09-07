import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/storage/preferences_service.dart';
import '../domain/cart_item.dart';
import '../domain/cart_state.dart';

const _uuid = Uuid();

/// Carrinho local, um estabelecimento por vez (`docs` — Etapa 2, seção 3).
///
/// Persistido em [PreferencesService] (não sensível) só para sobreviver a um
/// fechamento acidental do app antes do checkout — nunca é a fonte de
/// verdade de preços (isso é sempre o backend, no momento de
/// `POST /api/orders`).
class CartController extends Notifier<CartState> {
  @override
  CartState build() {
    final raw = ref.read(preferencesServiceProvider).readCart();
    if (raw == null) return CartState.empty;
    try {
      return CartState.fromJson(raw);
    } catch (_) {
      return CartState.empty;
    }
  }

  Future<void> _persist() async {
    await ref.read(preferencesServiceProvider).saveCart(state.toJson());
  }

  /// `true` se adicionar um item deste estabelecimento exigiria confirmar a
  /// troca (carrinho não vazio e pertencente a outro estabelecimento).
  bool wouldRequireEstablishmentSwitch(String establishmentId) {
    return state.establishmentId != null &&
        state.establishmentId != establishmentId &&
        state.items.isNotEmpty;
  }

  /// Limpa o carrinho atual e começa um novo, vinculado a
  /// [establishmentId]/[establishmentName].
  Future<void> startNewEstablishment({
    required String establishmentId,
    required String establishmentName,
  }) async {
    state = CartState(
      establishmentId: establishmentId,
      establishmentName: establishmentName,
    );
    await _persist();
  }

  /// Adiciona um item já validado (grupos obrigatórios respeitados na UI de
  /// seleção do produto). Se o carrinho pertencer a outro estabelecimento, o
  /// chamador deve confirmar a troca antes via
  /// [wouldRequireEstablishmentSwitch] + [startNewEstablishment].
  Future<void> addItem({
    required String establishmentId,
    required String establishmentName,
    required String productId,
    required String productName,
    required double unitPrice,
    required int quantity,
    required List<CartItemOption> selectedOptions,
  }) async {
    final isDifferentEstablishment =
        state.establishmentId != null &&
        state.establishmentId != establishmentId;
    final baseItems = isDifferentEstablishment
        ? <CartItem>[]
        : List<CartItem>.from(state.items);

    baseItems.add(
      CartItem(
        cartItemId: _uuid.v4(),
        productId: productId,
        productName: productName,
        unitPrice: unitPrice,
        quantity: quantity,
        selectedOptions: selectedOptions,
      ),
    );

    state = CartState(
      establishmentId: establishmentId,
      establishmentName: establishmentName,
      items: baseItems,
    );
    await _persist();
  }

  Future<void> updateQuantity(String cartItemId, int quantity) async {
    if (quantity <= 0) {
      await removeItem(cartItemId);
      return;
    }
    state = state.copyWith(
      items: [
        for (final item in state.items)
          if (item.cartItemId == cartItemId)
            item.copyWith(quantity: quantity)
          else
            item,
      ],
    );
    await _persist();
  }

  Future<void> removeItem(String cartItemId) async {
    final remaining = state.items
        .where((i) => i.cartItemId != cartItemId)
        .toList();
    state = remaining.isEmpty
        ? CartState.empty
        : state.copyWith(items: remaining);
    if (remaining.isEmpty) {
      await ref.read(preferencesServiceProvider).clearCart();
    } else {
      await _persist();
    }
  }

  Future<void> clear() async {
    state = CartState.empty;
    await ref.read(preferencesServiceProvider).clearCart();
  }
}

final cartControllerProvider = NotifierProvider<CartController, CartState>(
  CartController.new,
);
