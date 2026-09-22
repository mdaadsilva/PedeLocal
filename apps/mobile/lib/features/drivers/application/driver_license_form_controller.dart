import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../domain/driver_documents.dart';
import 'driver_documents_controller.dart';

/// `PUT /api/driver/me/documents/license` — submeter/atualizar a CNH
/// (`docs/DRIVER_ONBOARDING.md#cnh`). Pode falhar com `422
/// VALIDATION_ERROR` se a CNH já estiver vencida.
class DriverLicenseFormController extends AsyncNotifier<DriverDocuments?> {
  @override
  Future<DriverDocuments?> build() async => null;

  Future<void> submit({
    required String registrationNumber,
    required String category,
    required DateTime expiresAt,
    bool? hasEar,
    String? documentImageUrl,
  }) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() async {
      final updated = await ref
          .read(driverRepositoryProvider)
          .submitLicense(
            registrationNumber: registrationNumber,
            category: category,
            expiresAt: expiresAt,
            hasEar: hasEar,
            documentImageUrl: documentImageUrl,
          );
      ref.invalidate(driverDocumentsControllerProvider);
      return updated;
    });
  }
}

final driverLicenseFormControllerProvider =
    AsyncNotifierProvider.autoDispose<
      DriverLicenseFormController,
      DriverDocuments?
    >(DriverLicenseFormController.new);
