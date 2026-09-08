import '../../features/session/domain/user_role.dart';
import '../routing/app_routes.dart';
import 'notification_payload.dart';

/// Resolve a rota do `go_router` a partir do payload FCM + sessão local.
///
/// A role vem sempre da sessão autenticada (`GET /api/me/profile`), nunca
/// do payload. Sem autenticação, sem role suportada, ou sem identificador
/// utilizável, devolve `null` — o chamador não navega.
class NotificationRouter {
  const NotificationRouter();

  String? resolve({
    required NotificationPayload payload,
    required bool isAuthenticated,
    required UserRole? role,
  }) {
    if (!isAuthenticated || role == null || payload.isEmpty) {
      return null;
    }

    switch (role) {
      case UserRole.customer:
        if (payload.orderId != null) {
          return AppRoutes.orderTracking(payload.orderId!);
        }
        return null;

      case UserRole.driver:
        if (payload.eventType == NotificationEventTypes.paymentReported) {
          return AppRoutes.driverReceivables;
        }
        return AppRoutes.homeDriver;

      case UserRole.unsupported:
        return null;
    }
  }
}
