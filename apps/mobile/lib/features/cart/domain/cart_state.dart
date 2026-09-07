import 'package:equatable/equatable.dart';

import 'cart_item.dart';

/// Estado do carrinho — pertence a **um único estabelecimento por vez**
/// (`docs` — "O carrinho deve pertencer a somente um estabelecimento por
/// vez"). Ao adicionar item de outro estabelecimento, a UI deve confirmar a
/// troca antes de chamar [CartController.startNewEstablishment].
class CartState extends Equatable {
  const CartState({
    this.establishmentId,
    this.establishmentName,
    this.items = const [],
  });

  final String? establishmentId;
  final String? establishmentName;
  final List<CartItem> items;

  bool get isEmpty => items.isEmpty;

  int get totalQuantity => items.fold(0, (sum, i) => sum + i.quantity);

  double get subtotal => items.fold(0, (sum, i) => sum + i.lineTotal);

  Map<String, dynamic> toJson() => {
    'establishmentId': establishmentId,
    'establishmentName': establishmentName,
    'items': items.map((i) => i.toJson()).toList(),
  };

  factory CartState.fromJson(Map<String, dynamic> json) {
    return CartState(
      establishmentId: json['establishmentId'] as String?,
      establishmentName: json['establishmentName'] as String?,
      items: (json['items'] as List<dynamic>? ?? const [])
          .map((e) => CartItem.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  CartState copyWith({
    String? establishmentId,
    String? establishmentName,
    List<CartItem>? items,
  }) {
    return CartState(
      establishmentId: establishmentId ?? this.establishmentId,
      establishmentName: establishmentName ?? this.establishmentName,
      items: items ?? this.items,
    );
  }

  static const empty = CartState();

  @override
  List<Object?> get props => [establishmentId, establishmentName, items];
}
