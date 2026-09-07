import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/driver_repository.dart';
import '../domain/delivery_detail.dart';

/// `GET /api/driver/deliveries/{id}/details` — só o entregador vinculado.
class DeliveryDetailsController extends AsyncNotifier<DeliveryDetail> {
  DeliveryDetailsController(this.deliveryId);

  final String deliveryId;

  @override
  Future<DeliveryDetail> build() {
    return ref.read(driverRepositoryProvider).getDeliveryDetails(deliveryId);
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(driverRepositoryProvider).getDeliveryDetails(deliveryId),
    );
  }
}

final deliveryDetailsControllerProvider =
    AsyncNotifierProvider.autoDispose
        .family<DeliveryDetailsController, DeliveryDetail, String>(
          DeliveryDetailsController.new,
        );
