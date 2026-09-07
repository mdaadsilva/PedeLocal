import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';

void main() {
  group('AppException.fromResponse', () {
    test('usa code/message/traceId do formato padrão de erro da API', () {
      final response = Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(path: '/api/auth/login'),
        statusCode: 401,
        data: {
          'code': 'INVALID_CREDENTIALS',
          'message': 'E-mail ou senha incorretos.',
          'traceId': '0HN123',
          'errors': null,
        },
      );

      final exception = AppException.fromResponse(response);

      expect(exception.code, 'INVALID_CREDENTIALS');
      expect(exception.message, 'E-mail ou senha incorretos.');
      expect(exception.traceId, '0HN123');
      expect(exception.statusCode, 401);
      expect(exception.isAuthError, isTrue);
    });

    test('extrai fieldErrors de erros de validação (422)', () {
      final response = Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(path: '/api/auth/register/customer'),
        statusCode: 422,
        data: {
          'code': 'VALIDATION_ERROR',
          'message': 'Um ou mais campos são inválidos.',
          'traceId': '0HN456',
          'errors': {
            'password': ['A senha deve ter pelo menos 8 caracteres.'],
          },
        },
      );

      final exception = AppException.fromResponse(response);

      expect(exception.fieldErrors, isNotNull);
      expect(
        exception.fieldErrors!['password'],
        contains('A senha deve ter pelo menos 8 caracteres.'),
      );
    });

    test('gera mensagem amigável de fallback quando a resposta não segue o formato padrão', () {
      final response = Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(path: '/api/orders'),
        statusCode: 500,
        data: null,
      );

      final exception = AppException.fromResponse(response);

      expect(exception.code, AppException.codeUnknown);
      expect(exception.message, isNotEmpty);
    });
  });

  group('AppException.fromDioException', () {
    test('timeout gera mensagem amigável', () {
      final error = DioException(
        requestOptions: RequestOptions(path: '/api/cities'),
        type: DioExceptionType.connectionTimeout,
      );

      final exception = AppException.fromDioException(error);

      expect(exception.code, AppException.codeTimeout);
      expect(exception.message, contains('conexão'));
    });

    test('sem conexão gera mensagem amigável', () {
      final error = DioException(
        requestOptions: RequestOptions(path: '/api/cities'),
        type: DioExceptionType.connectionError,
      );

      final exception = AppException.fromDioException(error);

      expect(exception.code, AppException.codeNoConnection);
    });

    test('badResponse delega para fromResponse', () {
      final response = Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(path: '/api/auth/refresh'),
        statusCode: 401,
        data: {
          'code': 'INVALID_REFRESH_TOKEN',
          'message': 'Sessão expirada.',
          'traceId': '0HN789',
        },
      );
      final error = DioException(
        requestOptions: response.requestOptions,
        response: response,
        type: DioExceptionType.badResponse,
      );

      final exception = AppException.fromDioException(error);

      expect(exception.code, 'INVALID_REFRESH_TOKEN');
      expect(exception.isAuthError, isTrue);
    });
  });
}
