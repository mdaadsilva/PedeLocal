import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Cities.CityResponse`
/// (`GET /api/cities` — público, retorna só cidades ativas).
class City extends Equatable {
  const City({
    required this.id,
    required this.name,
    required this.state,
    required this.isActive,
  });

  final String id;
  final String name;
  final String state;
  final bool isActive;

  factory City.fromJson(Map<String, dynamic> json) {
    return City(
      id: json['id'] as String,
      name: json['name'] as String,
      state: json['state'] as String,
      isActive: json['isActive'] as bool,
    );
  }

  @override
  List<Object?> get props => [id, name, state, isActive];
}

/// Recorte mínimo persistido localmente como "última cidade escolhida".
class SelectedCity extends Equatable {
  const SelectedCity({required this.id, required this.name});

  final String id;
  final String name;

  @override
  List<Object?> get props => [id, name];
}
