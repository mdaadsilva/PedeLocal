import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../cities/application/selected_city_controller.dart';
import '../../establishments/data/establishments_repository.dart';
import 'home_customer_state.dart';

/// Carrega, em paralelo, os estabelecimentos da cidade selecionada e as
/// categorias públicas de estabelecimento (para os chips de categoria da
/// Home Cliente).
class HomeCustomerController extends AsyncNotifier<HomeCustomerState> {
  @override
  Future<HomeCustomerState> build() {
    return _load();
  }

  Future<HomeCustomerState> _load() async {
    final selectedCity = ref.read(selectedCityControllerProvider);
    if (selectedCity == null) {
      return const HomeCustomerState(establishments: [], categories: []);
    }

    final repository = ref.read(establishmentsRepositoryProvider);
    final establishmentsFuture = repository.searchByCity(
      cityId: selectedCity.id,
    );
    final categoriesFuture = repository.getCategories();

    final establishmentsPage = await establishmentsFuture;
    final categories = await categoriesFuture;

    return HomeCustomerState(
      establishments: establishmentsPage.items,
      categories: categories,
      totalCount: establishmentsPage.totalCount,
    );
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_load);
  }
}

final homeCustomerControllerProvider =
    AsyncNotifierProvider<HomeCustomerController, HomeCustomerState>(
      HomeCustomerController.new,
    );
