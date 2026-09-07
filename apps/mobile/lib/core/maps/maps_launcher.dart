import 'package:url_launcher/url_launcher.dart';

/// Abre o Google Maps **externo** (sem navegação turn-by-turn própria).
///
/// Usado no MVP para conferir um pin e para "Abrir rota" quando há
/// coordenadas (`docs` — Etapa 2, seção 12). Não depende de chave de API.
abstract class MapsLauncher {
  MapsLauncher._();

  static Uri searchUri(double latitude, double longitude) => Uri.parse(
    'https://www.google.com/maps/search/?api=1&query=$latitude,$longitude',
  );

  static Uri directionsUri(double latitude, double longitude) => Uri.parse(
    'https://www.google.com/maps/dir/?api=1&destination=$latitude,$longitude',
  );

  static Future<bool> openPin(double latitude, double longitude) {
    return launchUrl(
      searchUri(latitude, longitude),
      mode: LaunchMode.externalApplication,
    );
  }

  static Future<bool> openRoute(double latitude, double longitude) {
    return launchUrl(
      directionsUri(latitude, longitude),
      mode: LaunchMode.externalApplication,
    );
  }
}
