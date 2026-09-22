import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Drivers.DriverResponse`
/// (`GET /api/driver/me` — `docs/API_CONTRACT.md`,
/// `docs/DRIVER_ONBOARDING.md`, `docs/DELIVERY_FLOW.md`).
class DriverProfile extends Equatable {
  const DriverProfile({
    required this.id,
    required this.userId,
    required this.name,
    required this.cityId,
    required this.cpf,
    required this.vehicleType,
    required this.pixKeyType,
    required this.pixKey,
    required this.onboardingStatus,
    required this.status,
    required this.eligibilityStatus,
    required this.reviewedAtUtc,
    required this.rejectionReason,
  });

  final String id;
  final String userId;
  final String name;
  final String cityId;
  final String cpf;

  /// `Bike | Motorcycle | Car`.
  final String vehicleType;

  /// `Cpf | Email | Phone | Random`.
  final String pixKeyType;
  final String pixKey;

  /// Decisão de compliance — `PendingDocuments | UnderReview | Approved |
  /// Rejected` (`docs/DRIVER_ONBOARDING.md`). Não existe `Suspended` nem
  /// `DocumentExpired` aqui: isso é [status]/[eligibilityStatus].
  final String onboardingStatus;

  /// Presença em tempo real — `Offline | Available | Busy | Blocked`. Também
  /// é o mecanismo de suspensão administrativa (`Blocked`).
  final String status;

  /// Fonte única de verdade sobre "pode ficar online/aceitar entrega agora,
  /// e se não, por quê" — `Eligible | PendingDocuments | UnderReview |
  /// Rejected | Blocked | DocumentExpired`. Sempre use este campo (não
  /// [onboardingStatus] sozinho) para decidir se mostra o botão "Ficar
  /// online" e para explicar o motivo ao entregador.
  final String eligibilityStatus;

  final DateTime? reviewedAtUtc;
  final String? rejectionReason;

  bool get isEligible => eligibilityStatus == 'Eligible';
  bool get isDocumentExpired => eligibilityStatus == 'DocumentExpired';
  bool get isBlocked => status == 'Blocked';
  bool get isBusy => status == 'Busy';
  bool get isOnline => status == 'Available' || status == 'Busy';

  factory DriverProfile.fromJson(Map<String, dynamic> json) {
    return DriverProfile(
      id: json['id'] as String,
      userId: json['userId'] as String,
      name: json['name'] as String,
      cityId: json['cityId'] as String,
      cpf: json['cpf'] as String,
      vehicleType: json['vehicleType'] as String,
      pixKeyType: json['pixKeyType'] as String,
      pixKey: json['pixKey'] as String,
      onboardingStatus: json['onboardingStatus'] as String,
      status: json['status'] as String,
      eligibilityStatus: json['eligibilityStatus'] as String,
      reviewedAtUtc: json['reviewedAtUtc'] != null
          ? DateTime.parse(json['reviewedAtUtc'] as String)
          : null,
      rejectionReason: json['rejectionReason'] as String?,
    );
  }

  @override
  List<Object?> get props => [
    id,
    userId,
    name,
    cityId,
    onboardingStatus,
    status,
    eligibilityStatus,
  ];
}

/// Mensagem amigável para cada [DriverProfile.eligibilityStatus] — nunca
/// mostrar o enum técnico cru na Home do Entregador
/// (`docs/DRIVER_ONBOARDING.md#elegibilidade-computada-não-persistida`).
extension DriverEligibilityMessage on DriverProfile {
  String get eligibilityTitle => switch (eligibilityStatus) {
    'PendingDocuments' => 'Documentação pendente',
    'UnderReview' => 'Cadastro em análise',
    'Rejected' => 'Cadastro não aprovado',
    'Blocked' => 'Conta suspensa',
    'DocumentExpired' => 'CNH vencida',
    _ => 'Aguardando',
  };

  String get eligibilityMessage => switch (eligibilityStatus) {
    'PendingDocuments' =>
      'Envie os dados da sua CNH e do seu veículo para começarmos a análise '
          'do seu cadastro.',
    'UnderReview' =>
      'Seu cadastro está em análise. Você poderá ficar online assim que for '
          'aprovado por um administrador.',
    'Rejected' =>
      rejectionReason != null && rejectionReason!.isNotEmpty
          ? 'Seu cadastro não foi aprovado. Motivo: $rejectionReason. Você '
                'pode reenviar seus documentos para nova análise.'
          : 'Seu cadastro não foi aprovado. Você pode reenviar seus documentos '
                'para nova análise.',
    'Blocked' =>
      'Seu acesso como entregador foi suspenso. Entre em contato com o '
          'suporte para mais informações.',
    'DocumentExpired' =>
      'Sua CNH está vencida. Atualize a validade nos seus documentos para '
          'voltar a ficar online.',
    _ => 'Aguardando liberação do seu cadastro.',
  };
}
