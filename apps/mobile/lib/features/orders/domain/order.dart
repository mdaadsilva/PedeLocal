import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Orders.OrderResponse` — usado igualmente
/// para criação, detalhe (`GET /api/orders/{id}`) e listagem
/// (`GET /api/orders`), conforme `docs/API_CONTRACT.md`/`docs/ORDER_FLOW.md`.
///
/// `type` é `Delivery` ou `Pickup`. `addressId` é `null` em retirada.
/// O código de confirmação não vem neste DTO — use
/// `GET /api/orders/{id}/delivery-code`.
class Order extends Equatable {
  const Order({
    required this.id,
    required this.customerUserId,
    required this.establishmentId,
    required this.cityId,
    this.addressId,
    required this.status,
    this.type = 'Delivery',
    required this.paymentMethod,
    required this.paymentStatus,
    this.changeForAmount,
    required this.subtotal,
    required this.deliveryFee,
    required this.total,
    this.calculatedDistanceKm,
    this.notes,
    required this.createdAtUtc,
    required this.items,
    required this.statusHistory,
  });

  final String id;
  final String customerUserId;
  final String establishmentId;
  final String cityId;

  /// Obrigatório em `Delivery`; `null` em `Pickup`.
  final String? addressId;

  /// `Pending, Accepted, Preparing, Ready, WaitingDriver, DriverAssigned,
  /// PickedUp, OutForDelivery, Delivered, PickupCompleted, Cancelled`.
  final String status;

  /// `Delivery | Pickup` (`docs/ORDER_FLOW.md`).
  final String type;

  /// `Pix, Cash, DebitOnDelivery, CreditOnDelivery`.
  final String paymentMethod;

  /// `Pending, Paid, NotApplicable`.
  final String paymentStatus;
  final double? changeForAmount;
  final double subtotal;
  final double deliveryFee;
  final double total;
  final double? calculatedDistanceKm;
  final String? notes;
  final DateTime createdAtUtc;
  final List<OrderItem> items;
  final List<OrderStatusHistoryEntry> statusHistory;

  bool get isDelivery => type == 'Delivery';
  bool get isPickup => type == 'Pickup';
  bool get isCancelled => status == 'Cancelled';
  bool get isDelivered => status == 'Delivered';
  bool get isPickupCompleted => status == 'PickupCompleted';
  bool get isCompleted => isDelivered || isPickupCompleted;
  bool get isActive => !isCancelled && !isCompleted;

  /// Código de 6 dígitos só existe em `Delivery` a partir de `READY`
  /// (`GET /api/orders/{id}/delivery-code` — `docs/DELIVERY_FLOW.md`).
  /// `Pickup` nunca gera código.
  bool get shouldShowDeliveryCodeNotice =>
      isDelivery &&
      const {
        'Ready',
        'WaitingDriver',
        'DriverAssigned',
        'PickedUp',
        'OutForDelivery',
      }.contains(status);

  bool get shouldShowPickupReadyNotice => isPickup && status == 'Ready';

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

  static const List<String> pickupLifecycle = [
    'Pending',
    'Accepted',
    'Preparing',
    'Ready',
    'PickupCompleted',
  ];

  List<String> get lifecycle => isPickup ? pickupLifecycle : deliveryLifecycle;

  factory Order.fromJson(Map<String, dynamic> json) {
    return Order(
      id: json['id'] as String,
      customerUserId: json['customerUserId'] as String,
      establishmentId: json['establishmentId'] as String,
      cityId: json['cityId'] as String,
      addressId: json['addressId'] as String?,
      status: json['status'] as String,
      type: json['type'] as String? ?? 'Delivery',
      paymentMethod: json['paymentMethod'] as String,
      paymentStatus: json['paymentStatus'] as String,
      changeForAmount: (json['changeForAmount'] as num?)?.toDouble(),
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      total: (json['total'] as num).toDouble(),
      calculatedDistanceKm: (json['calculatedDistanceKm'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
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
    type,
    paymentStatus,
    subtotal,
    deliveryFee,
    total,
    notes,
    items,
  ];
}

/// `Order.type` — `Delivery` (padrão) ou `Pickup`.
enum OrderFulfillmentType { delivery, pickup }

extension OrderFulfillmentTypeApi on OrderFulfillmentType {
  String get apiValue => switch (this) {
    OrderFulfillmentType.delivery => 'Delivery',
    OrderFulfillmentType.pickup => 'Pickup',
  };

  String get label => switch (this) {
    OrderFulfillmentType.delivery => 'Entrega',
    OrderFulfillmentType.pickup => 'Retirada no estabelecimento',
  };
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

  String get label => labelFor(isPickup: false);

  String labelFor({required bool isPickup}) => switch (this) {
    PaymentMethodOption.pix => 'Pix',
    PaymentMethodOption.cash => 'Dinheiro',
    PaymentMethodOption.debitOnDelivery =>
      isPickup ? 'Débito na retirada' : 'Débito na entrega',
    PaymentMethodOption.creditOnDelivery =>
      isPickup ? 'Crédito na retirada' : 'Crédito na entrega',
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
    'PickupCompleted' => 'Retirado no estabelecimento',
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
