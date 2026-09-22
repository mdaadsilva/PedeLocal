import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../domain/driver_documents.dart';

/// Hub de onboarding — `GET /api/driver/me/documents`
/// (`docs/DRIVER_ONBOARDING.md`).
class DriverDocumentsController extends AsyncNotifier<DriverDocuments> {
  @override
  Future<DriverDocuments> build() {
    return ref.read(driverRepositoryProvider).getMyDocuments();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(driverRepositoryProvider).getMyDocuments(),
    );
  }
}

final driverDocumentsControllerProvider =
    AsyncNotifierProvider<DriverDocumentsController, DriverDocuments>(
      DriverDocumentsController.new,
    );
