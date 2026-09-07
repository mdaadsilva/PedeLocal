import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/cities_repository.dart';
import '../domain/city.dart';

/// Lista de cidades ativas disponível para seleção. `AsyncNotifier` cuida
/// naturalmente dos estados loading/error/success exigidos pela tela.
class CitiesController extends AsyncNotifier<List<City>> {
  @override
  Future<List<City>> build() {
    return ref.read(citiesRepositoryProvider).getActiveCities();
  }

  Future<void> reload() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(citiesRepositoryProvider).getActiveCities(),
    );
  }
}

final citiesControllerProvider =
    AsyncNotifierProvider<CitiesController, List<City>>(CitiesController.new);
