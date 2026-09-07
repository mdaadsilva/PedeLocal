import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Establishments.EstablishmentCategoryResponse`
/// (`GET /api/establishment-categories` — público).
class EstablishmentCategory extends Equatable {
  const EstablishmentCategory({required this.id, required this.name});

  final String id;
  final String name;

  factory EstablishmentCategory.fromJson(Map<String, dynamic> json) {
    return EstablishmentCategory(
      id: json['id'] as String,
      name: json['name'] as String,
    );
  }

  @override
  List<Object?> get props => [id, name];
}
