import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/utils/validators.dart';

void main() {
  group('Validators.email', () {
    test('rejeita vazio', () {
      expect(Validators.email(''), isNotNull);
    });

    test('rejeita sem @', () {
      expect(Validators.email('maria.example.com'), isNotNull);
    });

    test('aceita e-mail válido', () {
      expect(Validators.email('maria@example.com'), isNull);
    });
  });

  group('Validators.password', () {
    // Espelha a regra do backend (docs/AUTH_FLOW.md): mínimo 8 caracteres,
    // 1 maiúscula, 1 minúscula, 1 número.
    test('rejeita senha curta', () {
      expect(Validators.password('Ab1'), isNotNull);
    });

    test('rejeita sem maiúscula', () {
      expect(Validators.password('senha123'), isNotNull);
    });

    test('rejeita sem minúscula', () {
      expect(Validators.password('SENHA123'), isNotNull);
    });

    test('rejeita sem número', () {
      expect(Validators.password('SenhaSegura'), isNotNull);
    });

    test('aceita senha válida', () {
      expect(Validators.password('Senha123'), isNull);
    });
  });

  group('Validators.confirmPassword', () {
    test('rejeita senhas diferentes', () {
      expect(Validators.confirmPassword('Senha123', 'Senha124'), isNotNull);
    });

    test('aceita senhas iguais', () {
      expect(Validators.confirmPassword('Senha123', 'Senha123'), isNull);
    });
  });

  group('Validators.cpf', () {
    test('rejeita CPF com menos de 11 dígitos', () {
      expect(Validators.cpf('123456789'), isNotNull);
    });

    test('aceita CPF com 11 dígitos (com máscara)', () {
      expect(Validators.cpf('123.456.789-01'), isNull);
    });
  });

  group('Validators.phone', () {
    test('rejeita telefone muito curto', () {
      expect(Validators.phone('123'), isNotNull);
    });

    test('aceita telefone com DDD (11 dígitos)', () {
      expect(Validators.phone('14999990000'), isNull);
    });
  });
}
