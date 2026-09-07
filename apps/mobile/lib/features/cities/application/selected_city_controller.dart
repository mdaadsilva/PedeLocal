import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/preferences_service.dart';
import '../domain/city.dart';

/// Cidade atualmente selecionada pelo cliente, persistida localmente
/// ("Salvar localmente a última cidade escolhida quando apropriado").
/// Não se aplica a entregadores — a cidade do entregador é fixa, definida
/// no cadastro (`Driver.cityId`).
class SelectedCityController extends Notifier<SelectedCity?> {
  @override
  SelectedCity? build() {
    final prefs = ref.read(preferencesServiceProvider);
    final id = prefs.lastCityId;
    final name = prefs.lastCityName;
    if (id == null || name == null) return null;
    return SelectedCity(id: id, name: name);
  }

  Future<void> select(City city) async {
    await ref
        .read(preferencesServiceProvider)
        .saveLastCity(id: city.id, name: city.name);
    state = SelectedCity(id: city.id, name: city.name);
  }

  Future<void> clear() async {
    await ref.read(preferencesServiceProvider).clearLastCity();
    state = null;
  }
}

final selectedCityControllerProvider =
    NotifierProvider<SelectedCityController, SelectedCity?>(
      SelectedCityController.new,
    );
