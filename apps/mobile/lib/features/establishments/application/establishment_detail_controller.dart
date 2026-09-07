import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/establishments_repository.dart';
import 'establishment_detail_state.dart';

/// Detalhe de um estabelecimento + seu catálogo de produtos — um controller
/// por `establishmentId` (family via construtor, padrão Riverpod 3.x: o
/// argumento é injetado no construtor da notifier, não em `build()`).
class EstablishmentDetailController
    extends AsyncNotifier<EstablishmentDetailState> {
  EstablishmentDetailController(this.establishmentId);

  final String establishmentId;

  @override
  Future<EstablishmentDetailState> build() => _load();

  Future<EstablishmentDetailState> _load() async {
    final repository = ref.read(establishmentsRepositoryProvider);

    final establishment = await repository.getById(establishmentId);
    final products = await repository.getProducts(establishmentId);
    final categories = await repository.getCategories();

    String? categoryName;
    for (final category in categories) {
      if (category.id == establishment.categoryId) {
        categoryName = category.name;
        break;
      }
    }

    return EstablishmentDetailState(
      establishment: establishment,
      categoryName: categoryName,
      products: products,
    );
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_load);
  }
}

final establishmentDetailControllerProvider =
    AsyncNotifierProvider.family<
      EstablishmentDetailController,
      EstablishmentDetailState,
      String
    >(EstablishmentDetailController.new);
