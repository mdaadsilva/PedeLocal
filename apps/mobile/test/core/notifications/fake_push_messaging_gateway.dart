import 'dart:async';

import 'package:pedelocal_mobile/core/notifications/push_messaging_gateway.dart';

class FakePushMessagingGateway implements PushMessagingGateway {
  FakePushMessagingGateway({this.available = true, this.token = 'fcm-token'});

  bool available;
  String? token;
  int permissionRequests = 0;
  int initializeCalls = 0;
  PushIncomingMessage? initialMessage;

  final tokenRefreshController = StreamController<String>.broadcast();
  final foregroundController = StreamController<PushIncomingMessage>.broadcast();
  final openedController = StreamController<PushIncomingMessage>.broadcast();

  @override
  Future<bool> ensureInitialized() async {
    initializeCalls++;
    return available;
  }

  @override
  Future<void> requestPermission() async {
    permissionRequests++;
  }

  @override
  Future<String?> getToken() async => available ? token : null;

  @override
  Stream<String> get onTokenRefresh => tokenRefreshController.stream;

  @override
  Stream<PushIncomingMessage> get onForegroundMessage =>
      foregroundController.stream;

  @override
  Stream<PushIncomingMessage> get onMessageOpenedApp => openedController.stream;

  @override
  Future<PushIncomingMessage?> getInitialMessage() async => initialMessage;

  Future<void> dispose() async {
    await tokenRefreshController.close();
    await foregroundController.close();
    await openedController.close();
  }
}
