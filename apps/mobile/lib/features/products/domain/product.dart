import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Catalog.ProductResponse`
/// (`GET /api/establishments/{id}/products` — `docs/API_CONTRACT.md`).
///
/// Importante: a API não expõe nome de categoria de cardápio para o cliente
/// final (só `menuCategoryId`, sem endpoint público de nomes de
/// menu-categories) — não inventamos um nome aqui; ver pendências da
/// Etapa 2.
class Product extends Equatable {
  const Product({
    required this.id,
    required this.establishmentId,
    required this.menuCategoryId,
    required this.name,
    this.description,
    this.imageUrl,
    required this.price,
    this.promotionalPrice,
    required this.stockMode,
    this.stockQuantity,
    required this.isAvailable,
    required this.optionGroups,
  });

  final String id;
  final String establishmentId;
  final String menuCategoryId;
  final String name;
  final String? description;
  final String? imageUrl;
  final double price;
  final double? promotionalPrice;

  /// `Uncontrolled | Controlled`.
  final String stockMode;
  final int? stockQuantity;
  final bool isAvailable;
  final List<ProductOptionGroup> optionGroups;

  /// Preço vigente para exibição (promocional quando realmente existir e for
  /// menor que o preço cheio) — nunca invertido, nunca inventado.
  double get displayPrice => (hasPromotion ? promotionalPrice : null) ?? price;

  bool get hasPromotion =>
      promotionalPrice != null && promotionalPrice! < price;

  bool get isControlledStock => stockMode == 'Controlled';

  bool get isOutOfStock => isControlledStock && (stockQuantity ?? 0) <= 0;

  /// Indisponível para pedido — seja por flag explícita, seja por estoque
  /// zerado quando o estoque é controlado.
  bool get isPurchasable => isAvailable && !isOutOfStock;

  factory Product.fromJson(Map<String, dynamic> json) {
    return Product(
      id: json['id'] as String,
      establishmentId: json['establishmentId'] as String,
      menuCategoryId: json['menuCategoryId'] as String,
      name: json['name'] as String,
      description: json['description'] as String?,
      imageUrl: json['imageUrl'] as String?,
      price: (json['price'] as num).toDouble(),
      promotionalPrice: (json['promotionalPrice'] as num?)?.toDouble(),
      stockMode: json['stockMode'] as String,
      stockQuantity: json['stockQuantity'] as int?,
      isAvailable: json['isAvailable'] as bool,
      optionGroups: (json['optionGroups'] as List<dynamic>? ?? const [])
          .map((e) => ProductOptionGroup.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [
    id,
    name,
    description,
    imageUrl,
    price,
    promotionalPrice,
    stockMode,
    stockQuantity,
    isAvailable,
    optionGroups,
  ];
}

/// `PedeLocal.Contracts.Catalog.ProductOptionGroupResponse`.
class ProductOptionGroup extends Equatable {
  const ProductOptionGroup({
    required this.id,
    required this.name,
    required this.isRequired,
    required this.min,
    required this.max,
    required this.options,
  });

  final String id;
  final String name;
  final bool isRequired;

  /// Quantidade mínima de opções que devem ser selecionadas neste grupo
  /// (validado pelo backend em `POST /api/orders` —
  /// `INVALID_OPTION_SELECTION` se fora do intervalo `min`/`max`).
  final int min;
  final int max;
  final List<ProductOption> options;

  bool get allowsMultipleSelection => max > 1;

  factory ProductOptionGroup.fromJson(Map<String, dynamic> json) {
    return ProductOptionGroup(
      id: json['id'] as String,
      name: json['name'] as String,
      isRequired: json['required'] as bool,
      min: json['min'] as int,
      max: json['max'] as int,
      options: (json['options'] as List<dynamic>? ?? const [])
          .map((e) => ProductOption.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [id, name, isRequired, min, max, options];
}

/// `PedeLocal.Contracts.Catalog.ProductOptionResponse`.
class ProductOption extends Equatable {
  const ProductOption({
    required this.id,
    required this.name,
    required this.priceDelta,
    required this.isAvailable,
  });

  final String id;
  final String name;
  final double priceDelta;
  final bool isAvailable;

  factory ProductOption.fromJson(Map<String, dynamic> json) {
    return ProductOption(
      id: json['id'] as String,
      name: json['name'] as String,
      priceDelta: (json['priceDelta'] as num).toDouble(),
      isAvailable: json['isAvailable'] as bool,
    );
  }

  @override
  List<Object?> get props => [id, name, priceDelta, isAvailable];
}
