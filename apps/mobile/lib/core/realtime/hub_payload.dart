/// Lê o primeiro argumento de um evento SignalR como mapa camelCase.
/// O backend envia objetos anônimos (`new { orderId, status }`) serializados
/// em camelCase — nunca inventamos campos.
Map<String, dynamic>? readHubPayload(List<Object?>? arguments) {
  if (arguments == null || arguments.isEmpty) return null;
  final payload = arguments.first;
  if (payload is Map) {
    return payload.map((key, value) => MapEntry(key.toString(), value));
  }
  return null;
}

String? readHubString(Map<String, dynamic> payload, String key) {
  final value = payload[key];
  if (value == null) return null;
  final text = value.toString();
  return text.isEmpty ? null : text;
}
