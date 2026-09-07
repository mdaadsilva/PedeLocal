/// Espelha `PedeLocal.Contracts.Common.PagedResult<T>` — usado por todos os
/// endpoints de listagem paginada (`docs/FRONTEND_GUIDE.md#paginação`).
class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    final rawItems = json['items'] as List<dynamic>? ?? const [];
    return PagedResult<T>(
      items: rawItems.map((e) => fromJsonT(e as Map<String, dynamic>)).toList(),
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? rawItems.length,
      totalCount: json['totalCount'] as int? ?? rawItems.length,
      totalPages: json['totalPages'] as int? ?? 1,
    );
  }

  bool get hasNextPage => page < totalPages;
}
