using System;
using System.Globalization;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace Lumon.HubWorld
{
    internal sealed class GuardianGait : IDisposable
    {
        private const int BoneCount = 11;
        private const float Stance = 0.58f;
        private const float Travel = 0.61f;
        private const float CycleDistance = Travel / Stance;
        private const float FootLift = 0.105f;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Vector3 RestHip = new(0.080f, 0.548f, 0.005f);
        private static readonly Vector3 RestKnee = new(0.106f, 0.257f, 0.018f);
        private static readonly Vector3 RestAnkle = new(0.115f, 0.072f, 0.046f);
        private static readonly float Thigh = SectionLength(RestKnee - RestHip);
        private static readonly float Shin = SectionLength(RestAnkle - RestKnee);

        private readonly GaussianSplatRenderer renderer;
        private readonly GraphicsBuffer weights;
        private readonly Vector4[] rotations = new Vector4[GaussianSplatRenderer.MaxSkinBones];
        private readonly Vector4[] translations = new Vector4[GaussianSplatRenderer.MaxSkinBones];
        private Vector3 leftSole;
        private Vector3 rightSole;
        private float phase;
        private float blend;
        private bool plantedLeft;
        private bool plantedRight;
        private bool bound;
        private bool rejected;

        private GuardianGait(GaussianSplatRenderer renderer, uint[] binding)
        {
            this.renderer = renderer;
            weights = new GraphicsBuffer(GraphicsBuffer.Target.Raw, binding.Length, sizeof(uint)) { name = "Guardiã skin weights" };
            try { weights.SetData(binding); }
            catch { weights.Dispose(); throw; }
            SetRest();
        }

        public static GuardianGait Create(Transform artwork)
        {
            if (artwork == null || !artwork.TryGetComponent(out GaussianSplatRenderer renderer) ||
                renderer.m_SourcePath != "Lumon/Splats/guardian.ply")
                return null;
            TextAsset binding = Resources.Load<TextAsset>("Lumon/GuardianRig");
            if (binding == null) return null;
            byte[] bytes = binding.bytes;
            if (bytes.Length < 8 || bytes[0] != 'L' || bytes[1] != 'G' || bytes[2] != 'R' || bytes[3] != '1')
                throw new InvalidOperationException("Assinatura inválida nos pesos da Guardiã.");

            int count = BitConverter.ToInt32(bytes, 4);
            if (count <= 0 || bytes.Length != 8L + count * sizeof(uint))
                throw new InvalidOperationException("Pesos da Guardiã incompletos.");
            uint[] packed = new uint[count];
            Buffer.BlockCopy(bytes, 8, packed, 0, packed.Length * sizeof(uint));
            foreach (uint entry in packed)
            {
                if ((entry & 255) >= BoneCount || ((entry >> 8) & 255) >= BoneCount)
                    throw new InvalidOperationException("Índice de osso inválido nos pesos da Guardiã.");
            }
            return new GuardianGait(renderer, packed);
        }

        public void Advance(float forwardDistance, float deltaTime, bool reduceMotion)
        {
            if (rejected || renderer == null) return;
            if (!bound && renderer.HasValidRenderSetup)
            {
                if (weights.count != renderer.splatCount)
                {
                    Debug.LogError("O PLY e os pesos da Guardiã têm contagens diferentes.", renderer);
                    rejected = true;
                    return;
                }
                renderer.SetSkinning(weights, rotations, translations);
                bound = true;
            }

            if (reduceMotion)
            {
                if (blend > 0f) SetRest();
                return;
            }

            bool stepping = forwardDistance > 0.0001f;
            if (!stepping && blend == 0f) return;
            blend = Mathf.MoveTowards(blend, stepping ? 1f : 0f, deltaTime * (stepping ? 11f : 8f));
            if (blend == 0f)
            {
                SetRest();
                return;
            }

            phase = Mathf.Repeat(phase + forwardDistance / CycleDistance, 1f);
            Vector2 left = Foot(phase, out plantedLeft);
            Vector2 right = Foot(Mathf.Repeat(phase + 0.5f, 1f), out plantedRight);
            float restingHeight = RestHip.y - 0.026f - 0.011f * Mathf.Cos(4f * Mathf.PI * phase);
            float hipHeight = Mathf.Min(restingHeight, Mathf.Min(ReachHeight(left), ReachHeight(right)));

            Vector3 torso = new(0f, hipHeight - RestHip.y, 0f);
            SetBone(0, Quaternion.Euler(2.5f, 2.5f * Mathf.Sin(phase * 2f * Mathf.PI), 0f), new Vector3(0f, RestHip.y, 0f),
                new Vector3(0f, hipHeight, 0f));
            leftSole = SolveLeg(1, -1f, left, torso);
            rightSole = SolveLeg(5, 1f, right, torso);
            SetBone(9, Quaternion.identity, Vector3.zero, new Vector3(0f, 0f, left.y));
            SetBone(10, Quaternion.identity, Vector3.zero, new Vector3(0f, 0f, right.y));
        }

        private static Vector2 Foot(float phase, out bool planted)
        {
            planted = phase < Stance;
            if (planted) return new Vector2(0f, Travel * 0.5f - phase * CycleDistance);

            float t = (phase - Stance) / (1f - Stance);
            float ease = t * t * (3f - 2f * t);
            float tangent = -CycleDistance * (1f - Stance) * (2f * t * t * t - 3f * t * t + t);
            float lift = Mathf.Sin(Mathf.PI * t);
            return new Vector2(FootLift * lift * lift, Mathf.Lerp(-Travel * 0.5f, Travel * 0.5f, ease) + tangent);
        }

        private static float ReachHeight(Vector2 foot)
        {
            float z = RestAnkle.z + foot.y - RestHip.z;
            float reach = Thigh + Shin - 0.010f;
            return RestAnkle.y + foot.x + Mathf.Sqrt(Mathf.Max(0f, reach * reach - z * z));
        }

        private Vector3 SolveLeg(int bone, float side, Vector2 offset, Vector3 torso)
        {
            Vector3 hipRest = WithSide(RestHip, side);
            Vector3 kneeRest = WithSide(RestKnee, side);
            Vector3 ankleRest = WithSide(RestAnkle, side);
            Vector3 hip = hipRest + torso;
            Vector3 ankle = ankleRest + new Vector3(0f, offset.x, offset.y);
            Vector2 delta = new(ankle.y - hip.y, ankle.z - hip.z);
            float distance = delta.magnitude;
            Vector2 axis = delta / distance;
            float along = (Thigh * Thigh - Shin * Shin + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, Thigh * Thigh - along * along));
            Vector2 joint = axis * along + new Vector2(axis.y, -axis.x) * bend;
            Vector3 knee = new(kneeRest.x, hip.y + joint.x, hip.z + joint.y);
            SetBone(bone, Align(kneeRest - hipRest, knee - hip), hipRest, hip);
            SetBone(bone + 1, Align(ankleRest - kneeRest, ankle - knee), kneeRest, knee);
            SetBone(bone + 2, Quaternion.identity, ankleRest, ankle);
            Vector3 shoulder = new(side * 0.156f, 0.859f, 0.002f);
            SetBone(bone + 3, Quaternion.Euler(offset.y * 78f, 0f, 0f), shoulder, shoulder + torso);
            return ankleRest - Vector3.up * RestAnkle.y + Vector3.Lerp(Vector3.zero, ankle - ankleRest, blend);
        }

        private static Quaternion Align(Vector3 rest, Vector3 posed)
        {
            float angle = (Mathf.Atan2(posed.z, posed.y) - Mathf.Atan2(rest.z, rest.y)) * Mathf.Rad2Deg;
            return Quaternion.Euler(angle, 0f, 0f);
        }

        private static float SectionLength(Vector3 vector) => new Vector2(vector.y, vector.z).magnitude;

        private static Vector3 WithSide(Vector3 vector, float side) => new(vector.x * side, vector.y, vector.z);

        private void SetBone(int index, Quaternion rotation, Vector3 rest, Vector3 posed)
        {
            Quaternion blended = Quaternion.Slerp(Quaternion.identity, rotation, blend);
            rotations[index] = new Vector4(blended.x, blended.y, blended.z, blended.w);
            translations[index] = Vector3.Lerp(rest, posed, blend) - blended * rest;
        }

        private void SetRest()
        {
            phase = 0f;
            blend = 0f;
            plantedLeft = plantedRight = true;
            leftSole = WithSide(RestAnkle, -1f) - Vector3.up * RestAnkle.y;
            rightSole = RestAnkle - Vector3.up * RestAnkle.y;
            for (int i = 0; i < rotations.Length; i++)
            {
                rotations[i] = new Vector4(0f, 0f, 0f, 1f);
                translations[i] = Vector4.zero;
            }
        }

        public string CapturePose(Transform artwork)
        {
            if (!bound) return "gait=unbound";
            string contact = blend < 0.999f ? "-" : plantedLeft && plantedRight ? "LR" : plantedLeft ? "L" : "R";
            return string.Format(Invariant, "gait={0:F3} phase={1:F3} contact={2} left={3} right={4}", blend, phase, contact,
                artwork.TransformPoint(leftSole).ToString("F4", Invariant), artwork.TransformPoint(rightSole).ToString("F4", Invariant));
        }

        public void Dispose()
        {
            if (bound && renderer != null) renderer.SetSkinning(null, null, null);
            weights.Dispose();
        }
    }
}

