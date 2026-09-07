import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';
import '../domain/auth_models.dart';

/// Controller de ação do cadastro de entregador
/// (`POST /api/auth/register/driver`). O entregador nasce sempre
/// `PendingApproval` (`docs/AUTH_FLOW.md`) — não faz login automático; a
/// tela leva ao Login (de onde, após autenticar, a Home do Entregador já
/// mostra o estado de aguardando aprovação).
class RegisterDriverController extends AsyncNotifier<RegisterDriverResult?> {
  @override
  Future<RegisterDriverResult?> build() async => null;

  Future<void> register({
    required String name,
    required String cpf,
    required String phone,
    required String email,
    required String password,
    required String cityId,
    required VehicleType vehicleType,
    required PixKeyType pixKeyType,
    required String pixKey,
  }) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() {
      return ref
          .read(authRepositoryProvider)
          .registerDriver(
            name: name,
            cpf: cpf,
            phone: phone,
            email: email,
            password: password,
            cityId: cityId,
            vehicleType: vehicleType,
            pixKeyType: pixKeyType,
            pixKey: pixKey,
          );
    });
  }
}

final registerDriverControllerProvider =
    AsyncNotifierProvider.autoDispose<
      RegisterDriverController,
      RegisterDriverResult?
    >(RegisterDriverController.new);
