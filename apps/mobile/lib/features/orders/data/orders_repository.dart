import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/network/paged_result.dart';
import '../domain/order.dart';

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

  /// `POST /api/orders`. [idempotencyKey] deve ser o mesmo em caso de retry
  /// do mesmo pedido lógico (protege contra duplo-submit em conexão
  /// instável — `docs/FRONTEND_GUIDE.md#idempotência`).
  Future<Order> create({
    required String establishmentId,
    required String addressId,
    required String paymentMethod,
    double? changeForAmount,
    required List<CreateOrderItem> items,
    required String idempotencyKey,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/orders',
        data: {
          'establishmentId': establishmentId,
          'addressId': addressId,
          'paymentMethod': paymentMethod,
          'changeForAmount': changeForAmount,
          'items': items.map((i) => i.toJson()).toList(),
          'idempotencyKey': idempotencyKey,
        },
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
}

final ordersRepositoryProvider = Provider<OrdersRepository>((ref) {
  return OrdersRepository(ref.watch(dioProvider));
});
