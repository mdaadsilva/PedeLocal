/// Validações client-side para feedback imediato no formulário.
///
/// Importante: isto é só UX (evitar round-trip óbvio ao servidor). O backend
/// é sempre a fonte de verdade — todo campo é revalidado lá
/// (`docs/AUTH_FLOW.md`: senha mín. 8 caracteres, 1 maiúscula, 1 minúscula,
/// 1 número) e qualquer erro retornado pela API (422 `VALIDATION_ERROR`)
/// deve ser exibido normalmente, mesmo que a validação local tenha passado.
abstract class Validators {
  Validators._();

  static final _emailRegex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
  static final _hasUpper = RegExp(r'[A-Z]');
  static final _hasLower = RegExp(r'[a-z]');
  static final _hasDigit = RegExp(r'[0-9]');

  static String? required(String? value, {String field = 'Este campo'}) {
    if (value == null || value.trim().isEmpty) {
      return '$field é obrigatório.';
    }
    return null;
  }

  static String? email(String? value) {
    final requiredError = required(value, field: 'O e-mail');
    if (requiredError != null) return requiredError;
    if (!_emailRegex.hasMatch(value!.trim())) {
      return 'Informe um e-mail válido.';
    }
    return null;
  }

  /// Espelha a regra do backend: mínimo 8 caracteres, 1 maiúscula, 1
  /// minúscula, 1 número (`docs/AUTH_FLOW.md`).
  static String? password(String? value) {
    final requiredError = required(value, field: 'A senha');
    if (requiredError != null) return requiredError;
    final v = value!;
    if (v.length < 8) return 'A senha deve ter pelo menos 8 caracteres.';
    if (!_hasUpper.hasMatch(v)) {
      return 'A senha deve ter ao menos uma letra maiúscula.';
    }
    if (!_hasLower.hasMatch(v)) {
      return 'A senha deve ter ao menos uma letra minúscula.';
    }
    if (!_hasDigit.hasMatch(v)) return 'A senha deve ter ao menos um número.';
    return null;
  }

  static String? confirmPassword(String? value, String original) {
    final requiredError = required(value, field: 'A confirmação de senha');
    if (requiredError != null) return requiredError;
    if (value != original) return 'As senhas não coincidem.';
    return null;
  }

  static String? phone(String? value) {
    final requiredError = required(value, field: 'O telefone');
    if (requiredError != null) return requiredError;
    final digits = value!.replaceAll(RegExp(r'\D'), '');
    if (digits.length < 10 || digits.length > 11) {
      return 'Informe um telefone válido com DDD.';
    }
    return null;
  }

  static String? cpf(String? value) {
    final requiredError = required(value, field: 'O CPF');
    if (requiredError != null) return requiredError;
    final digits = value!.replaceAll(RegExp(r'\D'), '');
    if (digits.length != 11) {
      return 'Informe um CPF válido com 11 dígitos.';
    }
    return null;
  }

  static String? name(String? value) {
    final requiredError = required(value, field: 'O nome');
    if (requiredError != null) return requiredError;
    if (value!.trim().length < 2) return 'Informe um nome válido.';
    return null;
  }
}
