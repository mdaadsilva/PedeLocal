/// Papel usado para decidir a navegação dentro do app (Cliente x Entregador
/// — "ambos dentro do mesmo aplicativo... navegação baseada na role
/// autenticada"). Os nomes de role reais vêm do backend
/// (`docs/AUTH_FLOW.md#papéis-roles`): `SUPER_ADMIN`, `CUSTOMER`, `DRIVER`,
/// `ESTABLISHMENT_ADMIN`, `ESTABLISHMENT_MANAGER`, `ATTENDANT`,
/// `STOCK_OPERATOR`.
///
/// Este app mobile atende apenas Cliente e Entregador — os demais papéis
/// pertencem ao futuro Painel Web (Next.js), fora do escopo desta etapa.
enum UserRole { customer, driver, unsupported }

extension UserRoleResolution on UserRole {
  static const _driverRole = 'DRIVER';
  static const _customerRole = 'CUSTOMER';

  /// Um usuário pode, em tese, ter mais de um papel (`user_roles`), mas os
  /// fluxos de cadastro atuais atribuem exatamente um por vez. Se ambos
  /// estiverem presentes, entregador tem prioridade (é o papel mais
  /// específico/operacional).
  static UserRole fromApiRoles(List<String> roles) {
    if (roles.contains(_driverRole)) return UserRole.driver;
    if (roles.contains(_customerRole)) return UserRole.customer;
    return UserRole.unsupported;
  }
}
