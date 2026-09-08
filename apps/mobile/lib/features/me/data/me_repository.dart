import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../domain/my_profile.dart';

/// Chamadas de `/api/me/*` (perfil, device tokens de push, exclusão).
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

  /// `PUT /api/me/device-tokens` — registra/atualiza o token FCM do
  /// dispositivo autenticado. Idempotente. O `userId` vem do JWT — nunca
  /// enviado no corpo (`docs/PUSH_NOTIFICATIONS.md`).
  Future<void> registerDeviceToken({
    required String token,
    required String platform,
  }) async {
    try {
      await _dio.put<void>(
        '/api/me/device-tokens',
        data: {'token': token, 'platform': platform},
      );
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `DELETE /api/me/device-tokens` — remove o token deste aparelho
  /// (logout). Idempotente se o token não existir para o usuário.
  Future<void> removeDeviceToken(String token) async {
    try {
      await _dio.delete<void>(
        '/api/me/device-tokens',
        data: {'token': token},
      );
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
