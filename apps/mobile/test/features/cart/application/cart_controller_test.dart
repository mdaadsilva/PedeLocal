import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/features/cart/application/cart_controller.dart';
import 'package:pedelocal_mobile/features/cart/domain/cart_item.dart';
import 'package:pedelocal_mobile/features/cart/domain/cart_state.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Carrinho local (`docs` — Etapa 2, seção 3): um estabelecimento por vez,
/// adicionar/alterar quantidade/remover/limpar, e troca de estabelecimento.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  Future<ProviderContainer> buildContainer() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final container = ProviderContainer(
      overrides: [sharedPreferencesProvider.overrideWithValue(prefs)],
    );
    addTearDown(container.dispose);
    return container;
  }

  test('adicionar item calcula subtotal visual com adicionais', () async {
    final container = await buildContainer();
    final notifier = container.read(cartControllerProvider.notifier);

    await notifier.addItem(
      establishmentId: 'est-1',
      establishmentName: 'Padaria do João',
      productId: 'prod-1',
      productName: 'X-Burguer',
      unitPrice: 20,
      quantity: 2,
      selectedOptions: const [
        CartItemOption(optionId: 'opt-1', name: 'Bacon', priceDelta: 4),
      ],
    );

    final state = container.read(cartControllerProvider);
    expect(state.establishmentId, 'est-1');
    expect(state.items, hasLength(1));
    expect(state.totalQuantity, 2);
    // (20 + 4) * 2 = 48.
    expect(state.subtotal, 48);
  });

  test('alterar quantidade recalcula o subtotal', () async {
    final container = await buildContainer();
    final notifier = container.read(cartControllerProvider.notifier);
    await notifier.addItem(
      establishmentId: 'est-1',
      establishmentName: 'Padaria do João',
      productId: 'prod-1',
      productName: 'X-Burguer',
      unitPrice: 10,
      quantity: 1,
      selectedOptions: const [],
    );
    final cartItemId = container
        .read(cartControllerProvider)
        .items
        .first
        .cartItemId;

    await notifier.updateQuantity(cartItemId, 3);

    expect(container.read(cartControllerProvider).subtotal, 30);
    expect(container.read(cartControllerProvider).totalQuantity, 3);
  });

  test(
    'remover o único item esvazia o carrinho (sem estabelecimento travado)',
    () async {
      final container = await buildContainer();
      final notifier = container.read(cartControllerProvider.notifier);
      await notifier.addItem(
        establishmentId: 'est-1',
        establishmentName: 'Padaria do João',
        productId: 'prod-1',
        productName: 'X-Burguer',
        unitPrice: 10,
        quantity: 1,
        selectedOptions: const [],
      );
      final cartItemId = container
          .read(cartControllerProvider)
          .items
          .first
          .cartItemId;

      await notifier.removeItem(cartItemId);

      final state = container.read(cartControllerProvider);
      expect(state.isEmpty, isTrue);
      expect(state.establishmentId, isNull);
    },
  );

  test('limpar carrinho remove todos os itens', () async {
    final container = await buildContainer();
    final notifier = container.read(cartControllerProvider.notifier);
    await notifier.addItem(
      establishmentId: 'est-1',
      establishmentName: 'Padaria do João',
      productId: 'prod-1',
      productName: 'X-Burguer',
      unitPrice: 10,
      quantity: 1,
      selectedOptions: const [],
    );

    await notifier.clear();

    expect(container.read(cartControllerProvider), CartState.empty);
  });

  group('troca de estabelecimento', () {
    test('wouldRequireEstablishmentSwitch é true ao ter itens de outro estabelecimento', () async {
      final container = await buildContainer();
      final notifier = container.read(cartControllerProvider.notifier);
      await notifier.addItem(
        establishmentId: 'est-1',
        establishmentName: 'Padaria do João',
        productId: 'prod-1',
        productName: 'X-Burguer',
        unitPrice: 10,
        quantity: 1,
        selectedOptions: const [],
      );

      expect(notifier.wouldRequireEstablishmentSwitch('est-1'), isFalse);
      expect(notifier.wouldRequireEstablishmentSwitch('est-2'), isTrue);
    });

    test('addItem de outro estabelecimento substitui o carrinho anterior por completo', () async {
      final container = await buildContainer();
      final notifier = container.read(cartControllerProvider.notifier);
      await notifier.addItem(
        establishmentId: 'est-1',
        establishmentName: 'Padaria do João',
        productId: 'prod-1',
        productName: 'X-Burguer',
        unitPrice: 10,
        quantity: 1,
        selectedOptions: const [],
      );

      // Simula a confirmação da UI para trocar de loja: adiciona item de
      // um estabelecimento diferente — o carrinho anterior é descartado.
      await notifier.addItem(
        establishmentId: 'est-2',
        establishmentName: 'Mercadinho da Ana',
        productId: 'prod-9',
        productName: 'Refrigerante',
        unitPrice: 6,
        quantity: 1,
        selectedOptions: const [],
      );

      final state = container.read(cartControllerProvider);
      expect(state.establishmentId, 'est-2');
      expect(state.items, hasLength(1));
      expect(state.items.first.productId, 'prod-9');
    });
  });
}
