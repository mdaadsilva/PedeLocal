import 'package:flutter/material.dart';

import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_text_field.dart';
import '../../domain/delivery_occurrence.dart';

/// Formulário de ocorrência operacional, aberto como bottom sheet a partir
/// da entrega atual (`docs/DELIVERY_FLOW.md#ocorrências-na-entrega`).
/// Retorna `(type, note)` via [Navigator.pop], ou `null` se cancelado.
Future<(String, String?)?> showOccurrenceFormSheet(BuildContext context) {
  return showModalBottomSheet<(String, String?)>(
    context: context,
    isScrollControlled: true,
    builder: (context) => const _OccurrenceFormSheet(),
  );
}

class _OccurrenceFormSheet extends StatefulWidget {
  const _OccurrenceFormSheet();

  @override
  State<_OccurrenceFormSheet> createState() => _OccurrenceFormSheetState();
}

class _OccurrenceFormSheetState extends State<_OccurrenceFormSheet> {
  String _type = occurrenceTypeLabels.keys.first;
  final _noteController = TextEditingController();

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(
        left: AppSpacing.lg,
        right: AppSpacing.lg,
        top: AppSpacing.lg,
        bottom: MediaQuery.of(context).viewInsets.bottom + AppSpacing.lg,
      ),
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Registrar ocorrência', style: AppTypography.title),
            const SizedBox(height: AppSpacing.xs),
            const Text(
              'Relate um problema durante esta entrega. Isso não altera o '
              'andamento do pedido.',
              style: AppTypography.bodyMuted,
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Tipo de ocorrência', style: AppTypography.label),
            const SizedBox(height: AppSpacing.xs),
            DropdownButtonFormField<String>(
              initialValue: _type,
              items: occurrenceTypeLabels.entries
                  .map(
                    (entry) => DropdownMenuItem(
                      value: entry.key,
                      child: Text(entry.value),
                    ),
                  )
                  .toList(),
              onChanged: (value) => setState(() => _type = value ?? _type),
            ),
            const SizedBox(height: AppSpacing.md),
            PedeLocalTextField(
              controller: _noteController,
              label: 'Detalhes (opcional)',
              hint: 'Ex.: toquei a campainha várias vezes...',
              maxLines: 3,
            ),
            const SizedBox(height: AppSpacing.lg),
            PedeLocalButton(
              label: 'Registrar ocorrência',
              onPressed: () {
                final note = _noteController.text.trim();
                Navigator.of(
                  context,
                ).pop((_type, note.isEmpty ? null : note));
              },
            ),
            const SizedBox(height: AppSpacing.xs),
            PedeLocalButton(
              label: 'Cancelar',
              variant: PedeLocalButtonVariant.text,
              onPressed: () => Navigator.of(context).pop(),
            ),
          ],
        ),
      ),
    );
  }
}
