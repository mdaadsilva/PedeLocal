import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/session/domain/user_role.dart';

void main() {
  group('UserRoleResolution.fromApiRoles', () {
    test('retorna driver quando a lista contém DRIVER', () {
      expect(UserRoleResolution.fromApiRoles(['DRIVER']), UserRole.driver);
    });

    test('retorna customer quando a lista contém CUSTOMER', () {
      expect(UserRoleResolution.fromApiRoles(['CUSTOMER']), UserRole.customer);
    });

    test('prioriza driver quando o usuário tem os dois papéis', () {
      expect(
        UserRoleResolution.fromApiRoles(['CUSTOMER', 'DRIVER']),
        UserRole.driver,
      );
    });

    test('retorna unsupported para papéis de staff/admin', () {
      expect(
        UserRoleResolution.fromApiRoles(['SUPER_ADMIN']),
        UserRole.unsupported,
      );
      expect(
        UserRoleResolution.fromApiRoles(['ESTABLISHMENT_ADMIN']),
        UserRole.unsupported,
      );
    });

    test('retorna unsupported para lista vazia', () {
      expect(UserRoleResolution.fromApiRoles([]), UserRole.unsupported);
    });
  });
}
