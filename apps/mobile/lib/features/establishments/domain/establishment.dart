import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Establishments.EstablishmentResponse`
/// (`GET /api/establishments?cityId=` — `docs/API_CONTRACT.md`).
///
/// Importante: a API não retorna avaliação, tempo de entrega estimado,
/// desconto nem foto do estabelecimento nesta etapa — nenhum desses campos
/// deve ser inventado na UI (instrução do produto). Apenas os campos abaixo
/// existem de fato no contrato.
class Establishment extends Equatable {
  const Establishment({
    required this.id,
    required this.cityId,
    required this.name,
    required this.slug,
    required this.categoryId,
    required this.operationalStatus,
    required this.commercialStatus,
    required this.createdAtUtc,
  });

  final String id;
  final String cityId;
  final String name;
  final String slug;
  final String categoryId;

  /// `Open | ClosedManual | ClosedSchedule | Paused`.
  final String operationalStatus;

  /// `Active | OverdueGrace | SuspendedPayment | SuspendedAdmin | Inactive`.
  final String commercialStatus;
  final DateTime createdAtUtc;

  bool get isOpenNow => operationalStatus == 'Open';

  /// Espelha a regra de `POST /api/orders`: suspenso comercialmente não
  /// aceita pedido (`ESTABLISHMENT_SUSPENDED`). `AcceptsNewOrders` (settings)
  /// **não** vem neste DTO — só é descoberto na criação do pedido.
  bool get isCommerciallyBlocked =>
      commercialStatus == 'SuspendedPayment' ||
      commercialStatus == 'SuspendedAdmin' ||
      commercialStatus == 'Inactive';

  bool get canReceiveNewOrders => isOpenNow && !isCommerciallyBlocked;

  factory Establishment.fromJson(Map<String, dynamic> json) {
    return Establishment(
      id: json['id'] as String,
      cityId: json['cityId'] as String,
      name: json['name'] as String,
      slug: json['slug'] as String,
      categoryId: json['categoryId'] as String,
      operationalStatus: json['operationalStatus'] as String,
      commercialStatus: json['commercialStatus'] as String,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }

  @override
  List<Object?> get props => [
    id,
    cityId,
    name,
    slug,
    categoryId,
    operationalStatus,
    commercialStatus,
  ];
}
