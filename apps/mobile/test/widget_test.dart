import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/app.dart';
import 'package:pedelocal_mobile/core/notifications/push_messaging_gateway.dart';
import 'package:pedelocal_mobile/core/notifications/push_notification_service.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/core/storage/secure_storage_service.dart';
import 'package:pedelocal_mobile/core/widgets/brand_wordmark.dart';
import 'package:shared_preferences/shared_preferences.dart';

class MockSecureStorageService extends Mock implements SecureStorageService {}

void main() {
  testWidgets(
    'App sobe na Splash e, sem sessão salva, é redirecionado para o Login',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();

      final secureStorage = MockSecureStorageService();
      when(() => secureStorage.hasSession()).thenAnswer((_) async => false);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sharedPreferencesProvider.overrideWithValue(prefs),
            secureStorageServiceProvider.overrideWithValue(secureStorage),
            pushMessagingGatewayProvider.overrideWithValue(
              const UnavailablePushMessagingGateway(),
            ),
          ],
          child: const PedeLocalApp(),
        ),
      );

      // Primeiro frame: Splash (sessão ainda resolvendo).
      expect(find.byType(BrandWordmark), findsWidgets);

      await tester.pumpAndSettle();

      // Sem sessão salva -> redireciona para o Login.
      expect(find.text('Entrar'), findsWidgets);
      expect(find.text('Da sua cidade, pra você'), findsOneWidget);
    },
  );
}
