import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pedelocal_mobile/core/utils/formatters.dart';

void main() {
  setUpAll(() async {
    await initializeDateFormatting('pt_BR');
  });

  test('Formatters.currency formata em Real brasileiro', () {
    final formatted = Formatters.currency(29.9);
    expect(formatted, contains('29,90'));
    expect(formatted, contains(r'R$'));
  });

  test(
    'Formatters.toDisplayTime converte UTC para America/Sao_Paulo (-3h)',
    () {
      final utc = DateTime.utc(2026, 9, 7, 18, 30);
      final display = Formatters.toDisplayTime(utc);
      expect(display.hour, 15);
    },
  );
}
