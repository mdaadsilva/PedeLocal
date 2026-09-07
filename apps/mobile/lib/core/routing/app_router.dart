import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/addresses/presentation/addresses_screen.dart';
import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/register_customer_screen.dart';
import '../../features/auth/presentation/register_driver_screen.dart';
import '../../features/cart/presentation/cart_screen.dart';
import '../../features/cities/application/selected_city_controller.dart';
import '../../features/cities/presentation/city_selection_screen.dart';
import '../../features/drivers/presentation/driver_receivables_screen.dart';
import '../../features/drivers/presentation/home_driver_screen.dart';
import '../../features/establishments/presentation/establishment_detail_screen.dart';
import '../../features/home_customer/presentation/home_customer_screen.dart';
import '../../features/orders/presentation/checkout_screen.dart';
import '../../features/orders/presentation/order_history_screen.dart';
import '../../features/orders/presentation/order_tracking_screen.dart';
import '../../features/session/application/session_controller.dart';
import '../../features/session/domain/session_state.dart';
import '../../features/session/domain/user_role.dart';
import '../../features/session/presentation/profile_screen.dart';
import '../../features/session/presentation/splash_screen.dart';
import '../../features/session/presentation/unsupported_role_screen.dart';
import 'app_routes.dart';
import 'router_refresh_notifier.dart';

/// Rotas públicas — acessíveis sem sessão autenticada.
const _publicPaths = {
  AppRoutes.splash,
  AppRoutes.login,
  AppRoutes.registerCustomer,
  AppRoutes.registerDriver,
  AppRoutes.forgotPassword,
};

final goRouterProvider = Provider<GoRouter>((ref) {
  final refreshNotifier = ref.watch(routerRefreshNotifierProvider);

  return GoRouter(
    initialLocation: AppRoutes.splash,
    refreshListenable: refreshNotifier,
    redirect: (context, state) => _redirect(ref, state.matchedLocation),
    routes: [
      GoRoute(
        path: AppRoutes.splash,
        builder: (context, state) => const SplashScreen(),
      ),
      GoRoute(
        path: AppRoutes.login,
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: AppRoutes.registerCustomer,
        builder: (context, state) => const RegisterCustomerScreen(),
      ),
      GoRoute(
        path: AppRoutes.registerDriver,
        builder: (context, state) => const RegisterDriverScreen(),
      ),
      GoRoute(
        path: AppRoutes.forgotPassword,
        builder: (context, state) => const ForgotPasswordScreen(),
      ),
      GoRoute(
        path: AppRoutes.selectCity,
        builder: (context, state) => const CitySelectionScreen(),
      ),
      GoRoute(
        path: AppRoutes.homeCustomer,
        builder: (context, state) => const HomeCustomerScreen(),
      ),
      GoRoute(
        path: AppRoutes.homeDriver,
        builder: (context, state) => const HomeDriverScreen(),
      ),
      GoRoute(
        path: AppRoutes.driverReceivables,
        builder: (context, state) => const DriverReceivablesScreen(),
      ),
      GoRoute(
        path: AppRoutes.unsupportedRole,
        builder: (context, state) => const UnsupportedRoleScreen(),
      ),
      GoRoute(
        path: AppRoutes.establishmentDetailPattern,
        builder: (context, state) => EstablishmentDetailScreen(
          establishmentId: state.pathParameters['id']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.cart,
        builder: (context, state) => const CartScreen(),
      ),
      GoRoute(
        path: AppRoutes.checkout,
        builder: (context, state) => const CheckoutScreen(),
      ),
      GoRoute(
        path: AppRoutes.orderHistory,
        builder: (context, state) => const OrderHistoryScreen(),
      ),
      GoRoute(
        path: AppRoutes.orderTrackingPattern,
        builder: (context, state) =>
            OrderTrackingScreen(orderId: state.pathParameters['orderId']!),
      ),
      GoRoute(
        path: AppRoutes.addresses,
        builder: (context, state) => const AddressesScreen(),
      ),
      GoRoute(
        path: AppRoutes.profile,
        builder: (context, state) => const ProfileScreen(),
      ),
    ],
  );
});

String? _redirect(Ref ref, String currentPath) {
  final sessionAsync = ref.read(sessionControllerProvider);

  // Sessão ainda resolvendo (bootstrap) — mantém na Splash.
  if (sessionAsync.isLoading || sessionAsync.hasError) {
    return currentPath == AppRoutes.splash ? null : AppRoutes.splash;
  }

  final session = sessionAsync.value ?? const SessionState.unauthenticated();

  if (!session.isAuthenticated) {
    // A Splash é pública apenas enquanto a sessão está resolvendo (acima);
    // uma vez resolvida como não autenticada, sempre sai da Splash rumo ao
    // Login — do contrário o app ficaria preso nela para sempre.
    final isAllowedUnauthPath =
        _publicPaths.contains(currentPath) && currentPath != AppRoutes.splash;
    return isAllowedUnauthPath ? null : AppRoutes.login;
  }

  switch (session.role) {
    case UserRole.driver:
      final isAllowedDriverPath =
          currentPath == AppRoutes.homeDriver ||
          currentPath == AppRoutes.driverReceivables ||
          currentPath == AppRoutes.profile;
      return isAllowedDriverPath ? null : AppRoutes.homeDriver;

    case UserRole.customer:
      final selectedCity = ref.read(selectedCityControllerProvider);
      if (selectedCity == null) {
        return currentPath == AppRoutes.selectCity
            ? null
            : AppRoutes.selectCity;
      }
      if (currentPath == AppRoutes.selectCity) {
        // Cidade já escolhida anteriormente — não precisa escolher de novo.
        return AppRoutes.homeCustomer;
      }
      // Telas da Etapa 2 (estabelecimento/carrinho/checkout/pedidos/
      // endereços/perfil) também são destinos válidos para o Cliente — só
      // volta pra Home fora dessas.
      final isAllowedCustomerPath =
          currentPath == AppRoutes.homeCustomer ||
          currentPath.startsWith('/establishments/') ||
          currentPath == AppRoutes.cart ||
          currentPath == AppRoutes.checkout ||
          currentPath == AppRoutes.orderHistory ||
          currentPath.startsWith('/orders/') ||
          currentPath == AppRoutes.addresses ||
          currentPath == AppRoutes.profile;
      return isAllowedCustomerPath ? null : AppRoutes.homeCustomer;

    case UserRole.unsupported:
    case null:
      return currentPath == AppRoutes.unsupportedRole
          ? null
          : AppRoutes.unsupportedRole;
  }
}
