import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logging/logging.dart';

import '../../features/me/data/me_repository.dart';
import '../storage/preferences_service.dart';
import '../storage/secure_storage_service.dart';
import 'device_platform_name.dart';
import 'firebase_push_messaging_gateway.dart';
import 'push_messaging_gateway.dart';

/// Ciclo de vida do token FCM ligado à sessão. Implementação real em
/// [PushNotificationService]; testes usam um fake sem Firebase.
abstract class PushTokenLifecycle {
  Future<void> startAfterAuthentication();

  Future<void> unregisterCurrentToken();
}

/// Obtém o token FCM, registra em `PUT /api/me/device-tokens` e remove em
/// `DELETE /api/me/device-tokens` no logout (`docs/PUSH_NOTIFICATIONS.md`).
///
/// Falha de registro nunca impede login. Sem Firebase, todos os métodos
/// retornam sem efeito.
class PushNotificationService implements PushTokenLifecycle {
  PushNotificationService({
    required this._gateway,
    required this._meRepository,
    required this._storage,
    required this._preferences,
    required this._platformResolver,
  });

  final PushMessagingGateway _gateway;
  final MeRepository _meRepository;
  final SecureStorageService _storage;
  final PreferencesService _preferences;
  final String Function() _platformResolver;

  final _log = Logger('Push');

  String? _registeredToken;
  StreamSubscription<String>? _refreshSubscription;

  @override
  Future<void> startAfterAuthentication() async {
    try {
      final available = await _gateway.ensureInitialized();
      if (!available) return;

      await _requestPermissionOnce();
      await _registerIfNeeded(await _gateway.getToken());
      _listenTokenRefresh();
    } catch (error) {
      _log.warning('Falha ao iniciar push (login segue). $error');
    }
  }

  @override
  Future<void> unregisterCurrentToken() async {
    final token = _registeredToken ?? await _storage.readFcmToken();
    _registeredToken = null;
    await _refreshSubscription?.cancel();
    _refreshSubscription = null;

    if (token == null || token.isEmpty) {
      await _storage.clearFcmToken();
      return;
    }

    try {
      await _meRepository.removeDeviceToken(token);
    } catch (error) {
      _log.warning('Falha ao remover device token no logout. $error');
    }

    await _storage.clearFcmToken();
  }

  Future<void> _requestPermissionOnce() async {
    if (_preferences.notificationPermissionAsked) return;
    try {
      await _gateway.requestPermission();
    } finally {
      await _preferences.markNotificationPermissionAsked();
    }
  }

  void _listenTokenRefresh() {
    if (_refreshSubscription != null) return;
    _refreshSubscription = _gateway.onTokenRefresh.listen((token) {
      unawaited(_registerIfNeeded(token));
    }, onError: (Object error) {
      _log.warning('onTokenRefresh falhou. $error');
    });
  }

  Future<void> _registerIfNeeded(String? token) async {
    if (token == null || token.isEmpty) return;
    if (_registeredToken == token) return;

    try {
      await _meRepository.registerDeviceToken(
        token: token,
        platform: _platformResolver(),
      );
      _registeredToken = token;
      await _storage.saveFcmToken(token);
      _log.info('Device token registrado (len=${token.length}).');
    } catch (error) {
      _log.warning('Falha ao registrar device token. $error');
    }
  }
}

final pushMessagingGatewayProvider = Provider<PushMessagingGateway>((ref) {
  return FirebasePushMessagingGateway();
});

final pushNotificationServiceProvider = Provider<PushNotificationService>((
  ref,
) {
  return PushNotificationService(
    gateway: ref.watch(pushMessagingGatewayProvider),
    meRepository: ref.watch(meRepositoryProvider),
    storage: ref.watch(secureStorageServiceProvider),
    preferences: ref.watch(preferencesServiceProvider),
    platformResolver: ref.watch(devicePlatformResolverProvider),
  );
});

final pushTokenLifecycleProvider = Provider<PushTokenLifecycle>((ref) {
  return ref.watch(pushNotificationServiceProvider);
});
