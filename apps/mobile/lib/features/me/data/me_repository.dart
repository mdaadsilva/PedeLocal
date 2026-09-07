import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../domain/my_profile.dart';

/// Chamadas de `/api/me/*` relevantes para esta etapa (perfil — usado pela
/// resolução de sessão/role). Endpoints de notificações, export LGPD e
/// exclusão de conta existem no contrato mas pertencem a telas futuras.
class MeRepository {
  MeRepository(this._dio);

  final Dio _dio;

  Future<MyProfile> getProfile() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/me/profile');
      return MyProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `DELETE /api/me/account` — anonimiza e desativa a conta
  /// (`docs/API_CONTRACT.md#minha-conta`). Irreversível: o chamador deve
  /// confirmar explicitamente com o usuário antes de invocar.
  Future<void> deleteAccount() async {
    try {
      await _dio.delete<void>('/api/me/account');
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final meRepositoryProvider = Provider<MeRepository>((ref) {
  return MeRepository(ref.watch(dioProvider));
});

final myProfileProvider = FutureProvider.autoDispose<MyProfile>((ref) {
  return ref.watch(meRepositoryProvider).getProfile();
});
