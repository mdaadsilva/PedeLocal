import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Drivers.DriverResponse`
/// (`GET /api/driver/me` — `docs/API_CONTRACT.md`, `docs/DELIVERY_FLOW.md`).
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
    required this.approvalStatus,
    required this.status,
    required this.approvedAtUtc,
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

  /// `PendingApproval | Approved | Rejected`.
  final String approvalStatus;

  /// `Offline | Available | Busy | Blocked`.
  final String status;
  final DateTime? approvedAtUtc;

  bool get isApproved => approvalStatus == 'Approved';
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
      approvalStatus: json['approvalStatus'] as String,
      status: json['status'] as String,
      approvedAtUtc: json['approvedAtUtc'] != null
          ? DateTime.parse(json['approvedAtUtc'] as String)
          : null,
    );
  }

  @override
  List<Object?> get props => [id, userId, name, cityId, approvalStatus, status];
}
