import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/orders_repository.dart';
import '../domain/delivery_code.dart';

/// `GET /api/orders/{id}/delivery-code` — só em memória, nunca em disco.
class DeliveryCodeController extends AsyncNotifier<DeliveryCode> {
  DeliveryCodeController(this.orderId);

  final String orderId;

  @override
  Future<DeliveryCode> build() {
    return ref.read(ordersRepositoryProvider).getDeliveryCode(orderId);
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(
      () => ref.read(ordersRepositoryProvider).getDeliveryCode(orderId),
    );
  }
}

final deliveryCodeControllerProvider =
    AsyncNotifierProvider.autoDispose
        .family<DeliveryCodeController, DeliveryCode, String>(
          DeliveryCodeController.new,
        );
