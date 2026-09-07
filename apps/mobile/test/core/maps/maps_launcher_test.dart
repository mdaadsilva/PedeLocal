import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/maps/maps_launcher.dart';

void main() {
  test('searchUri aponta para o pin no Google Maps', () {
    expect(
      MapsLauncher.searchUri(-21.85, -49.9).toString(),
      'https://www.google.com/maps/search/?api=1&query=-21.85,-49.9',
    );
  });

  test('directionsUri abre rota até o destino', () {
    expect(
      MapsLauncher.directionsUri(-21.85, -49.9).toString(),
      'https://www.google.com/maps/dir/?api=1&destination=-21.85,-49.9',
    );
  });
}
