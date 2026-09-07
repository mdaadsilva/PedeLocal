import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_radius.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../cart/application/cart_controller.dart';
import '../../cart/domain/cart_item.dart';
import '../domain/product.dart';

/// Abre a folha de detalhe/seleção de um produto, com adição ao carrinho.
///
/// Se o carrinho já tiver itens de **outro** estabelecimento, confirma a
/// troca (limpando o carrinho atual) antes de adicionar — conforme
/// `docs` (Etapa 2, seção 3).
Future<void> showProductDetailSheet(
  BuildContext context, {
  required Product product,
  required String establishmentId,
  required String establishmentName,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (context) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      minChildSize: 0.4,
      maxChildSize: 0.95,
      builder: (context, scrollController) => _ProductDetailContent(
        product: product,
        establishmentId: establishmentId,
        establishmentName: establishmentName,
        scrollController: scrollController,
      ),
    ),
  );
}

class _ProductDetailContent extends ConsumerStatefulWidget {
  const _ProductDetailContent({
    required this.product,
    required this.establishmentId,
    required this.establishmentName,
    required this.scrollController,
  });

  final Product product;
  final String establishmentId;
  final String establishmentName;
  final ScrollController scrollController;

  @override
  ConsumerState<_ProductDetailContent> createState() =>
      _ProductDetailContentState();
}

class _ProductDetailContentState extends ConsumerState<_ProductDetailContent> {
  int _quantity = 1;

  /// `groupId -> Set<optionId selecionados>`.
  final Map<String, Set<String>> _selections = {};

  @override
  void initState() {
    super.initState();
    for (final group in widget.product.optionGroups) {
      _selections[group.id] = <String>{};
    }
  }

  bool _isGroupValid(ProductOptionGroup group) {
    final count = _selections[group.id]?.length ?? 0;
    return count >= group.min && count <= group.max;
  }

  bool get _allGroupsValid => widget.product.optionGroups.every(_isGroupValid);

  double get _optionsTotal {
    double total = 0;
    for (final group in widget.product.optionGroups) {
      final selectedIds = _selections[group.id] ?? const <String>{};
      for (final option in group.options) {
        if (selectedIds.contains(option.id)) total += option.priceDelta;
      }
    }
    return total;
  }

  double get _unitTotal => widget.product.displayPrice + _optionsTotal;

  void _toggleOption(ProductOptionGroup group, String optionId) {
    setState(() {
      final selected = _selections[group.id] ??= <String>{};
      final isSingleChoice = group.max <= 1;
      if (selected.contains(optionId)) {
        selected.remove(optionId);
      } else {
        if (isSingleChoice) selected.clear();
        if (selected.length < group.max || isSingleChoice) {
          selected.add(optionId);
        }
      }
    });
  }

