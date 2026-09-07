import 'package:equatable/equatable.dart';

import '../domain/delivery.dart';
import '../domain/driver_profile.dart';

class DriverHomeState extends Equatable {
  const DriverHomeState({
    required this.profile,
    this.availableDeliveries = const [],
    this.currentDelivery,
    this.isTogglingStatus = false,
    this.isProcessingAction = false,
  });

  final DriverProfile profile;
  final List<Delivery> availableDeliveries;
  final Delivery? currentDelivery;

  /// Enquanto `true`, o botão online/offline fica desabilitado — previne
  /// duplo clique durante a chamada de rede.
  final bool isTogglingStatus;

  /// Enquanto `true`, ações sobre entregas (aceitar/retirar/sair para
  /// entrega/confirmar) ficam desabilitadas — previne duplo clique.
  final bool isProcessingAction;

  DriverHomeState copyWith({
    DriverProfile? profile,
    List<Delivery>? availableDeliveries,
    Delivery? currentDelivery,
    bool clearCurrentDelivery = false,
    bool? isTogglingStatus,
    bool? isProcessingAction,
  }) {
    return DriverHomeState(
      profile: profile ?? this.profile,
      availableDeliveries: availableDeliveries ?? this.availableDeliveries,
      currentDelivery: clearCurrentDelivery
          ? null
          : (currentDelivery ?? this.currentDelivery),
      isTogglingStatus: isTogglingStatus ?? this.isTogglingStatus,
      isProcessingAction: isProcessingAction ?? this.isProcessingAction,
    );
  }

  @override
  List<Object?> get props => [
    profile,
    availableDeliveries,
    currentDelivery,
    isTogglingStatus,
    isProcessingAction,
  ];
}
