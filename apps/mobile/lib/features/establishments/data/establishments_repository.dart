import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/network/paged_result.dart';
import '../../products/domain/product.dart';
import '../domain/establishment.dart';
import '../domain/establishment_category.dart';

/// `docs/API_CONTRACT.md#estabelecimentos`.
class EstablishmentsRepository {
  EstablishmentsRepository(this._dio);

  final Dio _dio;

  /// `GET /api/establishments?cityId=&page=&pageSize=` (CUSTOMER — busca
  /// filtrada, só estabelecimentos visíveis e não suspensos).
  Future<PagedResult<Establishment>> searchByCity({
    required String cityId,
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/establishments',
        queryParameters: {'cityId': cityId, 'page': page, 'pageSize': pageSize},
      );
      return PagedResult.fromJson(
        response.data!,
        (item) => Establishment.fromJson(item),
      );
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `GET /api/establishment-categories` (público).
  Future<List<EstablishmentCategory>> getCategories() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        '/api/establishment-categories',
      );
      return response.data!
          .map((e) => EstablishmentCategory.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `GET /api/establishments/{id}` (CUSTOMER).
  Future<Establishment> getById(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/api/establishments/$id',
      );
      return Establishment.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `GET /api/establishments/{id}/products` (CUSTOMER — catálogo público,
  /// só produtos disponíveis). Lista plana: o backend não expõe nomes de
  /// categoria de cardápio nesta rota (ver pendências da Etapa 2).
  Future<List<Product>> getProducts(String establishmentId) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        '/api/establishments/$establishmentId/products',
      );
      return response.data!
          .map((e) => Product.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final establishmentsRepositoryProvider = Provider<EstablishmentsRepository>((
  ref,
) {
  return EstablishmentsRepository(ref.watch(dioProvider));
});
