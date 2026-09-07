/// Modelos espelhando `PedeLocal.Contracts.Auth` exatamente
/// (`docs/API_CONTRACT.md`, `docs/AUTH_FLOW.md`). Nomes de campo em
/// camelCase no JSON (serialização padrão do ASP.NET Core).
library;

class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresAtUtc,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresAtUtc;

  factory AuthTokens.fromJson(Map<String, dynamic> json) {
    return AuthTokens(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      accessTokenExpiresAtUtc: DateTime.parse(
        json['accessTokenExpiresAtUtc'] as String,
      ),
    );
  }
}

class RegisterCustomerResult {
  const RegisterCustomerResult({
    required this.userId,
    required this.name,
    required this.email,
  });

  final String userId;
  final String name;
  final String email;

  factory RegisterCustomerResult.fromJson(Map<String, dynamic> json) {
    return RegisterCustomerResult(
      userId: json['userId'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
    );
  }
}

class RegisterDriverResult {
  const RegisterDriverResult({
    required this.userId,
    required this.driverId,
    required this.approvalStatus,
  });

  final String userId;
  final String driverId;

  /// `PendingApproval | Approved | Rejected` — recém-cadastrado sempre nasce
  /// `PendingApproval` (`docs/AUTH_FLOW.md`).
  final String approvalStatus;

  factory RegisterDriverResult.fromJson(Map<String, dynamic> json) {
    return RegisterDriverResult(
      userId: json['userId'] as String,
      driverId: json['driverId'] as String,
      approvalStatus: json['approvalStatus'] as String,
    );
  }
}

/// `Driver.vehicleType` (`docs/FRONTEND_GUIDE.md#enums-de-status`).
enum VehicleType { bike, motorcycle, car }

extension VehicleTypeApi on VehicleType {
  String get apiValue => switch (this) {
    VehicleType.bike => 'Bike',
    VehicleType.motorcycle => 'Motorcycle',
    VehicleType.car => 'Car',
  };

  String get label => switch (this) {
    VehicleType.bike => 'Bicicleta',
    VehicleType.motorcycle => 'Moto',
    VehicleType.car => 'Carro',
  };
}

/// `Driver.pixKeyType`.
enum PixKeyType { cpf, email, phone, random }

extension PixKeyTypeApi on PixKeyType {
  String get apiValue => switch (this) {
    PixKeyType.cpf => 'Cpf',
    PixKeyType.email => 'Email',
    PixKeyType.phone => 'Phone',
    PixKeyType.random => 'Random',
  };

  String get label => switch (this) {
    PixKeyType.cpf => 'CPF',
    PixKeyType.email => 'E-mail',
    PixKeyType.phone => 'Telefone',
    PixKeyType.random => 'Chave aleatória',
  };
}
