/// Evita regressão de status quando um evento SignalR chega fora de ordem
/// (`docs/REALTIME_EVENTS.md` — não há replay; o `status` é o do momento do envio).
bool shouldApplyOrderStatus(String current, String incoming) {
  if (current == incoming) return false;
  if (incoming == 'Cancelled') {
    return current != 'Delivered' &&
        current != 'PickupCompleted' &&
        current != 'Cancelled';
  }
  if (current == 'Cancelled') return false;

  final currentRank = _rank[current];
  final incomingRank = _rank[incoming];
  if (currentRank == null || incomingRank == null) return true;
  return incomingRank > currentRank;
}

const _rank = <String, int>{
  'Pending': 0,
  'Accepted': 1,
  'Preparing': 2,
  'Ready': 3,
  'WaitingDriver': 4,
  'DriverAssigned': 5,
  'PickedUp': 6,
  'OutForDelivery': 7,
  'Delivered': 8,
  'PickupCompleted': 8,
};
