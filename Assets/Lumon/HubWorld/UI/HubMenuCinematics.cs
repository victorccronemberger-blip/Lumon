using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    // The parchment entrance and the egg seal have independent short timelines.
    internal sealed class HubMenuCinematics
    {
        private VisualElement paper;
        private VisualElement veil;
        private float enteredAt;

        internal void Open(VisualElement panel, VisualElement lighting, bool reducedMotion)
        {
            Reset();
            paper = panel;
            veil = lighting;
            enteredAt = Time.unscaledTime;
            if (reducedMotion) Reset();
            else Tick();
        }

        internal void Tick()
        {
            if (paper == null) return;
            float t = Mathf.Clamp01((Time.unscaledTime - enteredAt) / .30f);
            float eased = EaseOut(t);
            paper.style.opacity = Mathf.Lerp(.72f, 1f, eased);
            paper.style.scale = new Scale(new Vector3(Mathf.Lerp(.985f, 1f, eased), Mathf.Lerp(.91f, 1f, eased), 1f));
            paper.style.translate = new Translate(0f, 15f * (1f - eased));
            veil.style.opacity = Mathf.Lerp(.62f, 1f, eased);
            if (t >= 1f) Reset();
        }

        internal void Reset()
        {
            if (paper == null) return;
            paper.style.opacity = 1f;
            paper.style.scale = new Scale(Vector3.one);
            paper.style.translate = new Translate(0f, 0f);
            veil.style.opacity = 1f;
            paper = null;
            veil = null;
        }

        internal static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
    }
}
