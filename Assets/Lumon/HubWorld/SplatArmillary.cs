using System;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace Lumon.HubWorld
{
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public sealed class SplatArmillary : MonoBehaviour
    {
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private Vector3 middleAxis;
        [SerializeField] private Vector3 horizonAxis;
        [SerializeField] private float middleSweep;
        [SerializeField] private float horizonSweep;
        [SerializeField] private float period;

        private readonly Vector4[] rotations = new Vector4[GaussianSplatRenderer.MaxSkinBones];
        private readonly Vector4[] translations = new Vector4[GaussianSplatRenderer.MaxSkinBones];
        private GaussianSplatRenderer renderer;
        private GraphicsBuffer weights;
        private bool bound;
        private float phase = -1f;
        private float? reviewPhase;
        private float middleAngle;
        private float horizonAngle;

        public void Configure(HubSplatWorld owner, Vector3 middleHinge, Vector3 innerHinge,
            float middleDegrees, float innerDegrees, float cycleSeconds)
        {
            world = owner;
            middleAxis = middleHinge.normalized;
            horizonAxis = innerHinge.normalized;
            middleSweep = middleDegrees;
            horizonSweep = innerDegrees;
            period = cycleSeconds;
        }

        private void OnEnable()
        {
            renderer = GetComponent<GaussianSplatRenderer>();
            TextAsset asset = Resources.Load<TextAsset>("Lumon/ArmillaryRig");
            if (asset == null)
            {
                Debug.LogError("Vínculos da esfera armilar ausentes.", this);
                return;
            }
            byte[] bytes = asset.bytes;
            if (bytes.Length < 8 || bytes[0] != 'L' || bytes[1] != 'A' || bytes[2] != 'R' || bytes[3] != '1')
            {
                Debug.LogError("Vínculos da esfera armilar inválidos.", this);
                return;
            }
            int count = BitConverter.ToInt32(bytes, 4);
            if (count <= 0 || bytes.Length != 8L + count * sizeof(uint))
            {
                Debug.LogError("Vínculos da esfera armilar incompletos.", this);
                return;
            }
            uint[] binding = new uint[count];
            Buffer.BlockCopy(bytes, 8, binding, 0, count * sizeof(uint));
            foreach (uint entry in binding)
            {
                if (entry == 0x0101 || entry == 0x0202) continue;
                Debug.LogError("Eixo inválido nos vínculos da esfera armilar.", this);
                return;
            }
            weights = new GraphicsBuffer(GraphicsBuffer.Target.Raw, count, sizeof(uint)) { name = "Armillary rigid bindings" };
            try { weights.SetData(binding); }
            catch { weights.Dispose(); weights = null; throw; }
            phase = -1f;
            ApplyPhase(0f);
        }

        private void OnDisable()
        {
            if (bound && renderer != null) renderer.SetSkinning(null, null, null);
            weights?.Dispose();
            weights = null;
            bound = false;
        }

        private void LateUpdate()
        {
            if (weights == null || !renderer.HasValidRenderSetup) return;
            if (!bound)
            {
                if (weights.count != renderer.splatCount)
                {
                    Debug.LogError("A esfera armilar e seus vínculos têm contagens diferentes.", this);
                    weights.Dispose();
                    weights = null;
                    return;
                }
                renderer.SetSkinning(weights, rotations, translations);
                bound = true;
            }

            float next = reviewPhase ?? Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(0.01f, period), 1f);
            ApplyPhase(world != null && world.ReducedMotion ? 0f : next);
        }

        internal void SetReviewPhase(float? fixedPhase) => reviewPhase = fixedPhase.HasValue ? Mathf.Repeat(fixedPhase.Value, 1f) : null;

        private void ApplyPhase(float next)
        {
            if (phase == next) return;
            phase = next;
            float cycle = next * 2f * Mathf.PI;
            middleAngle = middleSweep * Mathf.Sin(cycle);
            horizonAngle = horizonSweep * Mathf.Sin(2f * cycle);
            Quaternion middle = Quaternion.AngleAxis(middleAngle, middleAxis);
            Quaternion horizon = middle * Quaternion.AngleAxis(horizonAngle, horizonAxis);
            rotations[1] = new Vector4(middle.x, middle.y, middle.z, middle.w);
            rotations[2] = new Vector4(horizon.x, horizon.y, horizon.z, horizon.w);
        }

        internal string CaptureMotion() => FormattableString.Invariant(
            $"armillaryPhase={phase:F3} middleAngle={middleAngle:F2} horizonAngle={horizonAngle:F2} armillaryBound={bound}");
    }
}
