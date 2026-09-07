import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/widgets/empty_state_view.dart';
import 'package:pedelocal_mobile/core/widgets/error_state_view.dart';

void main() {
  testWidgets('EmptyStateView mostra título e mensagem', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: EmptyStateView(
            title: 'Nenhum resultado',
            message: 'Tente outra busca.',
          ),
        ),
      ),
    );

    expect(find.text('Nenhum resultado'), findsOneWidget);
    expect(find.text('Tente outra busca.'), findsOneWidget);
  });

  testWidgets('ErrorStateView aciona onRetry ao tocar no botão', (
    tester,
  ) async {
    var retried = false;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ErrorStateView(
            message: 'Falha ao carregar.',
            onRetry: () => retried = true,
          ),
        ),
      ),
    );

    await tester.tap(find.text('Tentar novamente'));
    await tester.pump();

    expect(retried, isTrue);
  });
}
