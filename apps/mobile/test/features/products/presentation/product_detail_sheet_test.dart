import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/features/cart/application/cart_controller.dart';
import 'package:pedelocal_mobile/features/products/domain/product.dart';
import 'package:pedelocal_mobile/features/products/presentation/product_detail_sheet.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Seleção de adicionais/variações (docs - Etapa 2, seção 2): grupo
/// obrigatório bloqueia a adição ao carrinho até que o mínimo de opções
/// seja selecionado.
void main() {
  Product buildProductWithRequiredGroup() {
    return const Product(
      id: 'prod-1',
      establishmentId: 'est-1',
      menuCategoryId: 'cat-1',
      name: 'X-Burguer',
      description: 'Pao, carne e queijo',
      price: 20,
      stockMode: 'Uncontrolled',
      isAvailable: true,
      optionGroups: [
        ProductOptionGroup(
          id: 'group-1',
          name: 'Ponto da carne',
          isRequired: true,
          min: 1,
          max: 1,
          options: [
            ProductOption(
              id: 'opt-mal',
              name: 'Mal passada',
              priceDelta: 0,
              isAvailable: true,
            ),
            ProductOption(
              id: 'opt-bem',
              name: 'Bem passada',
              priceDelta: 0,
              isAvailable: true,
            ),
          ],
        ),
      ],
    );
  }

  const requiredGroupButtonLabel = 'Selecione as op\u00e7\u00f5es';

  Future<ProviderContainer> pumpProductSheet(
    WidgetTester tester,
    Product product,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final container = ProviderContainer(
      overrides: [sharedPreferencesProvider.overrideWithValue(prefs)],
    );
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (context) => ElevatedButton(
                onPressed: () => showProductDetailSheet(
                  context,
                  product: product,
                  establishmentId: 'est-1',
                  establishmentName: 'Padaria do Joao',
                ),
                child: const Text('Abrir produto'),
              ),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Abrir produto'));
    await tester.pumpAndSettle();
    return container;
  }

  testWidgets(
    'grupo obrigatorio bloqueia o botao de adicionar até selecionar uma opção',
    (tester) async {
      await pumpProductSheet(tester, buildProductWithRequiredGroup());

      final addButtonBefore = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, requiredGroupButtonLabel),
      );
      expect(addButtonBefore.onPressed, isNull);

      await tester.tap(find.text('Mal passada'));
      await tester.pumpAndSettle();

      expect(find.text(requiredGroupButtonLabel), findsNothing);
      final addButtonAfter = tester.widget<ElevatedButton>(
        find.byType(ElevatedButton).last,
      );
      expect(addButtonAfter.onPressed, isNotNull);
    },
  );

  testWidgets(
    'adicionar ao carrinho apos satisfazer o grupo obrigatorio envia a opcao selecionada',
    (tester) async {
      final container = await pumpProductSheet(
        tester,
        buildProductWithRequiredGroup(),
      );

      await tester.tap(find.text('Bem passada'));
      await tester.pumpAndSettle();
      await tester.tap(find.byType(ElevatedButton).last);
      await tester.pumpAndSettle();

      final cart = container.read(cartControllerProvider);
      expect(cart.items, hasLength(1));
      expect(cart.items.first.selectedOptions.single.optionId, 'opt-bem');
    },
  );

  testWidgets('produto sem grupos obrigatorios permite adicionar direto', (
    tester,
  ) async {
    const product = Product(
      id: 'prod-2',
      establishmentId: 'est-1',
      menuCategoryId: 'cat-1',
      name: 'Refrigerante',
      price: 6,
      stockMode: 'Uncontrolled',
      isAvailable: true,
      optionGroups: [],
    );
    final container = await pumpProductSheet(tester, product);

    // Sem grupo obrigatorio, o unico botao de adicionar (o segundo
    // ElevatedButton, apos o "Abrir produto" da tela de tras) já vem
    // habilitado.
    final addButton = tester.widget<ElevatedButton>(
      find.byType(ElevatedButton).last,
    );
    expect(addButton.onPressed, isNotNull);
    expect(find.text(requiredGroupButtonLabel), findsNothing);

    await tester.tap(find.byType(ElevatedButton).last);
    await tester.pumpAndSettle();

    expect(container.read(cartControllerProvider).items, hasLength(1));
  });

  testWidgets(
    'produto indisponivel nunca permite adicionar, mesmo sem grupos obrigatorios',
    (tester) async {
      const product = Product(
        id: 'prod-3',
        establishmentId: 'est-1',
        menuCategoryId: 'cat-1',
        name: 'Suco',
        price: 8,
        stockMode: 'Uncontrolled',
        isAvailable: false,
        optionGroups: [],
      );
      await pumpProductSheet(tester, product);

      final addButton = tester.widget<ElevatedButton>(
        find.byType(ElevatedButton).last,
      );
      expect(addButton.onPressed, isNull);
    },
  );

  testWidgets('estoque controlado limita a quantidade máxima', (tester) async {
    const product = Product(
      id: 'prod-4',
      establishmentId: 'est-1',
      menuCategoryId: 'cat-1',
      name: 'Bolo',
      price: 30,
      stockMode: 'Controlled',
      stockQuantity: 2,
      isAvailable: true,
      optionGroups: [],
    );
    await pumpProductSheet(tester, product);

    expect(find.text('Estoque: 2'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.add_rounded));
    await tester.pumpAndSettle();
    expect(find.text('2'), findsWidgets);

    await tester.tap(find.byIcon(Icons.add_rounded));
    await tester.pumpAndSettle();
    expect(find.text('3'), findsNothing);
  });
}
