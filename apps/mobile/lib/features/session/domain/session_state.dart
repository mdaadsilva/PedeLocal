import 'package:equatable/equatable.dart';

import 'user_role.dart';

/// Estado de autenticação resolvido (a partir de token salvo + confirmação
/// via `GET /api/me/profile`, nunca só a partir de dados locais — "backend
/// continua sendo a fonte de verdade").
class SessionState extends Equatable {
  const SessionState({required this.isAuthenticated, this.role, this.userName});

  const SessionState.unauthenticated()
    : isAuthenticated = false,
      role = null,
      userName = null;

  const SessionState.authenticated({required this.role, required this.userName})
    : isAuthenticated = true;

  final bool isAuthenticated;
  final UserRole? role;
  final String? userName;

  @override
  List<Object?> get props => [isAuthenticated, role, userName];
}
