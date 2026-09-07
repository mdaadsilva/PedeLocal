import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/network/paged_result.dart';
import '../domain/delivery_code.dart';
import '../domain/order.dart';
import '../domain/order_preview.dart';

/// Item de pedido a ser criado — espelha `CreateOrderItemRequest`.
class CreateOrderItem {
  const CreateOrderItem({
    required this.productId,
    required this.quantity,
    required this.optionIds,
  });

  final String productId;
  final int quantity;
  final List<String> optionIds;

  Map<String, dynamic> toJson() => {
    'productId': productId,
    'quantity': quantity,
    'options': optionIds.map((id) => {'productOptionId': id}).toList(),
  };
}

/// `/api/orders` (CUSTOMER) — `docs/API_CONTRACT.md`, `docs/ORDER_FLOW.md`.
class OrdersRepository {
  OrdersRepository(this._dio);

  final Dio _dio;

  Map<String, dynamic> _payload({
    required String establishmentId,
    String? addressId,
    required String paymentMethod,
    double? changeForAmount,
    required List<CreateOrderItem> items,
    String? idempotencyKey,
    required String orderType,
    String? notes,
  }) {
    return {
      'establishmentId': establishmentId,
      'addressId': addressId,
      'paymentMethod': paymentMethod,
      'changeForAmount': changeForAmount,
      'items': items.map((i) => i.toJson()).toList(),
      'idempotencyKey': idempotencyKey,
      'orderType': orderType,
      'notes': notes,
    };
  }

  /// `POST /api/orders/preview` — mesma validação/cálculo de
  /// `POST /api/orders`, sem persistir (`docs/ORDER_FLOW.md#preview`).
  Future<OrderPreview> preview({
    required String establishmentId,
    String? addressId,
    required String paymentMethod,
    double? changeForAmount,
    required List<CreateOrderItem> items,
    required String orderType,
    String? notes,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/orders/preview',
        data: _payload(
          establishmentId: establishmentId,
          addressId: addressId,
          paymentMethod: paymentMethod,
          changeForAmount: changeForAmount,
          items: items,
          orderType: orderType,
          notes: notes,
        ),
      );
      return OrderPreview.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `POST /api/orders`. [idempotencyKey] deve ser o mesmo em caso de retry
  /// do mesmo pedido lógico (`docs/FRONTEND_GUIDE.md#idempotência`).
  ///
  /// `orderType = Pickup`: [addressId] deve ser `null`.
  /// `orderType = Delivery`: [addressId] é obrigatório.
  Future<Order> create({
    required String establishmentId,
    String? addressId,
    required String paymentMethod,
    double? changeForAmount,
    required List<CreateOrderItem> items,
    required String idempotencyKey,
    String orderType = 'Delivery',
    String? notes,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/orders',
        data: _payload(
          establishmentId: establishmentId,
          addressId: addressId,
          paymentMethod: paymentMethod,
          changeForAmount: changeForAmount,
          items: items,
          idempotencyKey: idempotencyKey,
          orderType: orderType,
          notes: notes,
        ),
      );
      return Order.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  Future<PagedResult<Order>> getMine({int page = 1, int pageSize = 20}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/orders',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return PagedResult.fromJson(
        response.data!,
        (item) => Order.fromJson(item),
      );
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  Future<Order> getById(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/orders/$id');
      return Order.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `GET /api/orders/{id}/delivery-code` — só o dono do pedido.
  /// O código fica só em memória (Riverpod); nunca logar o valor.
  Future<DeliveryCode> getDeliveryCode(String orderId) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/orders/$orderId/delivery-code',
      );
      return DeliveryCode.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final ordersRepositoryProvider = Provider<OrdersRepository>((ref) {
  return OrdersRepository(ref.watch(dioProvider));
});
