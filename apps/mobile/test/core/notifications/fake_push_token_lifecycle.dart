import 'package:pedelocal_mobile/core/notifications/push_notification_service.dart';

class FakePushTokenLifecycle implements PushTokenLifecycle {
  int startCalls = 0;
  int unregisterCalls = 0;

  @override
  Future<void> startAfterAuthentication() async {
    startCalls++;
  }

  @override
  Future<void> unregisterCurrentToken() async {
    unregisterCalls++;
  }
}
