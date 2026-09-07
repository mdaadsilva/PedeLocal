import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/app_exception.dart';
import '../domain/address.dart';

/// `/api/me/addresses` (autenticado) — `docs/API_CONTRACT.md#endereços`.
class AddressesRepository {
  AddressesRepository(this._dio);

  final Dio _dio;

  Future<List<Address>> getAll() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/me/addresses');
      return response.data!
          .map((e) => Address.fromJson(e as Map<String, dynamic>))
          .toList(growable: false);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  Future<Address> create({
    required String cityId,
    required String type,
    String? label,
    String? street,
    String? number,
    String? district,
    String? complement,
    String? reference,
    String? zipCode,
    String? propertyName,
    String? road,
    String? kilometer,
    required double latitude,
    required double longitude,
    String? googlePlaceId,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/me/addresses',
        data: {
          'cityId': cityId,
          'type': type,
          'label': label,
          'street': street,
          'number': number,
          'district': district,
          'complement': complement,
          'reference': reference,
          'zipCode': zipCode,
          'propertyName': propertyName,
          'road': road,
          'kilometer': kilometer,
          'latitude': latitude,
          'longitude': longitude,
          'googlePlaceId': googlePlaceId,
        },
      );
      return Address.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  /// `PUT /api/me/addresses/{id}` — sem `cityId` (a cidade do endereço não
  /// pode ser alterada, conforme contrato).
  Future<Address> update({
    required String id,
    required String type,
    String? label,
    String? street,
    String? number,
    String? district,
    String? complement,
    String? reference,
    String? zipCode,
    String? propertyName,
    String? road,
    String? kilometer,
    required double latitude,
    required double longitude,
    String? googlePlaceId,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        '/api/me/addresses/$id',
        data: {
          'type': type,
          'label': label,
          'street': street,
          'number': number,
          'district': district,
          'complement': complement,
          'reference': reference,
          'zipCode': zipCode,
          'propertyName': propertyName,
          'road': road,
          'kilometer': kilometer,
          'latitude': latitude,
          'longitude': longitude,
          'googlePlaceId': googlePlaceId,
        },
      );
      return Address.fromJson(response.data!);
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }

  Future<void> delete(String id) async {
    try {
      await _dio.delete<void>('/api/me/addresses/$id');
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final addressesRepositoryProvider = Provider<AddressesRepository>((ref) {
  return AddressesRepository(ref.watch(dioProvider));
});
