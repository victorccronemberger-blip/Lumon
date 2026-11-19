using System.Globalization;
using UnityEngine;
using GaussianSplatting.Runtime;

namespace Lumon.HubWorld
{
    [DefaultExecutionOrder(20)]
    public sealed class SplatGuardian : MonoBehaviour
    {
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private Camera view;
        [SerializeField] private Transform artwork;
        private Vector3 velocity;
        private float stepClock;
        private GuardianGait gait;
        private GaussianSplatRenderer splatRenderer;
        private SplatSceneLight sceneLight;
        private float keyVisibility = 1f;
        private float paintedCycle = .25f;
        private float walkStrength;
        private int paintedPhase;
        private int paintedDirection;

        internal bool NetworkDriven { get; set; }
        internal Vector3 NetworkTarget { get; set; }
        internal float NetworkYaw { get; set; }
        internal Vector4 ClothDye { get; set; }
        internal const float WalkSpeed = 1.5f;
        private const float StrideMetres = 1.38f;
        private const int WalkFrames = 20;
        public const int AtlasDirections = 8;
        public const int AtlasBankRows = 14;
        public const int TileWidth = 196, TileHeight = 284;

        public bool Moving { get; private set; }
        internal bool SceneLightingEnabled { get; set; } = true;

        private void OnEnable()
        {
            gait = GuardianGait.Create(artwork);
            if (artwork != null) artwork.TryGetComponent(out splatRenderer);
            sceneLight = SplatSceneLight.Load();
            keyVisibility = sceneLight?.Sample(transform.position) ?? 1f;
            SetOcclusionOutline(true);
        }

        private void OnDisable()
        {
            ResetTravel();
            SetOcclusionOutline(false);
            gait?.Dispose();
            gait = null;
            if (splatRenderer != null) splatRenderer.ColorMultiplier = Vector3.one;
        }

        public string CapturePose()
        {
            string pose = splatRenderer != null && splatRenderer.m_PaintedAtlas != null
                ? System.FormattableString.Invariant($"paintedDirection={paintedDirection} paintedPhase={paintedPhase} cycle={paintedCycle:F3} strideWeight={walkStrength:F2}")
                : gait?.CapturePose(artwork) ?? "gait=unbound";
            return pose + $" keyLight={keyVisibility.ToString("F3", CultureInfo.InvariantCulture)}";
        }

        internal void SetOcclusionOutline(bool enabled)
        {
            if (splatRenderer == null) return;
            if (enabled && view != null && view.TryGetComponent(out SplatPixelRender pixelRender))
                splatRenderer.SetOcclusionOutline(view, pixelRender.PixelSize);
            else
                splatRenderer.SetOcclusionOutline(null, 0);
        }

        public void Configure(HubSplatWorld owner, Camera camera, Transform model)
        {
            world = owner;
            view = camera;
            artwork = model;
        }

        internal Vector3 ReadDirection()
        {
            if (view == null || world == null || world.InputPaused) return Vector3.zero;
            Vector2 input = HubQaInput.Move;
            float x = input.x;
            float y = input.y;
            Vector3 forward = view.transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = view.transform.right;
            right.y = 0;
            right.Normalize();
            return Vector3.ClampMagnitude(forward * y + right * x, 1f);
        }

        internal void ResetTravel()
        {
            velocity = Vector3.zero;
            walkStrength = 0f;
            paintedPhase = 0;
            paintedCycle = .25f;
        }

        internal static Vector3 WalkDelta(ref Vector3 currentVelocity, Vector3 input, float dt)
        {
            Vector3 before = currentVelocity;
            Vector3 target = Vector3.ClampMagnitude(input, 1f) * WalkSpeed;
            float acceleration = target.sqrMagnitude < before.sqrMagnitude ? 9f : 7f;
            currentVelocity = Vector3.MoveTowards(before, target, acceleration * dt);
            // Integrate the ramp and any remaining time spent at the target speed.
            float rampTime = Mathf.Min(dt, (target - before).magnitude / acceleration);
            return (before + currentVelocity) * (.5f * rampTime) + currentVelocity * (dt - rampTime);
        }