  Future<void> _addToCart() async {
    final cart = ref.read(cartControllerProvider.notifier);
    final wouldSwitch = cart.wouldRequireEstablishmentSwitch(
      widget.establishmentId,
    );

    if (wouldSwitch) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Trocar de estabelecimento?'),
          content: Text(
            'Seu carrinho tem itens de "${ref.read(cartControllerProvider).establishmentName}". '
            'Ao adicionar este item, o carrinho atual será esvaziado.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Cancelar'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Trocar e continuar'),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    final selectedOptions = <CartItemOption>[
      for (final group in widget.product.optionGroups)
        for (final option in group.options)
          if ((_selections[group.id] ?? const <String>{}).contains(option.id))
            option.toCartOption(),
    ];

    await cart.addItem(
      establishmentId: widget.establishmentId,
      establishmentName: widget.establishmentName,
      productId: widget.product.id,
      productName: widget.product.name,
      unitPrice: widget.product.displayPrice,
      quantity: _quantity,
      selectedOptions: selectedOptions,
    );

    if (!mounted) return;
    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('${widget.product.name} adicionado ao carrinho.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    final product = widget.product;
    final canAdd = product.isPurchasable && _allGroupsValid;

    return SafeArea(
      child: Column(
        children: [
          Expanded(
            child: ListView(
              controller: widget.scrollController,
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    margin: const EdgeInsets.only(bottom: AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.divider,
                      borderRadius: AppRadius.radiusPill,
                    ),
                  ),
                ),
                if (product.imageUrl != null) ...[
                  ClipRRect(
                    borderRadius: AppRadius.radiusLg,
                    child: Image.network(
                      product.imageUrl!,
                      height: 160,
                      width: double.infinity,
                      fit: BoxFit.cover,
                      errorBuilder: (context, error, stackTrace) =>
                          const SizedBox.shrink(),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                ],
                Text(product.name, style: AppTypography.headline),
                const SizedBox(height: AppSpacing.xs),
                if (product.description != null &&
                    product.description!.isNotEmpty) ...[
                  Text(product.description!, style: AppTypography.bodyMuted),
                  const SizedBox(height: AppSpacing.sm),
                ],
                Row(
                  children: [
                    if (product.hasPromotion) ...[
                      Text(
                        Formatters.currency(product.price),
                        style: AppTypography.bodyMuted.copyWith(
                          decoration: TextDecoration.lineThrough,
                        ),
                      ),
                      const SizedBox(width: AppSpacing.xs),
                    ],
                    Text(
                      Formatters.currency(product.displayPrice),
                      style: AppTypography.title.copyWith(
                        color: AppColors.primary,
                      ),
                    ),
                  ],
                ),
                if (!product.isAvailable) ...[
                  const SizedBox(height: AppSpacing.sm),
                  _WarningBanner(text: 'Produto indisponível no momento.'),
                ] else if (product.isOutOfStock) ...[
                  const SizedBox(height: AppSpacing.sm),
                  _WarningBanner(text: 'Sem estoque no momento.'),
                ] else if (product.isControlledStock &&
                    product.stockQuantity != null) ...[
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    'Estoque: ${product.stockQuantity}',
                    style: AppTypography.caption,
                  ),
                ],
                const SizedBox(height: AppSpacing.lg),
                for (final group in product.optionGroups) ...[
                  _OptionGroupSection(
                    group: group,
                    selected: _selections[group.id] ?? const <String>{},
                    onToggle: (optionId) => _toggleOption(group, optionId),
                    isValid: _isGroupValid(group),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                ],
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.md,
              AppSpacing.lg,
              AppSpacing.md,
            ),
            decoration: BoxDecoration(
              color: AppColors.surface,
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.06),
                  blurRadius: 12,
                  offset: const Offset(0, -4),
                ),
              ],
            ),
            child: Row(
              children: [
                _QuantityStepper(
                  quantity: _quantity,
                  maxQuantity: product.isControlledStock
                      ? (product.stockQuantity ?? 1)
                      : 99,
                  onChanged: (q) => setState(() => _quantity = q),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: PedeLocalButton(
                    label: canAdd
                        ? 'Adicionar • ${Formatters.currency(_unitTotal * _quantity)}'
                        : 'Selecione as opções',
                    onPressed: canAdd ? _addToCart : null,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _WarningBanner extends StatelessWidget {
  const _WarningBanner({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        color: AppColors.accentLight,
        borderRadius: AppRadius.radiusMd,
      ),
      child: Row(
        children: [
          const Icon(
            Icons.info_outline_rounded,
            size: 18,
            color: AppColors.accent,
          ),
          const SizedBox(width: AppSpacing.xs),
          Expanded(child: Text(text, style: AppTypography.caption)),
        ],
      ),
    );
  }
}

class _OptionGroupSection extends StatelessWidget {
  const _OptionGroupSection({
    required this.group,
    required this.selected,
    required this.onToggle,
    required this.isValid,
  });

  final ProductOptionGroup group;
  final Set<String> selected;
  final void Function(String optionId) onToggle;
  final bool isValid;

  String get _hint {
    if (group.min == group.max) return 'Escolha ${group.min}';
    if (group.min == 0) return 'Escolha até ${group.max}';
    return 'Escolha de ${group.min} a ${group.max}';
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(group.name, style: AppTypography.subtitle)),
            Text(
              _hint,
              style: AppTypography.caption.copyWith(
                color: isValid ? AppColors.textSecondary : AppColors.error,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.xs),
        for (final option in group.options)
          _OptionRow(
            option: option,
            isSelected: selected.contains(option.id),
            isSingleChoice: group.max <= 1,
            onTap: option.isAvailable ? () => onToggle(option.id) : null,
          ),
      ],
    );
  }
}

class _OptionRow extends StatelessWidget {
  const _OptionRow({
    required this.option,
    required this.isSelected,
    required this.isSingleChoice,
    required this.onTap,
  });

  final ProductOption option;
  final bool isSelected;
  final bool isSingleChoice;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: option.isAvailable ? 1 : 0.5,
      child: InkWell(
        onTap: onTap,
        borderRadius: AppRadius.radiusSm,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
          child: Row(
            children: [
              Icon(
                isSelected
                    ? (isSingleChoice
                          ? Icons.radio_button_checked
                          : Icons.check_box_rounded)
                    : (isSingleChoice
                          ? Icons.radio_button_off
                          : Icons.check_box_outline_blank_rounded),
                color: isSelected ? AppColors.primary : AppColors.textDisabled,
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  option.isAvailable
                      ? option.name
                      : '${option.name} (indisponível)',
                  style: AppTypography.body,
                ),
              ),
              if (option.priceDelta != 0)
                Text(
                  '${option.priceDelta > 0 ? '+ ' : ''}${Formatters.currency(option.priceDelta)}',
                  style: AppTypography.caption,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _QuantityStepper extends StatelessWidget {
  const _QuantityStepper({
    required this.quantity,
    required this.maxQuantity,
    required this.onChanged,
  });

  final int quantity;
  final int maxQuantity;
  final void Function(int) onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: AppColors.border),
        borderRadius: AppRadius.radiusMd,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            onPressed: quantity > 1 ? () => onChanged(quantity - 1) : null,
            icon: const Icon(Icons.remove_rounded),
          ),
          SizedBox(
            width: 24,
            child: Text(
              '$quantity',
              textAlign: TextAlign.center,
              style: AppTypography.subtitle,
            ),
          ),
          IconButton(
            onPressed: quantity < maxQuantity
                ? () => onChanged(quantity + 1)
                : null,
            icon: const Icon(Icons.add_rounded),
          ),
        ],
      ),
    );
  }
}
