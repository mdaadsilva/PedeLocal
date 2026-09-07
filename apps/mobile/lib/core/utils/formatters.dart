import 'package:intl/intl.dart';

/// Formatadores centralizados — nenhuma tela deve formatar moeda/data "na
/// mão". Valores monetários chegam como `number` (JSON) / `decimal`
/// (backend) — nunca strings (`docs/FRONTEND_GUIDE.md#dinheiro`); aqui só
/// formatamos para exibição, sem somar nada em `double`.
abstract class Formatters {
  Formatters._();

  static final NumberFormat _currency = NumberFormat.currency(
    locale: 'pt_BR',
    symbol: 'R\$',
  );

  static String currency(num value) => _currency.format(value);

  /// Converte um instante UTC (formato ISO 8601 retornado pela API — ver
  /// `docs/FRONTEND_GUIDE.md#datas`) para `America/Sao_Paulo` apenas para
  /// exibição. Sem pacote de timezone, usamos o offset fixo de -3h (BRT),
  /// que cobre o horário oficial brasileiro atual (sem horário de verão
  /// desde 2019).
  static DateTime toDisplayTime(DateTime utc) =>
      utc.toUtc().subtract(const Duration(hours: 3));

  static final DateFormat _dateTime = DateFormat('dd/MM/yyyy HH:mm', 'pt_BR');
  static final DateFormat _date = DateFormat('dd/MM/yyyy', 'pt_BR');
  static final DateFormat _time = DateFormat('HH:mm', 'pt_BR');

  static String dateTime(DateTime utc) => _dateTime.format(toDisplayTime(utc));

  static String date(DateTime utc) => _date.format(toDisplayTime(utc));

  static String time(DateTime utc) => _time.format(toDisplayTime(utc));
}
