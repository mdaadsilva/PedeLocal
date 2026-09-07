/// Espelha `PedeLocal.Contracts.Me.MyProfileResponse`
/// (`GET/PUT /api/me/profile` — `docs/API_CONTRACT.md`).
class MyProfile {
  const MyProfile({
    required this.id,
    required this.name,
    required this.email,
    required this.phoneNumber,
    required this.roles,
    required this.createdAtUtc,
  });

  final String id;
  final String name;
  final String email;
  final String phoneNumber;

  /// Nomes exatos de `docs/AUTH_FLOW.md#papéis-roles`
  /// (`SUPER_ADMIN`, `CUSTOMER`, `DRIVER`, `ESTABLISHMENT_ADMIN`, ...).
  final List<String> roles;
  final DateTime createdAtUtc;

  factory MyProfile.fromJson(Map<String, dynamic> json) {
    return MyProfile(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      phoneNumber: json['phoneNumber'] as String,
      roles: (json['roles'] as List<dynamic>? ?? const [])
          .map((e) => e.toString())
          .toList(),
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }
}
