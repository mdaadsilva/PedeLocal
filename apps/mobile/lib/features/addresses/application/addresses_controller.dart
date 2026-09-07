import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/addresses_repository.dart';
import '../domain/address.dart';

/// Lista de endereços do cliente autenticado
/// (`GET /api/me/addresses`) — usada tanto na tela de gerenciamento de
/// endereços quanto no seletor de endereço do checkout.
class AddressesController extends AsyncNotifier<List<Address>> {
  @override
  Future<List<Address>> build() {
    return ref.read(addressesRepositoryProvider).getAll();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(addressesRepositoryProvider).getAll(),
    );
  }

  Future<void> delete(String id) async {
    await ref.read(addressesRepositoryProvider).delete(id);
    await refresh();
  }
}

final addressesControllerProvider =
    AsyncNotifierProvider<AddressesController, List<Address>>(
      AddressesController.new,
    );
