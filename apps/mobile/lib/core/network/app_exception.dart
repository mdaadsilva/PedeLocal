import 'package:dio/dio.dart';

/// Exceção de aplicação unificada.
///
/// Toda chamada de rede deve, em caso de falha, lançar um [AppException] em
/// vez de propagar [DioException] cru — isso garante que a UI sempre tenha
/// uma [message] amigável em PT-BR para mostrar ao usuário, e um [code]
/// estável para lógica condicional (nunca comparar por [message]), exatamente
/// como descrito em `docs/ERROR_CODES.md`.
///
/// Quando o backend responde no formato padrão de erro
/// (`{ code, message, traceId, errors }`), [message] já vem pronta para
/// exibição direta — ver `docs/ERROR_CODES.md`. Para falhas técnicas (sem
/// resposta estruturada do backend: timeout, sem internet, 5xx sem corpo,
/// parsing), este arquivo é responsável por gerar uma mensagem amigável
/// equivalente.
class AppException implements Exception {
  const AppException({
    required this.code,
    required this.message,
    this.statusCode,
    this.traceId,
    this.fieldErrors,
  });

  /// Código estável (ex.: `INVALID_CREDENTIALS`, `ORDER_NOT_FOUND`) ou um dos
  /// pseudo-códigos client-side abaixo para falhas sem resposta do backend.
  final String code;

  /// Mensagem amigável em PT-BR, pronta para exibição direta ao usuário.
  final String message;

  final int? statusCode;
  final String? traceId;

  /// Presente apenas em erros de validação (422) — `{ campo: [mensagens] }`.
  final Map<String, List<String>>? fieldErrors;

  // Pseudo-códigos client-side (não existem no backend, usados apenas para
  // lógica local de tratamento de erro de rede/parsing).
  static const codeNoConnection = 'NO_CONNECTION';
  static const codeTimeout = 'TIMEOUT';
  static const codeCancelled = 'CANCELLED';
  static const codeUnknown = 'CLIENT_UNKNOWN_ERROR';

  bool get isAuthError =>
      code == 'INVALID_CREDENTIALS' ||
      code == 'INVALID_REFRESH_TOKEN' ||
      code == 'UNAUTHENTICATED';

  factory AppException.fromDioException(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return const AppException(
          code: codeTimeout,
          message: 'A conexão demorou demais para responder. Tente novamente.',
        );
      case DioExceptionType.connectionError:
        return const AppException(
          code: codeNoConnection,
          message: 'Não foi possível conectar. Verifique sua internet e tente novamente.',
        );
      case DioExceptionType.cancel:
        return const AppException(
          code: codeCancelled,
          message: 'Operação cancelada.',
        );
      case DioExceptionType.badResponse:
        return AppException.fromResponse(error.response);
      case DioExceptionType.badCertificate:
      case DioExceptionType.unknown:
      default:
        return AppException(
          code: codeUnknown,
          message: 'Ocorreu um erro inesperado. Tente novamente em instantes.',
          statusCode: error.response?.statusCode,
        );
    }
  }

  /// Constrói a exceção a partir do corpo de erro padrão da API
  /// (`{ code, message, traceId, errors }`) — ver `docs/ERROR_CODES.md`.
  factory AppException.fromResponse(Response<dynamic>? response) {
    final statusCode = response?.statusCode;
    final data = response?.data;

    if (data is Map<String, dynamic>) {
      final code = data['code'] as String?;
      final message = data['message'] as String?;
      final traceId = data['traceId'] as String?;
      final rawErrors = data['errors'];
      Map<String, List<String>>? fieldErrors;
      if (rawErrors is Map) {
        fieldErrors = rawErrors.map(
          (key, value) => MapEntry(
            key.toString(),
            (value as List<dynamic>? ?? const [])
                .map((e) => e.toString())
                .toList(),
          ),
        );
      }

      if (code != null && message != null) {
        return AppException(
          code: code,
          message: message,
          statusCode: statusCode,
          traceId: traceId,
          fieldErrors: fieldErrors,
        );
      }
    }

    return AppException(
      code: codeUnknown,
      message: _fallbackMessageForStatus(statusCode),
      statusCode: statusCode,
    );
  }

  static String _fallbackMessageForStatus(int? statusCode) {
    switch (statusCode) {
      case 401:
        return 'Sua sessão expirou. Faça login novamente.';
      case 403:
        return 'Você não tem permissão para realizar esta ação.';
      case 404:
        return 'Não encontramos o que você procurava.';
      case 429:
        return 'Muitas tentativas em pouco tempo. Aguarde um momento e tente novamente.';
      case 500:
      case 502:
      case 503:
        return 'Nossos servidores estão com um problema. Tente novamente em instantes.';
      default:
        return 'Ocorreu um erro inesperado. Tente novamente em instantes.';
    }
  }

  @override
  String toString() =>
      'AppException(code: $code, statusCode: $statusCode, message: $message)';
}
