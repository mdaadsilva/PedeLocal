import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../maps/maps_launcher.dart';
import '../theme/app_colors.dart';
import '../theme/app_radius.dart';
import '../theme/app_spacing.dart';
import '../theme/app_typography.dart';
import 'pedelocal_button.dart';

/// Mini-mapa com pin quando há coordenadas reais.
///
/// Sem chave nativa de Google Maps o tile pode falhar — o botão "Abrir rota"
/// continua funcionando via `url_launcher` (não depende da chave).
class MapPinPreview extends StatelessWidget {
  const MapPinPreview({
    super.key,
    required this.latitude,
    required this.longitude,
    this.highlighted = false,
    this.height = 180,
    this.openRouteLabel = 'Abrir rota',
  });

  final double latitude;
  final double longitude;
  final bool highlighted;
  final double height;
  final String openRouteLabel;

  bool get _hasValidCoordinates =>
      latitude.abs() > 0.0001 || longitude.abs() > 0.0001;

  @override
  Widget build(BuildContext context) {
    if (!_hasValidCoordinates) {
      return const SizedBox.shrink();
    }

    final target = LatLng(latitude, longitude);
    final accent = highlighted ? AppColors.accent : AppColors.primary;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: AppRadius.radiusLg,
          child: SizedBox(
            height: height,
            child: Stack(
              children: [
                GoogleMap(
                  initialCameraPosition: CameraPosition(
                    target: target,
                    zoom: 16,
                  ),
                  markers: {
                    Marker(
                      markerId: const MarkerId('pin'),
                      position: target,
                      icon: highlighted
                          ? BitmapDescriptor.defaultMarkerWithHue(
                              BitmapDescriptor.hueOrange,
                            )
                          : BitmapDescriptor.defaultMarkerWithHue(
                              BitmapDescriptor.hueGreen,
                            ),
                    ),
                  },
                  zoomControlsEnabled: false,
                  myLocationButtonEnabled: false,
                  compassEnabled: false,
                  mapToolbarEnabled: false,
                  liteModeEnabled: true,
                  onTap: (_) => MapsLauncher.openPin(latitude, longitude),
                ),
                Positioned(
                  left: AppSpacing.sm,
                  top: AppSpacing.sm,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.sm,
                      vertical: AppSpacing.xxs,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.surface.withValues(alpha: 0.92),
                      borderRadius: AppRadius.radiusPill,
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.location_on_rounded,
                          size: 16,
                          color: accent,
                        ),
                        const SizedBox(width: AppSpacing.xxs),
                        Text(
                          highlighted ? 'Ponto rural' : 'Localização',
                          style: AppTypography.caption.copyWith(color: accent),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalButton(
          label: openRouteLabel,
          icon: Icons.directions_rounded,
          variant: PedeLocalButtonVariant.secondary,
          onPressed: () => MapsLauncher.openRoute(latitude, longitude),
        ),
      ],
    );
  }
}