        private void Update()
        {
            if (NetworkDriven)
            {
                Vector3 before = transform.position;
                transform.position = Vector3.Lerp(before, NetworkTarget, 1f - Mathf.Exp(-23f * Time.unscaledDeltaTime));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, NetworkYaw, 0), 720f * Time.unscaledDeltaTime);
                float travel = Vector3.Distance(before, transform.position);
                Moving = travel > 0.0001f;
                UpdatePose(travel, world != null && world.ReducedMotion);
                return;
            }
            Vector3 direction = ReadDirection();
            if (world == null) return;
            if (world.InputPaused) velocity = Vector3.zero;
            Vector3 current = transform.position;
            Vector3 delta = WalkDelta(ref velocity, direction, Time.deltaTime);
            Vector3 next = delta.sqrMagnitude > 0f ? world.Constrain(current, current + delta) : current;
            transform.position = next;
            Vector3 step = next - current;
            Moving = step.magnitude > .045f * Time.deltaTime;
            if (Moving)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(step), 720f * Time.deltaTime);
            float projected = Vector3.Dot(step, transform.forward);
            float forwardDistance = projected >= step.magnitude * 0.995f ? Mathf.Max(0f, projected) : 0f;
            UpdatePose(splatRenderer != null && splatRenderer.m_PaintedAtlas != null ? step.magnitude : forwardDistance, world.ReducedMotion);
        }

        private void LateUpdate()
        {
            if (splatRenderer == null) return;
            if (view != null && splatRenderer.m_PaintedAtlas != null)
            {
                artwork.rotation = view.transform.rotation;
                float angle = Mathf.DeltaAngle(view.transform.eulerAngles.y + 180f, transform.eulerAngles.y);
                paintedDirection = Mathf.RoundToInt(angle / 45f);
                // Left to right in the sheet: S, SE, E, NE, N, NW, W, SW.
                int column = (AtlasDirections - paintedDirection) % AtlasDirections;
                int bank = paintedPhase / AtlasBankRows;
                int row = paintedPhase % AtlasBankRows;
                splatRenderer.PaintedFrame = new Vector4((column + bank * AtlasDirections) * TileWidth,
                    (AtlasBankRows - 1 - row) * TileHeight, TileWidth, 0);
                UpdateFeet(column);
            }
            float target = SceneLightingEnabled && sceneLight != null ? sceneLight.Sample(transform.position) : 1f;
            keyVisibility = world != null && world.ReducedMotion
                ? target
                : Mathf.MoveTowards(keyVisibility, target, 7.5f * Time.deltaTime);
            splatRenderer.ClothDye = ClothDye;
            splatRenderer.ColorMultiplier = Vector3.Lerp(new Vector3(0.78f, 0.85f, 0.94f), Vector3.one, keyVisibility);
        }

        private void UpdatePose(float distance, bool reduceMotion)
        {
            if (splatRenderer != null && splatRenderer.m_PaintedAtlas != null)
            {
                AdvancePaintedWalk(distance, NetworkDriven ? Time.unscaledDeltaTime : Time.deltaTime, reduceMotion);
                return;
            }
            if (gait != null)
            {
                gait.Advance(distance, Time.deltaTime, reduceMotion);
                return;
            }
            if (artwork == null) return;
            if (distance <= 0f || reduceMotion)
            {
                artwork.localRotation = Quaternion.identity;
                return;
            }
            stepClock += Time.deltaTime * 10f;
            artwork.localRotation = Quaternion.Euler(Mathf.Sin(stepClock) * 2.5f, 0f, Mathf.Sin(stepClock * 0.5f) * 1.5f);
        }

        private void AdvancePaintedWalk(float distance, float dt, bool reduced)
        {
            if (reduced || distance > .75f)
            {
                walkStrength = 0f;
                paintedPhase = 0;
                paintedCycle = .25f;
                return;
            }
            float passing = .25f + .5f * Mathf.Round((paintedCycle - .25f) * 2f);
            if (distance > .055f * dt)
            {
                walkStrength = Mathf.MoveTowards(walkStrength, 1f, dt / .16f);
                if (walkStrength >= 1f) paintedCycle += distance / StrideMetres;
            }
            else if (walkStrength >= 1f && Mathf.Abs(paintedCycle - passing) > .003f)
            {
                paintedCycle = Mathf.MoveTowards(paintedCycle, passing, dt * 1.45f);
            }
            else
            {
                walkStrength = Mathf.MoveTowards(walkStrength, 0f, dt / .15f);
            }
            paintedCycle = Mathf.Repeat(paintedCycle, 1f);
            int strengthLevel = Mathf.RoundToInt(walkStrength * 4f);
            if (strengthLevel == 0) paintedPhase = 0;
            else if (strengthLevel == 4) paintedPhase = 1 + Mathf.RoundToInt(paintedCycle * WalkFrames) % WalkFrames;
            else paintedPhase = WalkFrames + (paintedCycle < .5f ? 0 : 3) + strengthLevel;
        }

        // Same ground trajectory as the offline rig. Between drawings, move the splats
        // around each boot by the residual distance so the supporting sole stays planted.
        private static Vector2 FootPath(float t)
        {
            const float stance = .54f;
            if (t < stance) return new Vector2(StrideMetres * (stance * .5f - t), 0f);
            float u = (t - stance) / (1f - stance);
            float travel = stance * (u * u * (3f - 2f * u) - .5f) - (1f - stance) * u * (1f - u) * (1f - 2f * u);
            float lift = 11f * (1.55f / 244f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 1.5f) * (1.2f - .4f * u);
            return new Vector2(StrideMetres * travel, lift);
        }

        private void UpdateFeet(int column)
        {
            if (walkStrength < 1f || paintedPhase < 1 || paintedPhase > WalkFrames)
            {
                splatRenderer.PaintedFootA = splatRenderer.PaintedFootB = Vector4.zero;
                return;
            }
            float heading = column * (Mathf.PI / 4f);
            float dx = Mathf.Sin(heading), depth = Mathf.Cos(heading);
            float dy = .5299193f * depth; // the authored 32-degree camera
            float sampled = (paintedPhase - 1f) / WalkFrames;
            Vector4 Foot(int side)
            {
                int sign = 2 * side - 1;
                Vector2 from = FootPath(Mathf.Repeat(sampled + side * .5f, 1f));
                Vector2 to = FootPath(Mathf.Repeat(paintedCycle + side * .5f, 1f));
                Vector2 d = to - from;
                const float px = 1.55f / 244f;
                return new Vector4(sign * (13f * depth + 1.8f * dx) * px + dx * from.x,
                    (17.5f + sign * dx * 4f) * px - dy * from.x + from.y, dx * d.x, -dy * d.x + d.y);
            }
            splatRenderer.PaintedFootA = Foot(0);
            splatRenderer.PaintedFootB = Foot(1);
        }
    }
}
