/// Caminhos de rota centralizados — nenhuma tela deve usar strings de rota
/// "soltas" em `context.go(...)`.
abstract class AppRoutes {
  AppRoutes._();

  static const splash = '/';
  static const login = '/login';
  static const registerCustomer = '/register/customer';
  static const registerDriver = '/register/driver';
  static const forgotPassword = '/forgot-password';
  static const selectCity = '/select-city';
  static const homeCustomer = '/home/customer';
  static const homeDriver = '/home/driver';
  static const unsupportedRole = '/unsupported-role';

  // Etapa 2 — fluxo de pedido/entrega (cliente).
  static const establishmentDetailPattern = '/establishments/:id';
  static String establishmentDetail(String establishmentId) =>
      '/establishments/$establishmentId';

  static const cart = '/cart';
  static const checkout = '/checkout';

  static const orderHistory = '/orders';
  static const orderTrackingPattern = '/orders/:orderId';
  static String orderTracking(String orderId) => '/orders/$orderId';

  static const addresses = '/addresses';
  static const profile = '/profile';

  // Etapa 2 — fluxo do entregador.
  static const driverReceivables = '/driver/receivables';

  // Onboarding do entregador (CNH/veículo/status) — `docs/DRIVER_ONBOARDING.md`.
  static const driverOnboarding = '/driver/onboarding';
  static const driverOnboardingLicense = '/driver/onboarding/license';
  static const driverOnboardingVehicle = '/driver/onboarding/vehicle';
}
