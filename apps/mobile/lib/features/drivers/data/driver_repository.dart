import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../domain/delivery.dart';
import '../domain/driver_profile.dart';
import '../domain/driver_receivable.dart';

/// `docs/API_CONTRACT.md#entregadores`, `docs/API_CONTRACT.md#entregas`,
/// `docs/DELIVERY_FLOW.md`.
///
/// IMPORTANTE: `DeliveryResponse` (contrato real do backend) não inclui
/// endereço do estabelecimento, endereço do cliente nem os itens do pedido —
/// apenas IDs e status. Por isso este repositório nunca inventa esses campos;
/// a tela do entregador mostra apenas o que a API de fato retorna.
class DriverRepository {
  DriverRepository(this._dio);

  final Dio _dio;

  Future<DriverProfile> getMyProfile() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/driver/me');
      return DriverProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `Offline → Available`. Falha (`409`) se `PendingApproval`/`Blocked`/`Busy`.
  Future<DriverProfile> goOnline() async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/me/status/online',
      );
      return DriverProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `Available → Offline`. Falha (`409`) se `Busy`.
  Future<DriverProfile> goOffline() async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/me/status/offline',
      );
      return DriverProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// Entregas `AVAILABLE` na cidade do entregador.
  Future<List<Delivery>> getAvailableDeliveries() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        '/api/driver/deliveries/available',
      );
      return response.data!
          .map((e) => Delivery.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// Entrega ativa do entregador, ou `null` (backend responde `204`).
  Future<Delivery?> getCurrentDelivery() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/driver/deliveries/current',
      );
      if (response.statusCode == 204 || response.data == null) return null;
      return Delivery.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `AVAILABLE → ASSIGNED`. Pode lançar `AppException` com
  /// `code == 'DELIVERY_ALREADY_TAKEN'` (409) — outro entregador venceu a
  /// corrida de aceite — ou `DRIVER_ALREADY_ON_DELIVERY` se já estiver
  /// `BUSY`. Ambos são fluxo esperado, tratados na camada de apresentação.
  Future<Delivery> acceptDelivery(String deliveryId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/deliveries/$deliveryId/accept',
      );
      return Delivery.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `ASSIGNED → PICKED_UP`.
  Future<Delivery> markPickedUp(String deliveryId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/deliveries/$deliveryId/pickup',
      );
      return Delivery.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `PICKED_UP → OUT_FOR_DELIVERY`.
  Future<Delivery> markOutForDelivery(String deliveryId) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/deliveries/$deliveryId/out-for-delivery',
      );
      return Delivery.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `OUT_FOR_DELIVERY → COMPLETED`. O código de 6 dígitos é informado
  /// verbalmente pelo cliente (`docs/DELIVERY_FLOW.md#código-de-
  /// confirmação`) — pode falhar com `INVALID_DELIVERY_CODE` (400),
  /// `DELIVERY_CODE_LOCKED` ou `DELIVERY_CODE_EXPIRED` (409).
  Future<Delivery> confirmDelivery(String deliveryId, String code) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/driver/deliveries/$deliveryId/confirm',
        data: {'code': code},
      );
      return Delivery.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// Meus recebíveis (um por entrega concluída) — usado como "histórico de
  /// entregas" do entregador na ausência de um endpoint dedicado
  /// (`docs/DELIVERY_FLOW.md#financeiro-do-entregador`).
  Future<List<DriverReceivable>> getReceivables() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/driver/receivables');
      return response.data!
          .map((e) => DriverReceivable.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final driverRepositoryProvider = Provider<DriverRepository>((ref) {
  return DriverRepository(ref.watch(dioProvider));
});
