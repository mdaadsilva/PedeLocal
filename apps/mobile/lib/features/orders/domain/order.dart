import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Orders.OrderResponse` — usado igualmente
/// para criação, detalhe (`GET /api/orders/{id}`) e listagem
/// (`GET /api/orders`), conforme `docs/API_CONTRACT.md`/`docs/ORDER_FLOW.md`.
///
/// Importante: este DTO **não** inclui dados do estabelecimento (só
/// `establishmentId`), do endereço (só `addressId`), nem do entregador —
/// resolvidos separadamente pela UI a partir de outros endpoints já
/// autorizados para o cliente (ver pendências da Etapa 2 quanto a dados do
/// entregador, que o backend não expõe ao cliente).
class Order extends Equatable {
  const Order({
    required this.id,
    required this.customerUserId,
    required this.establishmentId,
    required this.cityId,
    required this.addressId,
    required this.status,
    required this.paymentMethod,
    required this.paymentStatus,
    this.changeForAmount,
    required this.subtotal,
    required this.deliveryFee,
    required this.total,
    this.calculatedDistanceKm,
    required this.createdAtUtc,
    required this.items,
    required this.statusHistory,
  });

  final String id;
  final String customerUserId;
  final String establishmentId;
  final String cityId;
  final String addressId;

  /// `Pending, Accepted, Preparing, Ready, WaitingDriver, DriverAssigned,
  /// PickedUp, OutForDelivery, Delivered, Cancelled`.
  final String status;

  /// `Pix, Cash, DebitOnDelivery, CreditOnDelivery`.
  final String paymentMethod;

  /// `Pending, Paid, NotApplicable`.
  final String paymentStatus;
  final double? changeForAmount;
  final double subtotal;
  final double deliveryFee;
  final double total;
  final double? calculatedDistanceKm;
  final DateTime createdAtUtc;
  final List<OrderItem> items;
  final List<OrderStatusHistoryEntry> statusHistory;

  bool get isCancelled => status == 'Cancelled';
  bool get isDelivered => status == 'Delivered';
  bool get isActive => !isCancelled && !isDelivered;

  /// O código de 6 dígitos é gerado em `READY`, mas o backend **não o
  /// expõe** ao cliente (só o hash — `docs/ORDER_FLOW.md`). A UI mostra o
  /// aviso a partir deste estágio até a entrega, sem inventar o número.
  bool get shouldShowDeliveryCodeNotice => const {
    'Ready',
    'WaitingDriver',
    'DriverAssigned',
    'PickedUp',
    'OutForDelivery',
  }.contains(status);

  static const List<String> deliveryLifecycle = [
    'Pending',
    'Accepted',
    'Preparing',
    'Ready',
    'WaitingDriver',
    'DriverAssigned',
    'PickedUp',
    'OutForDelivery',
    'Delivered',
  ];

  factory Order.fromJson(Map<String, dynamic> json) {
    return Order(
      id: json['id'] as String,
      customerUserId: json['customerUserId'] as String,
      establishmentId: json['establishmentId'] as String,
      cityId: json['cityId'] as String,
      addressId: json['addressId'] as String,
      status: json['status'] as String,
      paymentMethod: json['paymentMethod'] as String,
      paymentStatus: json['paymentStatus'] as String,
      changeForAmount: (json['changeForAmount'] as num?)?.toDouble(),
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      total: (json['total'] as num).toDouble(),
      calculatedDistanceKm: (json['calculatedDistanceKm'] as num?)?.toDouble(),
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      items: (json['items'] as List<dynamic>? ?? const [])
          .map((e) => OrderItem.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
      statusHistory: (json['statusHistory'] as List<dynamic>? ?? const [])
          .map(
            (e) => OrderStatusHistoryEntry.fromJson(e as Map<String, dynamic>),
          )
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [
    id,
    status,
    paymentStatus,
    subtotal,
    deliveryFee,
    total,
    items,
  ];
}

class OrderItem extends Equatable {
  const OrderItem({
    required this.id,
    required this.productId,
    required this.productName,
    required this.unitPrice,
    required this.quantity,
    required this.lineTotal,
    required this.options,
  });

  final String id;
  final String productId;
  final String productName;
  final double unitPrice;
  final int quantity;
  final double lineTotal;
  final List<OrderItemOption> options;

  factory OrderItem.fromJson(Map<String, dynamic> json) {
    return OrderItem(
      id: json['id'] as String,
      productId: json['productId'] as String,
      productName: json['productName'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      quantity: json['quantity'] as int,
      lineTotal: (json['lineTotal'] as num).toDouble(),
      options: (json['options'] as List<dynamic>? ?? const [])
          .map((e) => OrderItemOption.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [
    id,
    productName,
    unitPrice,
    quantity,
    lineTotal,
    options,
  ];
}

class OrderItemOption extends Equatable {
  const OrderItemOption({
    required this.id,
    required this.optionName,
    required this.priceDelta,
  });

  final String id;
  final String optionName;
  final double priceDelta;

  factory OrderItemOption.fromJson(Map<String, dynamic> json) {
    return OrderItemOption(
      id: json['id'] as String,
      optionName: json['optionName'] as String,
      priceDelta: (json['priceDelta'] as num).toDouble(),
    );
  }

  @override
  List<Object?> get props => [id, optionName, priceDelta];
}

/// `Order.paymentMethod` (`docs/FRONTEND_GUIDE.md#enums-de-status`).
enum PaymentMethodOption { pix, cash, debitOnDelivery, creditOnDelivery }

extension PaymentMethodOptionApi on PaymentMethodOption {
  String get apiValue => switch (this) {
    PaymentMethodOption.pix => 'Pix',
    PaymentMethodOption.cash => 'Cash',
    PaymentMethodOption.debitOnDelivery => 'DebitOnDelivery',
    PaymentMethodOption.creditOnDelivery => 'CreditOnDelivery',
  };

  String get label => switch (this) {
    PaymentMethodOption.pix => 'Pix',
    PaymentMethodOption.cash => 'Dinheiro',
    PaymentMethodOption.debitOnDelivery => 'Débito na entrega',
    PaymentMethodOption.creditOnDelivery => 'Crédito na entrega',
  };
}

/// Rótulos amigáveis para exibição do status do pedido
/// (`Order.status` — `docs/ORDER_FLOW.md#máquina-de-estados`).
extension OrderStatusLabel on String {
  String get orderStatusLabel => switch (this) {
    'Pending' => 'Aguardando confirmação',
    'Accepted' => 'Pedido aceito',
    'Preparing' => 'Em preparo',
    'Ready' => 'Pronto',
    'WaitingDriver' => 'Aguardando entregador',
    'DriverAssigned' => 'Entregador a caminho da loja',
    'PickedUp' => 'Entregador retirou o pedido',
    'OutForDelivery' => 'Saiu para entrega',
    'Delivered' => 'Entregue',
    'Cancelled' => 'Cancelado',
    _ => this,
  };
}

class OrderStatusHistoryEntry extends Equatable {
  const OrderStatusHistoryEntry({
    required this.status,
    required this.changedAtUtc,
    this.notes,
  });

  final String status;
  final DateTime changedAtUtc;
  final String? notes;

  factory OrderStatusHistoryEntry.fromJson(Map<String, dynamic> json) {
    return OrderStatusHistoryEntry(
      status: json['status'] as String,
      changedAtUtc: DateTime.parse(json['changedAtUtc'] as String),
      notes: json['notes'] as String?,
    );
  }

  @override
  List<Object?> get props => [status, changedAtUtc, notes];
}
