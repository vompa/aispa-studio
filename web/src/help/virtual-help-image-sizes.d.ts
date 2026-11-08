// Wird zur Bauzeit von `helpImageSizes` in `vite.config.ts` erzeugt: Breite und Höhe je Bild (`app/<name>`, `web/<name>`).
declare module "virtual:help-image-sizes" {
  const sizes: Record<string, [width: number, height: number]>;
  export default sizes;
}
