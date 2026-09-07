/// Caminhos centralizados dos assets de marca do PedeLocal.
///
/// Os arquivos vivem em `assets/branding/` (declarado como diretório no
/// `pubspec.yaml`, não como arquivos individuais) justamente para que
/// `logoHorizontal` e `icon` — que ainda não foram entregues fisicamente —
/// possam ser adicionados depois sem qualquer alteração de código ou
/// `pubspec.yaml`. Todo widget que exibe esses assets (ver
/// `lib/core/widgets/brand_logo.dart`) deve tratar a ausência do arquivo
/// com um fallback visual (nunca deixar a tela quebrar).
abstract class BrandAssets {
  BrandAssets._();

  static const String logo = 'assets/branding/logo.png';
  static const String logoHorizontal = 'assets/branding/logo_horizontal.png';
  static const String icon = 'assets/branding/icon.png';
}
