import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../domain/city.dart';

/// `GET /api/cities` — público, retorna somente cidades ativas
/// (`docs/API_CONTRACT.md`). Nunca hardcodar a lista de cidades no cliente.
class CitiesRepository {
  CitiesRepository(this._dio);

  final Dio _dio;

  Future<List<City>> getActiveCities() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/cities');
      return response.data!
          .map((e) => City.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final citiesRepositoryProvider = Provider<CitiesRepository>((ref) {
  return CitiesRepository(ref.watch(dioProvider));
});
