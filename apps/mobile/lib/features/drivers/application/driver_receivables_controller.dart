import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../domain/driver_receivable.dart';

/// Lista de recebíveis do entregador — usada como histórico de entregas
/// concluídas (`GET /api/driver/receivables`).
class DriverReceivablesController
    extends AsyncNotifier<List<DriverReceivable>> {
  @override
  Future<List<DriverReceivable>> build() {
    return ref.read(driverRepositoryProvider).getReceivables();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(driverRepositoryProvider).getReceivables(),
    );
  }
}

final driverReceivablesControllerProvider =
    AsyncNotifierProvider<DriverReceivablesController, List<DriverReceivable>>(
      DriverReceivablesController.new,
    );
