import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../../addresses/domain/address.dart';
import '../../addresses/presentation/addresses_screen.dart';
import '../../cart/application/cart_controller.dart';
import '../../cities/application/selected_city_controller.dart';
import '../../home_customer/presentation/widgets/category_chip.dart';
import '../application/checkout_controller.dart';
import '../domain/order.dart';
import '../domain/order_preview.dart';

/// Checkout — entrega ou retirada, preview real (`POST /api/orders/preview`)
/// e criação (`POST /api/orders`).
class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen> {
  OrderFulfillmentType _orderType = OrderFulfillmentType.delivery;
  Address? _selectedAddress;
  PaymentMethodOption _paymentMethod = PaymentMethodOption.pix;
  bool _needsChange = false;
  final _changeController = TextEditingController();
  final _notesController = TextEditingController();

  @override
  void dispose() {
    _changeController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  bool get _isPickup => _orderType == OrderFulfillmentType.pickup;

  double? get _changeForAmount {
    if (_paymentMethod != PaymentMethodOption.cash || !_needsChange) {
      return null;
    }
    return double.tryParse(_changeController.text.replaceAll(',', '.'));
  }

  String? get _notes {
    final text = _notesController.text.trim();
    return text.isEmpty ? null : text;
  }

  Future<void> _refreshPreview() {
    return ref
        .read(checkoutPreviewControllerProvider.notifier)
        .load(
          orderType: _orderType,
          addressId: _isPickup ? null : _selectedAddress?.id,
          paymentMethod: _paymentMethod,
          changeForAmount: _changeForAmount,
          notes: _notes,
        );
  }

  Future<void> _setOrderType(OrderFulfillmentType type) async {
    if (type == _orderType) return;
    setState(() => _orderType = type);
    await _refreshPreview();
  }

  Future<void> _pickAddress() async {
    final cityId = ref.read(selectedCityControllerProvider)?.id;
    final result = await Navigator.of(context).push<Address>(
      MaterialPageRoute<Address>(
        builder: (_) =>
            AddressesScreen(selectionMode: true, filterCityId: cityId),
      ),
    );
    if (result != null) {
      setState(() => _selectedAddress = result);
      await _refreshPreview();
    }
  }

  Future<void> _submit() async {
    if (!_isPickup && _selectedAddress == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Selecione um endereço de entrega.')),
      );
      return;
    }

    if (_notes != null && _notes!.length > 1000) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('A observação deve ter no máximo 1000 caracteres.'),
        ),
      );
      return;
    }

    if (_paymentMethod == PaymentMethodOption.cash && _needsChange) {
      final change = _changeForAmount;
      if (change == null || change <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Informe um valor de troco válido.')),
        );
        return;
      }
    }

    await ref
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: _orderType,
          addressId: _isPickup ? null : _selectedAddress?.id,
          paymentMethod: _paymentMethod,
          changeForAmount: _changeForAmount,
          notes: _notes,
        );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(checkoutControllerProvider, (previous, next) {
      next.when(
        data: (order) {
          if (order != null) {
            context.pushReplacement(AppRoutes.orderTracking(order.id));
          }
        },
        loading: () {},
        error: (error, stackTrace) {
          final message = error is AppException
              ? error.message
              : 'Não foi possível concluir seu pedido.';
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text(message)));
        },
      );
    });

    final cart = ref.watch(cartControllerProvider);
    final isSubmitting = ref.watch(checkoutControllerProvider).isLoading;
    final previewAsync = ref.watch(checkoutPreviewControllerProvider);
    final preview = previewAsync.value;
    final canConfirm =
        !isSubmitting &&
        preview != null &&
        (_isPickup || _selectedAddress != null);

    if (cart.items.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: const Text('Checkout')),
        body: const EmptyStateView(
          title: 'Seu carrinho está vazio',
          message: 'Volte ao estabelecimento para adicionar itens.',
          icon: Icons.shopping_bag_outlined,
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Checkout')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          Row(
            children: [
              const Icon(
                Icons.storefront_rounded,
                size: 18,
                color: AppColors.primary,
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Text(
                  cart.establishmentName ?? '',
                  style: AppTypography.subtitle,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Como você quer receber?', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            spacing: AppSpacing.sm,
            children: [
              CategoryChip(
                label: 'Entrega',
                selected: !_isPickup,
                onTap: () => _setOrderType(OrderFulfillmentType.delivery),
              ),
              CategoryChip(
                label: 'Retirar no estabelecimento',
                selected: _isPickup,
                onTap: () => _setOrderType(OrderFulfillmentType.pickup),
              ),
            ],
          ),
          if (_isPickup) ...[
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'Você retira o pedido no estabelecimento. Sem taxa de entrega '
              'e sem entregador.',
              style: AppTypography.caption,
            ),
          ],
          const SizedBox(height: AppSpacing.lg),
          Text('Itens', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalCard(
            child: Column(
              children: [
                for (final item in cart.items) ...[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('${item.quantity}x ', style: AppTypography.body),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(item.productName, style: AppTypography.body),
                            if (item.selectedOptions.isNotEmpty)
                              Text(
                                item.selectedOptions
                                    .map((o) => o.name)
                                    .join(', '),
                                style: AppTypography.caption,
                              ),
                          ],
                        ),
                      ),
                      Text(
                        Formatters.currency(item.lineTotal),
                        style: AppTypography.body,
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xs),
                ],
              ],
            ),
          ),
          if (!_isPickup) ...[
            const SizedBox(height: AppSpacing.lg),
            Text('Endereço de entrega', style: AppTypography.title),
            const SizedBox(height: AppSpacing.sm),
            PedeLocalCard(
              onTap: _pickAddress,
              child: Row(
                children: [
                  const Icon(
                    Icons.location_on_outlined,
                    color: AppColors.primary,
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      _selectedAddress?.summary ??
                          'Selecionar endereço de entrega',
                      style: AppTypography.body,
                    ),
                  ),
                  const Icon(
                    Icons.chevron_right_rounded,
                    color: AppColors.textSecondary,
                  ),
                ],
              ),
            ),
          ] else ...[
            const SizedBox(height: AppSpacing.lg),
            Text('Retirada', style: AppTypography.title),
            const SizedBox(height: AppSpacing.sm),
            PedeLocalCard(
              child: Row(
                children: [
                  const Icon(
                    Icons.storefront_outlined,
                    color: AppColors.primary,
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'Retire em ${cart.establishmentName ?? 'o estabelecimento'}',
                      style: AppTypography.body,
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.lg),
          Text('Forma de pagamento', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalCard(
            child: RadioGroup<PaymentMethodOption>(
              groupValue: _paymentMethod,
              onChanged: (value) {
                setState(() => _paymentMethod = value!);
                _refreshPreview();
              },
              child: Column(
                children: [
                  for (final method in PaymentMethodOption.values)
                    RadioListTile<PaymentMethodOption>(
                      value: method,
                      title: Text(method.labelFor(isPickup: _isPickup)),
                      contentPadding: EdgeInsets.zero,
                    ),
                  if (_paymentMethod == PaymentMethodOption.pix) ...[
                    const Divider(),
                    Text(
                      _isPickup
                          ? 'O Pix é combinado direto com o estabelecimento na retirada.'
                          : 'As informações de pagamento via Pix (chave/QR) são fornecidas pelo '
                                'estabelecimento fora deste app. O pagamento só é confirmado pelo '
                                'estabelecimento, não pelo simples "copiei o código".',
                      style: AppTypography.caption,
                    ),
                  ],
                  if (_paymentMethod == PaymentMethodOption.cash) ...[
                    const Divider(),
                    CheckboxListTile(
                      value: _needsChange,
                      onChanged: (value) =>
                          setState(() => _needsChange = value ?? false),
                      title: const Text('Precisa de troco?'),
                      contentPadding: EdgeInsets.zero,
                      controlAffinity: ListTileControlAffinity.leading,
                    ),
                    if (_needsChange) ...[
                      const SizedBox(height: AppSpacing.xs),
                      PedeLocalTextField(
                        controller: _changeController,
                        label: 'Troco para quanto?',
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                      ),
                    ],
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Observações', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalTextField(
            controller: _notesController,
            label: 'Observação para o pedido (opcional)',
            hint: 'Ex.: interfone 12, sem cebola...',
            maxLines: 3,
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Resumo', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          _PreviewTotals(
            cartSubtotal: cart.subtotal,
            preview: preview,
            isLoading: previewAsync.isLoading,
            error: previewAsync.error,
            isPickup: _isPickup,
            needsAddress: !_isPickup && _selectedAddress == null,
            onRetry: _refreshPreview,
          ),
          const SizedBox(height: AppSpacing.xxl),
        ],
      ),
      bottomNavigationBar: Container(
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
        child: SafeArea(
          top: false,
          child: PedeLocalButton(
            label: _isPickup ? 'Confirmar retirada' : 'Confirmar pedido',
            isLoading: isSubmitting,
            onPressed: canConfirm ? _submit : null,
          ),
        ),
      ),
    );
  }
}

class _PreviewTotals extends StatelessWidget {
  const _PreviewTotals({
    required this.cartSubtotal,
    required this.preview,
    required this.isLoading,
    required this.error,
    required this.isPickup,
    required this.needsAddress,
    required this.onRetry,
  });

  final double cartSubtotal;
  final OrderPreview? preview;
  final bool isLoading;
  final Object? error;
  final bool isPickup;
  final bool needsAddress;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    if (needsAddress) {
      return const PedeLocalCard(
        child: Text(
          'Selecione um endereço para calcular a taxa e o total.',
          style: AppTypography.bodyMuted,
        ),
      );
    }

    if (isLoading) {
      return const PedeLocalCard(
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
          child: Center(
            child: SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
        ),
      );
    }

    if (error != null) {
      final message = error is AppException
          ? (error as AppException).message
          : 'Não foi possível calcular o total.';
      return PedeLocalCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(message, style: AppTypography.body),
            const SizedBox(height: AppSpacing.sm),
            TextButton(onPressed: onRetry, child: const Text('Tentar novamente')),
          ],
        ),
      );
    }

    final subtotal = preview?.subtotal ?? cartSubtotal;
    final fee = preview?.deliveryFee ?? (isPickup ? 0 : null);
    final total = preview?.total;

    return PedeLocalCard(
      child: Column(
        children: [
          _TotalRow(label: 'Subtotal', value: subtotal),
          _TotalRow(
            label: 'Taxa de entrega',
            value: fee,
            fallback: isPickup ? Formatters.currency(0) : 'A calcular',
          ),
          if (preview?.distanceKm != null) ...[
            const SizedBox(height: AppSpacing.xxs),
            Row(
              children: [
                const Text('Distância', style: AppTypography.caption),
                const Spacer(),
                Text(
                  '${preview!.distanceKm!.toStringAsFixed(1)} km',
                  style: AppTypography.caption,
                ),
              ],
            ),
          ],
          const SizedBox(height: AppSpacing.xs),
          _TotalRow(
            label: 'Total',
            value: total,
            fallback: 'Confirme após o cálculo',
            isEmphasis: true,
          ),
        ],
      ),
    );
  }
}

class _TotalRow extends StatelessWidget {
  const _TotalRow({
    required this.label,
    this.value,
    this.fallback,
    this.isEmphasis = false,
  });

  final String label;
  final double? value;
  final String? fallback;
  final bool isEmphasis;

  @override
  Widget build(BuildContext context) {
    final style = isEmphasis ? AppTypography.subtitle : AppTypography.body;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Text(label, style: style),
          const Spacer(),
          Text(
            value != null ? Formatters.currency(value!) : (fallback ?? '—'),
            style: style,
          ),
        ],
      ),
    );
  }
}
