import 'package:equatable/equatable.dart';

import '../../establishments/domain/establishment.dart';
import '../../establishments/domain/establishment_category.dart';

class HomeCustomerState extends Equatable {
  const HomeCustomerState({
    required this.establishments,
    required this.categories,
    this.totalCount = 0,
  });

  final List<Establishment> establishments;
  final List<EstablishmentCategory> categories;
  final int totalCount;

  @override
  List<Object?> get props => [establishments, categories, totalCount];
}
