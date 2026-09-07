import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/widgets/pedelocal_button.dart';

void main() {
  testWidgets('chama onPressed ao tocar', (tester) async {
    var tapCount = 0;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: PedeLocalButton(label: 'Entrar', onPressed: () => tapCount++),
        ),
      ),
    );

    await tester.tap(find.text('Entrar'));
    await tester.pump();

    expect(tapCount, 1);
  });

  testWidgets(
    'fica desabilitado e mostra spinner quando isLoading é true (previne duplo clique)',
    (tester) async {
      var tapCount = 0;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PedeLocalButton(
              label: 'Entrar',
              isLoading: true,
              onPressed: () => tapCount++,
            ),
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Entrar'), findsNothing);

      await tester.tap(find.byType(ElevatedButton), warnIfMissed: false);
      await tester.pump();

      expect(tapCount, 0);
    },
  );

  testWidgets('fica desabilitado quando onPressed é nulo', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: PedeLocalButton(label: 'Entrar', onPressed: null)),
      ),
    );

    final button = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
    expect(button.onPressed, isNull);
  });
}
