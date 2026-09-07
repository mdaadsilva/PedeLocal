import 'package:equatable/equatable.dart';

import '../../products/domain/product.dart';

/// Uma opção selecionada dentro de um item do carrinho — espelha o que será
/// enviado como `CreateOrderItemOptionSelection` (`{ productOptionId }`) e
/// guarda nome/preço só para exibição local (o backend recalcula tudo e
/// congela seu próprio snapshot ao criar o pedido).
class CartItemOption extends Equatable {
  const CartItemOption({
    required this.optionId,
    required this.name,
    required this.priceDelta,
  });

  final String optionId;
  final String name;
  final double priceDelta;

  @override
  List<Object?> get props => [optionId, name, priceDelta];
}

/// Um item do carrinho local (nunca enviado como está — apenas usado para
/// montar `CreateOrderItemRequest` no checkout). Preços aqui são somente
/// visuais/UX; o backend é a fonte final de verdade
/// (`docs/ORDER_FLOW.md#criar-pedido-cliente`).
class CartItem extends Equatable {
  const CartItem({
    required this.cartItemId,
    required this.productId,
    required this.productName,
    required this.unitPrice,
    required this.quantity,
    required this.selectedOptions,
  });

  /// Identificador local (não é o `productId`) — permite ter duas linhas do
  /// mesmo produto com combinações diferentes de adicionais no carrinho.
  final String cartItemId;
  final String productId;
  final String productName;
  final double unitPrice;
  final int quantity;
  final List<CartItemOption> selectedOptions;

  double get optionsTotal =>
      selectedOptions.fold(0, (sum, o) => sum + o.priceDelta);

  double get lineUnitPrice => unitPrice + optionsTotal;

  double get lineTotal => lineUnitPrice * quantity;

  CartItem copyWith({int? quantity}) {
    return CartItem(
      cartItemId: cartItemId,
      productId: productId,
      productName: productName,
      unitPrice: unitPrice,
      quantity: quantity ?? this.quantity,
      selectedOptions: selectedOptions,
    );
  }

  Map<String, dynamic> toJson() => {
    'cartItemId': cartItemId,
    'productId': productId,
    'productName': productName,
    'unitPrice': unitPrice,
    'quantity': quantity,
    'selectedOptions': selectedOptions
        .map(
          (o) => {
            'optionId': o.optionId,
            'name': o.name,
            'priceDelta': o.priceDelta,
          },
        )
        .toList(),
  };

  factory CartItem.fromJson(Map<String, dynamic> json) {
    return CartItem(
      cartItemId: json['cartItemId'] as String,
      productId: json['productId'] as String,
      productName: json['productName'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      quantity: json['quantity'] as int,
      selectedOptions: (json['selectedOptions'] as List<dynamic>? ?? const [])
          .map(
            (o) => CartItemOption(
              optionId: o['optionId'] as String,
              name: o['name'] as String,
              priceDelta: (o['priceDelta'] as num).toDouble(),
            ),
          )
          .toList(),
    );
  }

  @override
  List<Object?> get props => [
    cartItemId,
    productId,
    unitPrice,
    quantity,
    selectedOptions,
  ];
}

/// Espelha um adicional escolhido para o `Product.fromJson` original — usado
/// apenas para montar [CartItemOption] a partir de uma seleção na tela de
/// produto.
extension ProductOptionToCartMapping on ProductOption {
  CartItemOption toCartOption() =>
      CartItemOption(optionId: id, name: name, priceDelta: priceDelta);
}
