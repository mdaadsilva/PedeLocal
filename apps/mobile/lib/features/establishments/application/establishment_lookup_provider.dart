import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/establishments_repository.dart';
import '../domain/establishment.dart';

/// Busca leve de um estabelecimento por id (`GET /api/establishments/{id}`)
/// — usada onde só o nome/status é necessário (ex.: acompanhamento de
/// pedido), sem carregar produtos/categorias como
/// `establishmentDetailControllerProvider` faz.
final establishmentLookupProvider =
    FutureProvider.family<Establishment, String>((ref, id) {
      return ref.read(establishmentsRepositoryProvider).getById(id);
    });
