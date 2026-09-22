import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../network/app_exception.dart';
import '../theme/app_colors.dart';
import '../theme/app_radius.dart';
import '../theme/app_spacing.dart';
import '../theme/app_typography.dart';
import '../uploads/uploads_repository.dart';
import 'pedelocal_card.dart';

/// Seletor + upload de foto de documento (CNH, CRLV) — encapsula
/// `POST /api/uploads/image` (`docs/FRONTEND_GUIDE.md#upload-de-imagens`) e
/// devolve a `url` relativa pronta para ser enviada em `documentImageUrl`.
///
/// Sempre opcional para o backend quando o campo aceita `null` — o
/// chamador decide se bloqueia o envio do formulário sem foto.
class DocumentImagePicker extends ConsumerStatefulWidget {
  const DocumentImagePicker({
    super.key,
    required this.label,
    required this.currentImageUrl,
    required this.onUploaded,
  });

  final String label;
  final String? currentImageUrl;
  final ValueChanged<String> onUploaded;

  @override
  ConsumerState<DocumentImagePicker> createState() =>
      _DocumentImagePickerState();
}

class _DocumentImagePickerState extends ConsumerState<DocumentImagePicker> {
  bool _isUploading = false;

  Future<void> _pick(ImageSource source) async {
    final picker = ImagePicker();
    final XFile? file;
    try {
      file = await picker.pickImage(source: source, imageQuality: 85);
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Não foi possível acessar a câmera/galeria.')),
      );
      return;
    }
    if (file == null) return;

    setState(() => _isUploading = true);
    try {
      final url = await ref
          .read(uploadsRepositoryProvider)
          .uploadImage(file.path);
      widget.onUploaded(url);
    } on AppException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
    } finally {
      if (mounted) setState(() => _isUploading = false);
    }
  }

  Future<void> _openSourceSheet() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: const Text('Tirar foto'),
              onTap: () => Navigator.of(context).pop(ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('Escolher da galeria'),
              onTap: () => Navigator.of(context).pop(ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source != null) await _pick(source);
  }

  @override
  Widget build(BuildContext context) {
    final hasImage = widget.currentImageUrl != null;
    return PedeLocalCard(
      onTap: _isUploading ? null : _openSourceSheet,
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: AppRadius.radiusMd,
            ),
            child: _isUploading
                ? const Padding(
                    padding: EdgeInsets.all(12),
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Icon(
                    hasImage
                        ? Icons.check_circle_outline
                        : Icons.camera_alt_outlined,
                    color: hasImage ? AppColors.success : AppColors.primary,
                  ),
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(widget.label, style: AppTypography.body),
                Text(
                  hasImage ? 'Foto enviada — toque para trocar' : 'Toque para anexar uma foto',
                  style: AppTypography.caption,
                ),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded, color: AppColors.textSecondary),
        ],
      ),
    );
  }
}
