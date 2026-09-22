import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_client.dart';
import '../network/app_exception.dart';

/// `POST /api/uploads/image` (multipart/form-data, campo `file`) —
/// infraestrutura de upload genérica usada por qualquer imagem de
/// documento/foto do app (`docs/FRONTEND_GUIDE.md#upload-de-imagens`).
/// Aceita apenas JPG/PNG/WEBP, até 5MB, validado por conteúdo real do
/// arquivo pelo backend.
class UploadsRepository {
  UploadsRepository(this._dio);

  final Dio _dio;

  /// Retorna a `url` relativa (`/uploads/<guid>.jpg`) — use diretamente no
  /// campo `documentImageUrl` do envio de CNH/veículo.
  Future<String> uploadImage(String filePath) async {
    try {
      final fileName = filePath.split(RegExp(r'[\\/]')).last;
      final response = await _dio.post<Map<String, dynamic>>(
        '/api/uploads/image',
        data: FormData.fromMap({
          'file': await MultipartFile.fromFile(filePath, filename: fileName),
        }),
      );
      return response.data!['url'] as String;
    } on DioException catch (e) {
      throw AppException.fromDioException(e);
    }
  }
}

final uploadsRepositoryProvider = Provider<UploadsRepository>((ref) {
  return UploadsRepository(ref.watch(dioProvider));
});
