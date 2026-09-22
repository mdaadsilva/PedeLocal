import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/widgets/status_badge.dart';

void main() {
  testWidgets('StatusBadge.driverStatus mapeia Available para "Disponível"', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: Center(child: _WrapBadge('Available'))),
      ),
    );

    expect(find.text('Disponível'), findsOneWidget);
  });

  testWidgets('StatusBadge.driverOnboardingStatus mapeia PendingDocuments', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: StatusBadge.driverOnboardingStatus('PendingDocuments'),
          ),
        ),
      ),
    );

    expect(find.text('Documentação pendente'), findsOneWidget);
  });

  testWidgets('StatusBadge.driverEligibilityStatus mapeia DocumentExpired', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: StatusBadge.driverEligibilityStatus('DocumentExpired'),
          ),
        ),
      ),
    );

    expect(find.text('CNH vencida'), findsOneWidget);
  });
}

class _WrapBadge extends StatelessWidget {
  const _WrapBadge(this.status);

  final String status;

  @override
  Widget build(BuildContext context) => StatusBadge.driverStatus(status);
}
