import { sstep } from "../../Tools/lumon-splat-authoring/src/authoring/island.js";
import { norm } from "../../Tools/lumon-splat-authoring/src/authoring/scene-kit.js";

function dot(a, b) {
  return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}

export function bakeDirectionalShadow(casters, direction, extent, texelSize) {
  const right = norm([direction[2], 0, -direction[0]]);
  const up = [
    direction[1] * right[2],
    direction[2] * right[0] - direction[0] * right[2],
    -direction[1] * right[0],
  ];
  const side = Math.ceil(2 * extent / texelSize) + 1;
  const depth = new Float32Array(side * side);
  const nearest = extent;
  const farthest = -extent;

  for (let row = 0; row < side; row++) {
    const v = -extent + row * texelSize;
    for (let col = 0; col < side; col++) {
      const u = -extent + col * texelSize;
      const ox = right[0] * u + up[0] * v;
      const oy = up[1] * v;
      const oz = right[2] * u + up[2] * v;
      let w = nearest;
      while (w > farthest) {
        const distance = casters.dist(ox + direction[0] * w, oy + direction[1] * w, oz + direction[2] * w);
        if (distance < 0.010) break;
        w -= Math.max(0.012, distance * 0.72);
      }
      depth[row * side + col] = w > farthest ? w : -Infinity;
    }
  }

  function visibility(x, y, z, normalFacing) {
    const position = [x, y, z];
    const u = (dot(position, right) + extent) / texelSize;
    const v = (dot(position, up) + extent) / texelSize;
    const col = Math.round(u);
    const row = Math.round(v);
    if (col < 1 || row < 1 || col >= side - 1 || row >= side - 1) return 1;
    const bias = 0.024 + 0.061 * (1 - Math.max(0, normalFacing));
    const receiver = dot(position, direction) + bias;
    let blocked = 0, totalWeight = 0;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const weight = (1.52 - Math.abs(u - col - dx)) * (1.52 - Math.abs(v - row - dy));
        const caster = depth[(row + dy) * side + col + dx];
        blocked += weight * sstep(0.010, 0.080, caster - receiver);
        totalWeight += weight;
      }
    }
    return 1 - blocked / totalWeight;
  }

  return { visibility, texelSize, side };
}
