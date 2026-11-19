import { shape } from "../../Tools/lumon-splat-authoring/src/authoring/sdf.js";
import { flameProfile, flameTongues, flamePeriod } from "./painted-flame.mjs";
export { flameTongues, flamePeriod };
export const forgeAngle = -1.90;
export const hearthX = 0.13;

export function makeFlame(tongue) {
  const { distance, shade } = flameProfile(tongue);
  return { field: shape().add(distance), shade };
}

export function forgeFireData(station) {
  const [x, , z] = station.position;
  const cosine = Math.cos(forgeAngle), sine = Math.sin(forgeAngle);
  return {
    origin: [x + hearthX * cosine, 0, z - hearthX * sine],
    yaw: forgeAngle * 180 / Math.PI,
    period: flamePeriod,
    tongues: flameTongues.map(({ id, position, phase, frequency }) => ({ id, position, phase, frequency })),
  };
}
