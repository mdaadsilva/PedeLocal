import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// AppBar padrão da marca — título consistente, sem elevação (o tema global
/// já cuida disso), com espaço opcional para ações.
class PedeLocalAppBar extends StatelessWidget implements PreferredSizeWidget {
  const PedeLocalAppBar({
    super.key,
    this.title,
    this.titleWidget,
    this.actions,
    this.leading,
    this.automaticallyImplyLeading = true,
    this.backgroundColor,
  });

  final String? title;
  final Widget? titleWidget;
  final List<Widget>? actions;
  final Widget? leading;
  final bool automaticallyImplyLeading;
  final Color? backgroundColor;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    return AppBar(
      title: titleWidget ?? (title != null ? Text(title!) : null),
      actions: actions,
      leading: leading,
      automaticallyImplyLeading: automaticallyImplyLeading,
      backgroundColor: backgroundColor ?? AppColors.surface,
    );
  }
}
