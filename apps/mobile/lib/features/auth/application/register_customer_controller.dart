import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';
import '../domain/auth_models.dart';

/// Controller de ação do cadastro de cliente
/// (`POST /api/auth/register/customer`). Não faz login automático — o
/// backend não retorna tokens neste endpoint (apenas confirmação do
/// cadastro), então a tela leva o usuário ao Login em caso de sucesso.
class RegisterCustomerController
    extends AsyncNotifier<RegisterCustomerResult?> {
  @override
  Future<RegisterCustomerResult?> build() async => null;

  Future<void> register({
    required String name,
    required String email,
    required String phone,
    required String password,
  }) async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() {
      return ref
          .read(authRepositoryProvider)
          .registerCustomer(
            name: name,
            email: email,
            phone: phone,
            password: password,
          );
    });
  }
}

final registerCustomerControllerProvider =
    AsyncNotifierProvider.autoDispose<
      RegisterCustomerController,
      RegisterCustomerResult?
    >(RegisterCustomerController.new);
