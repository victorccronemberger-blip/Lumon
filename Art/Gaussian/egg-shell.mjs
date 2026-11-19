export function eggShell(x, y, z, radius, halfHeight, distanceScale = radius) {
  return (px, py, pz) => {
    const u = (py - y) / halfHeight;
    const section = radius * (1 - 0.18 * Math.min(1, Math.max(-1, u)));
    return (Math.hypot(Math.hypot(px - x, pz - z) / section, u) - 1) * distanceScale;
  };
}
