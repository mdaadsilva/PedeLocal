import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/notifications/firebase_bootstrap.dart';
import 'core/storage/preferences_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Necessário para os `DateFormat` em pt_BR (`core/utils/formatters.dart`).
  await initializeDateFormatting('pt_BR');

  // Sem google-services.json / GoogleService-Info.plist o init falha
  // de forma controlada — REST e SignalR continuam funcionando.
  await bootstrapFirebasePush();

  final sharedPreferences = await SharedPreferences.getInstance();

  runApp(
    ProviderScope(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(sharedPreferences),
      ],
      child: const PedeLocalApp(),
    ),
  );
}
