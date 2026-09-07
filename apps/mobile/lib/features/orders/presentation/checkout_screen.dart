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
import '../../addresses/presentation/addresses_screen.dart';
import '../../addresses/domain/address.dart';
import '../../cart/application/cart_controller.dart';
import '../../cities/application/selected_city_controller.dart';
import '../application/checkout_controller.dart';
import '../domain/order.dart';

/// Checkout — mostra estabelecimento/itens/pagamento/endereço/total e cria
/// o pedido via `POST /api/orders`.
///
/// PENDÊNCIA CRÍTICA (ver relatório da Etapa 2): o backend não tem endpoint
/// de preview/cálculo de taxa de entrega antes da confirmação — por isso a
/// taxa e o total final só são conhecidos DEPOIS da criação do pedido
/// (retornados por esta mesma chamada). Este fluxo não deve ser considerado
/// definitivamente concluído do ponto de vista de produto até esse endpoint
/// existir; o subtotal mostrado aqui é só a soma visual dos itens.
class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen> {
  Address? _selectedAddress;
  PaymentMethodOption _paymentMethod = PaymentMethodOption.pix;
  bool _needsChange = false;
  final _changeController = TextEditingController();

  @override
  void dispose() {
    _changeController.dispose();
    super.dispose();
  }

  Future<void> _pickAddress() async {
    final cityId = ref.read(selectedCityControllerProvider)?.id;
    final result = await Navigator.of(context).push<Address>(
      MaterialPageRoute<Address>(
        builder: (_) =>
            AddressesScreen(selectionMode: true, filterCityId: cityId),
      ),
    );
    if (result != null) setState(() => _selectedAddress = result);
  }

  Future<void> _submit() async {
    if (_selectedAddress == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Selecione um endereço de entrega.')),
      );
      return;
    }

    double? changeForAmount;
    if (_paymentMethod == PaymentMethodOption.cash && _needsChange) {
      changeForAmount = double.tryParse(
        _changeController.text.replaceAll(',', '.'),
      );
      if (changeForAmount == null || changeForAmount <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Informe um valor de troco válido.')),
        );
        return;
      }
    }

    await ref
        .read(checkoutControllerProvider.notifier)
        .submit(
          addressId: _selectedAddress!.id,
          paymentMethod: _paymentMethod,
          changeForAmount: changeForAmount,
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
          const SizedBox(height: AppSpacing.md),
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
                const Divider(),
                Row(
                  children: [
                    const Text('Subtotal', style: AppTypography.subtitle),
                    const Spacer(),
                    Text(
                      Formatters.currency(cart.subtotal),
                      style: AppTypography.subtitle,
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xxs),
                const Text(
                  'A taxa de entrega é calculada pelo sistema e exibida junto com o total '
                  'assim que o pedido é confirmado.',
                  style: AppTypography.caption,
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Entrega', style: AppTypography.title),
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
          const SizedBox(height: AppSpacing.lg),
          Text('Forma de pagamento', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalCard(
            child: RadioGroup<PaymentMethodOption>(
              groupValue: _paymentMethod,
              onChanged: (value) => setState(() => _paymentMethod = value!),
              child: Column(
                children: [
                  for (final method in PaymentMethodOption.values)
                    RadioListTile<PaymentMethodOption>(
                      value: method,
                      title: Text(method.label),
                      contentPadding: EdgeInsets.zero,
                    ),
                  if (_paymentMethod == PaymentMethodOption.pix) ...[
                    const Divider(),
                    const Text(
                      'As informações de pagamento via Pix (chave/QR) são fornecidas pelo '
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
            label: 'Confirmar pedido',
            isLoading: isSubmitting,
            onPressed: isSubmitting ? null : _submit,
          ),
        ),
      ),
    );
  }
}
