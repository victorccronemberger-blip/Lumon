// Shared by the editable game emitter and the standalone pose exporter.
const rgb = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
const mix = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);
function sstep(a, b, v) {
  const t = Math.max(0, Math.min(1, (v - a) / (b - a)));
  return t * t * (3 - 2 * t);
}

export const flamePeriod = 4.8;
export const flameTongues = [
  {
    id: "core",
    position: [0.001, 0.633, 0.112],
    height: 0.272,
    width: 0.091,
    depth: 0.049,
    curl: -0.044,
    phase: 0.38,
    frequency: 2,
  },
  {
    id: "left",
    position: [-0.1, 0.631, 0.108],
    height: 0.178,
    width: 0.068,
    depth: 0.043,
    curl: -0.041,
    phase: 2.39,
    frequency: 3,
  },
  {
    id: "right",
    position: [0.1, 0.633, 0.094],
    height: 0.218,
    width: 0.07,
    depth: 0.052,
    curl: 0.048,
    phase: 4.47,
    frequency: 2,
  },
];

const coalRed = rgb("#c44727");
const copper = rgb("#f87524");
const gold = rgb("#ffb938");
const whiteGold = rgb("#fff3ad");

export function flameProfile(tongue) {
  const { height, width, depth, curl, id } = tongue;
  const centerX = (t) => curl * t * t + 0.02 * Math.sin(t * Math.PI * 1.73) * t;
  const breadth = (t) => (0.63 + 0.37 * sstep(0.0, 0.28, t)) * Math.max(0.06, 1 - t ** 1.24);
  const limit = (value) => Math.max(0, Math.min(1, value));

  const distance = (x, y, z) => {
    const t = limit(y / height);
    const u = Math.abs(x - centerX(t)) / width;
    const v = Math.abs(z + t * 0.019) / depth;
    const facet = Math.max(u, v, (u + v) * Math.SQRT1_2);
    return Math.max(-y - 0.016, y - height, (facet - breadth(t)) * Math.min(width, depth));
  };
  function shade(x, y, z) {
    const t = limit(y / height);
    const edge = Math.abs(x - centerX(t)) / Math.max(0.012, width * breadth(t));
    const contour = edge + t * 0.59 + 0.11 * Math.sin(9 * t + x * 26);
    const tongueGlow = 1 - sstep(0.47, 0.57, contour);
    const hot = mix(copper, gold, 1 - sstep(0.76, 0.88, contour));
    return {
      color: mix(
        mix(coalRed, hot, 1 - sstep(1.04, 1.16, contour)),
        id === "core" ? whiteGold : gold,
        tongueGlow,
      ),
      opacity: 0.89 - 0.28 * sstep(0.63, 1.0, t) + (z < -0.05 ? 0.03 : 0),
    };
  }
  return { distance, shade, centerX, breadth };
}

export function addFirePose(surface, phase, spill = true) {
  if (!Number.isFinite(phase)) throw Error("Expected a finite flame phase");
  const cycle = 2 * Math.PI * phase,
    heat = 0.5 + 0.31 * Math.sin(3 * cycle) + 0.19 * Math.sin(7 * cycle);
  if (spill) {
    const lamp = [0, 0.681, 0.158],
      range = 0.88,
      strength = 0.05 + (0.26 - 0.05) * heat;
    surface.recolor((p, n, color) => {
      const delta = p.map((v, i) => v - lamp[i]),
        d = Math.hypot(...delta);
      if (d >= range || d < 0.00001) return color;
      const facing = Math.max(0, -n.reduce((sum, v, i) => sum + (v * delta[i]) / d, 0));
      const gain =
        (1 - (d * d) / (range * range)) ** 2 *
        sstep(-0.32, 0.29, delta[2] / d) *
        (0.19 + 0.81 * facing) *
        strength;
      return color.map((v, i) => Math.min(255, v + 255 * gain * [1, 0.53, 0.14][i]));
    });
  }
  for (const tongue of flameTongues) {
    const { centerX, breadth, shade } = flameProfile(tongue);
    const a = cycle * tongue.frequency + tongue.phase;
    const lean = ((8 * Math.PI) / 180) * Math.sin(a),
      stretch = Math.cos(a);
    const q = Math.SQRT2 - 1,
      sections = [
        [-1, -q],
        [-1, q],
        [-q, 1],
        [q, 1],
        [1, q],
        [1, -q],
        [q, -1],
        [-q, -1],
      ];
    const local = (pair, t) => [
      centerX(t) + pair[0] * tongue.width * breadth(t),
      t * tongue.height,
      pair[1] * tongue.depth * breadth(t) - t * 0.019,
    ];
    const pose = ([x, y, z]) => {
      const u = x * (1 - 0.06 * stretch),
        v = y * (1 + 0.18 * stretch);
      return [
        tongue.position[0] + u * Math.cos(lean) - v * Math.sin(lean),
        tongue.position[1] + u * Math.sin(lean) + v * Math.cos(lean),
        tongue.position[2] + z,
      ];
    };
    for (let row = 0; row < 24; row++)
      for (let i = 0; i < sections.length; i++) {
        const t = row / 24,
          tt = (row + 1) / 24,
          p = sections[i],
          q = sections[(i + 1) % sections.length];
        const paint = (position, n, u, v) => {
          const point = local(
            p.map((k, j) => k + (q[j] - k) * u),
            t + (tt - t) * v,
          );
          return shade(...point).color.map((k) => k * 255);
        };
        surface.quad(
          pose(local(p, t)),
          pose(local(q, t)),
          pose(local(q, tt)),
          pose(local(p, tt)),
          paint,
          0.0048,
        );
      }
  }
}
