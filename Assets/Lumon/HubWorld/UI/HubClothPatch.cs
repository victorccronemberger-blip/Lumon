using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    internal sealed class HubClothPatch : VisualElement
    {
        internal const int Frames = 12;
        private const float Left = 174;
        private const float Top = 307;
        private const float Width = 95;
        private const float Height = 77;
        private readonly Texture2D fabric;
        private int frame;

        internal HubClothPatch(Texture2D fabric)
        {
            this.fabric = fabric;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hub-inscription");
            generateVisualContent += Draw;
        }

        internal void SetFrame(int value)
        {
            if (frame == value) return;
            frame = value;
            MarkDirtyRepaint();
        }

        internal static float Fold(Vector2 point, int frame)
        {
            float across = (point.x - 176f) / 83f;
            float bend = Mathf.Sin(Mathf.PI * Mathf.Clamp01(across));
            float depth = Mathf.Clamp01((point.y - 317f) / 44f);
            float phase = 2f * Mathf.PI * frame / Frames;
            float fold = 2f * Mathf.PI * (across * 1.48f + (point.y - 321f) * 0.010f) + 0.38f * Mathf.Sin(phase);
            return (Mathf.Sin(fold) * 2.6f + Mathf.Sin(fold * 2f) * 0.55f) * bend * depth;
        }

        private void Draw(MeshGenerationContext context)
        {
            float x = contentRect.width / 1280f, y = contentRect.height / 720f;
            if (x <= 0f || y <= 0f) return;
            var mesh = context.Allocate(4, 6, fabric);
            ushort cursor = 0;
            float u = (frame * (Width + 2f) + 1f) / fabric.width;
            HubInscription.AddQuad(mesh, ref cursor, new(Left * x, Top * y), new((Left + Width) * x, Top * y),
                new((Left + Width) * x, (Top + Height) * y), new(Left * x, (Top + Height) * y),
                u, u + Width / fabric.width, 1f - 1f / fabric.height, 1f - (Height + 1f) / fabric.height, Color.white);
        }
    }
}
