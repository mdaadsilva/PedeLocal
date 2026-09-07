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

  testWidgets('StatusBadge.driverApprovalStatus mapeia PendingApproval', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: StatusBadge.driverApprovalStatus('PendingApproval'),
          ),
        ),
      ),
    );

    expect(find.text('Aguardando aprovação'), findsOneWidget);
  });
}

class _WrapBadge extends StatelessWidget {
  const _WrapBadge(this.status);

  final String status;

  @override
  Widget build(BuildContext context) => StatusBadge.driverStatus(status);
}
