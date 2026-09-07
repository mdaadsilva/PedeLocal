import 'package:equatable/equatable.dart';

import '../../products/domain/product.dart';
import '../domain/establishment.dart';

class EstablishmentDetailState extends Equatable {
  const EstablishmentDetailState({
    required this.establishment,
    required this.categoryName,
    required this.products,
  });

  final Establishment establishment;

  /// Resolvido via `GET /api/establishment-categories` combinando
  /// `establishment.categoryId` — `null` se a categoria não for encontrada
  /// na lista pública (não deveria acontecer em uso normal).
  final String? categoryName;
  final List<Product> products;

  bool get canReceiveNewOrders => establishment.canReceiveNewOrders;

  /// Agrupa o catálogo pela categoria de cardápio (`menuCategoryId`).
  /// O endpoint público não devolve o **nome** da categoria — não
  /// inventamos rótulos; a UI só usa o agrupamento visual.
  List<List<Product>> get productsGroupedByMenuCategory {
    final order = <String>[];
    final map = <String, List<Product>>{};
    for (final product in products) {
      if (!map.containsKey(product.menuCategoryId)) {
        order.add(product.menuCategoryId);
      }
      map.putIfAbsent(product.menuCategoryId, () => []).add(product);
    }
    return [for (final id in order) map[id]!];
  }

  @override
  List<Object?> get props => [establishment, categoryName, products];
}
