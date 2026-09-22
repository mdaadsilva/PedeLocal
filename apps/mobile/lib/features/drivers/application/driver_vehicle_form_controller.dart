import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../domain/driver_documents.dart';
import 'driver_documents_controller.dart';

/// `PUT /api/driver/me/documents/vehicle` — submeter/atualizar o veículo
/// (`docs/DRIVER_ONBOARDING.md#veículo`). Trocar `type` depois de `Approved`
/// volta o entregador para `UnderReview`.
class DriverVehicleFormController extends AsyncNotifier<DriverDocuments?> {
  @override
  Future<DriverDocuments?> build() async => null;

  Future<void> submit({
    required String type,
    String? plate,
    required String model,
    required String color,
    String? documentImageUrl,
  }) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      final updated = await ref
          .read(driverRepositoryProvider)
          .submitVehicle(
            type: type,
            plate: plate,
            model: model,
            color: color,
            documentImageUrl: documentImageUrl,
          );
      ref.invalidate(driverDocumentsControllerProvider);
      return updated;
    });
  }
}

final driverVehicleFormControllerProvider =
    AsyncNotifierProvider.autoDispose<
      DriverVehicleFormController,
      DriverDocuments?
    >(DriverVehicleFormController.new);
