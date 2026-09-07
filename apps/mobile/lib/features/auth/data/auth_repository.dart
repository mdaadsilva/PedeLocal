import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/storage/secure_storage_service.dart';
import '../domain/auth_models.dart';

/// Chamadas de `/api/auth/*` (`docs/API_CONTRACT.md`, `docs/AUTH_FLOW.md`).
/// Único ponto que lê/grava tokens diretamente após login/registro — todo o
/// resto do app lê o estado de sessão via `sessionControllerProvider`.
class AuthRepository {
  AuthRepository(this._dio, this._secureStorage);

  final Dio _dio;
  final SecureStorageService _secureStorage;

  Future<RegisterCustomerResult> registerCustomer({
    required String name,
    required String email,
    required String phone,
    required String password,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/auth/register/customer',
        data: {
          'name': name,
          'email': email,
          'phone': phone,
          'password': password,
        },
      );
      return RegisterCustomerResult.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  Future<RegisterDriverResult> registerDriver({
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
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/auth/register/driver',
        data: {
          'name': name,
          'cpf': cpf,
          'phone': phone,
          'email': email,
          'password': password,
          'cityId': cityId,
          'vehicleType': vehicleType.apiValue,
          'pixKeyType': pixKeyType.apiValue,
          'pixKey': pixKey,
        },
      );
      return RegisterDriverResult.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// Faz login e já persiste os tokens no secure storage — nunca retorna a
  /// senha nem a loga em nenhum ponto.
  Future<AuthTokens> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/auth/login',
        data: {'email': email, 'password': password},
      );
      final tokens = AuthTokens.fromJson(response.data!);
      await _secureStorage.saveTokens(
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        accessTokenExpiresAtUtc: tokens.accessTokenExpiresAtUtc,
      );
      return tokens;
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// Revoga a sessão atual no backend (idempotente — `docs/AUTH_FLOW.md`) e
  /// limpa os tokens locais independentemente do resultado da chamada.
  Future<void> logout() async {
    final refreshToken = await _secureStorage.readRefreshToken();
    try {
      if (refreshToken != null && refreshToken.isNotEmpty) {
        await _dio.post<void>(
          '/api/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      }
    } on DioException catch (_) {
      // Logout local sempre acontece, mesmo que a chamada de rede falhe.
    } finally {
      await _secureStorage.clearTokens();
    }
  }

  /// Sempre retorna sucesso (204), exista ou não o e-mail — evita
  /// enumeração de contas (`docs/AUTH_FLOW.md`).
  Future<void> forgotPassword(String email) async {
    try {
      await _dio.post<void>(
        '/api/auth/forgot-password',
        data: {'email': email},
      );
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepository(
    ref.watch(dioProvider),
    ref.watch(secureStorageServiceProvider),
  );
});
