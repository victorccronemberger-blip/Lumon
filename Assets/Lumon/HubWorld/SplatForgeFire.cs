using System;
using GaussianSplatting.Runtime;
using UnityEngine;

namespace Lumon.HubWorld
{
    public sealed class SplatForgeFire : MonoBehaviour
    {
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private GaussianSplatRenderer scenery;
        [SerializeField] private GaussianSplatRenderer masonry;
        [SerializeField] private GaussianSplatRenderer[] tongues;
        [SerializeField] private float[] offsets;
        [SerializeField] private int[] frequencies;
        [SerializeField] private float period;

        private float phase;
        private float heat;
        private float? reviewPhase;
        internal bool GlowEnabled { get; set; } = true;

        public void Configure(HubSplatWorld owner, GaussianSplatRenderer scene, GaussianSplatRenderer forge, GaussianSplatRenderer[] flames,
            float[] phaseOffsets, int[] cycles, float seconds)
        {
            world = owner;
            scenery = scene;
            masonry = forge;
            tongues = flames;
            offsets = phaseOffsets;
            frequencies = cycles;
            period = seconds;
        }

        private void LateUpdate()
        {
            if (scenery == null || tongues == null || period <= 0f) return;
            float next = reviewPhase ?? Mathf.Repeat(phase + Time.deltaTime / period, 1f);
            phase = world != null && world.ReducedMotion ? 0f : next;
            float cycle = phase * (2f * Mathf.PI);
            heat = 0.50f + 0.31f * Mathf.Sin(3f * cycle) + 0.19f * Mathf.Sin(7f * cycle);

            for (int i = 0; i < tongues.Length; i++)
            {
                float sway = Mathf.Sin(cycle * frequencies[i] + offsets[i]);
                float height = Mathf.Cos(cycle * frequencies[i] + offsets[i]);
                Transform flame = tongues[i].transform;
                flame.localRotation = Quaternion.Euler(0f, 0f, 8f * sway);
                flame.localScale = new Vector3(1f - 0.06f * height, 1f + 0.18f * height, 1f);
                tongues[i].m_OpacityScale = 0.91f + 0.13f * heat;
            }

            float strength = Mathf.Lerp(0.05f, 0.26f, heat);
            Vector3 warm = new Vector3(1f, 0.53f, 0.14f) * strength;
            Vector3 source = transform.TransformPoint(new Vector3(0f, 0.681f, 0.158f));
            float range = GlowEnabled ? 0.88f : 0f;
            scenery.SetLocalLight(source, range, transform.forward, warm);
            if (masonry != null) masonry.SetLocalLight(source, range, transform.forward, warm);
        }

        private void OnDisable()
        {
            if (scenery != null) scenery.SetLocalLight(Vector3.zero, 0f, Vector3.zero, Vector3.zero);
            if (masonry != null) masonry.SetLocalLight(Vector3.zero, 0f, Vector3.zero, Vector3.zero);
        }

        internal void SetReviewPhase(float? fixedPhase) => reviewPhase = fixedPhase.HasValue ? Mathf.Repeat(fixedPhase.Value, 1f) : null;

        internal string CaptureMotion() => FormattableString.Invariant($"forgePhase={phase:F3} forgeHeat={heat:F3} forgeGlow={GlowEnabled}");
    }
}
