using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    // Four corners of usable painted surfaces at the reference camera resolution.
    // Positions are independent of the translated string or display size.
    internal sealed class HubInscription : VisualElement
    {
        [System.Serializable]
        internal sealed class Sheet
        {
            public int size;
            public Glyph[] glyphs;
        }

        [System.Serializable]
        internal struct Glyph
        {
            public int code, x, y, width, height, left, top;
            public float advance;
        }

        private static readonly Color32 Ink = new(65, 34, 43, 255);
        private static readonly Color32 CoverInk = new(77, 39, 56, 255);
        private static readonly Color32 PaperRelief = new(255, 232, 176, 186);
        private readonly Dictionary<int, Glyph> glyphs;
        private readonly Texture2D atlas;
        private readonly Vector2[] corners;
        private readonly float pointSize;
        private readonly int sourceSize;
        private readonly bool leftAligned;
        private readonly bool folded;
        private readonly bool fieldBook;
        private int clothFrame;
        private string text = "";

        internal HubInscription(Texture2D atlas, Sheet sheet, Vector2[] corners, float pointSize, bool leftAligned = false,
            bool fieldBook = false, bool folded = false)
        {
            this.atlas = atlas;
            this.corners = corners;
            this.pointSize = pointSize;
            this.leftAligned = leftAligned;
            this.fieldBook = fieldBook;
            this.folded = folded;
            sourceSize = sheet.size;
            glyphs = new Dictionary<int, Glyph>(sheet.glyphs.Length);
            foreach (Glyph glyph in sheet.glyphs) glyphs.Add(glyph.code, glyph);
            pickingMode = PickingMode.Ignore;
            AddToClassList("hub-inscription");
            generateVisualContent += Draw;
        }

        internal void SetText(string value)
        {
            if (text == value) return;
            text = value;
            MarkDirtyRepaint();
        }

        internal void SetClothFrame(int value)
        {
            if (!folded || clothFrame == value) return;
            clothFrame = value;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (string.IsNullOrEmpty(text) || contentRect.width <= 0 || contentRect.height <= 0) return;
            string[] lines = text.Split('\n');
            var placed = new List<(Glyph glyph, Rect rect)>(text.Length);
            float widest = 0f;
            float top = float.PositiveInfinity, bottom = float.NegativeInfinity;
            float titleLineScale = 1f;
            if (fieldBook)
            {
                // Leave the compass on the top-right of the painted page visible in every locale.
                float titleAdvance = 0f;
                foreach (char code in lines[0])
                    if (glyphs.TryGetValue(code, out Glyph glyph)) titleAdvance += glyph.advance;
                titleLineScale = Mathf.Min(0.96f, 151f * sourceSize / (pointSize * Mathf.Max(1f, titleAdvance)));
            }
            float lineSpacing = sourceSize * (fieldBook ? 0.84f : 0.73f);
            float secondLineInset = fieldBook ? sourceSize * 0.29f : 0f;
            float[] lineWidths = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                float lineScale = i == 0 ? titleLineScale : 1f;
                if (i > 0) lineWidths[i] += secondLineInset;
                foreach (char code in lines[i])
                    if (glyphs.TryGetValue(code, out Glyph glyph)) lineWidths[i] += glyph.advance * lineScale;
                widest = Mathf.Max(widest, lineWidths[i]);
            }

            for (int index = 0; index < lines.Length; index++)
            {
                float lineScale = index == 0 ? titleLineScale : 1f;
                float advance = leftAligned ? 0f : (widest - lineWidths[index]) * 0.5f;
                if (index > 0) advance += secondLineInset;
                foreach (char code in lines[index])
                {
                    if (!glyphs.TryGetValue(code, out Glyph glyph)) continue;
                    float y = glyph.top * lineScale + index * lineSpacing;
                    if (glyph.width > 0 && glyph.height > 0)
                    {
                        placed.Add((glyph, new Rect(advance + glyph.left * lineScale, y, glyph.width * lineScale, glyph.height * lineScale)));
                        top = Mathf.Min(top, y);
                        bottom = Mathf.Max(bottom, y + glyph.height * lineScale);
                    }
                    advance += glyph.advance * lineScale;
                }
            }
            if (placed.Count == 0) return;

            float planeWidth = Vector2.Distance(corners[0], corners[1]);
            float planeHeight = ((corners[3] - corners[0]).magnitude + (corners[2] - corners[1]).magnitude) * 0.5f;
            float scale = Mathf.Min(pointSize / sourceSize, planeWidth / widest, planeHeight / (bottom - top));
            float originX = leftAligned ? 0f : (planeWidth - widest * scale) * 0.5f;
            float originY = (planeHeight - (bottom - top) * scale) * 0.5f - top * scale;
            int layers = fieldBook ? 1 : 2;
            var mesh = context.Allocate(placed.Count * 4 * layers, placed.Count * 6 * layers, atlas);
            ushort vertex = 0;
            foreach (var (glyph, rect) in placed)
            {
                float left = (originX + rect.xMin * scale) / planeWidth;
                float right = (originX + rect.xMax * scale) / planeWidth;
                float high = (originY + rect.yMin * scale) / planeHeight;
                float low = (originY + rect.yMax * scale) / planeHeight;
                Vector2 tl = Project(left, high);
                Vector2 tr = Project(right, high);
                Vector2 br = Project(right, low);
                Vector2 bl = Project(left, low);
                float u0 = (float)glyph.x / atlas.width, u1 = (float)(glyph.x + glyph.width) / atlas.width;
                float v0 = 1f - (float)glyph.y / atlas.height, v1 = 1f - (float)(glyph.y + glyph.height) / atlas.height;
                Vector2 slip = new(0f, 0.62f * contentRect.height / 720f);
                if (!fieldBook)
                    AddQuad(mesh, ref vertex, tl + slip, tr + slip, br + slip, bl + slip, u0, u1, v0, v1, PaperRelief);
                AddQuad(mesh, ref vertex, tl, tr, br, bl, u0, u1, v0, v1, fieldBook ? CoverInk : Ink);
            }
        }

        private Vector2 Project(float x, float y)
        {
            Vector2 spot = Vector2.LerpUnclamped(Vector2.LerpUnclamped(corners[0], corners[1], x),
                Vector2.LerpUnclamped(corners[3], corners[2], x), y);
            if (folded) spot.y += HubClothPatch.Fold(spot, clothFrame);
            return new Vector2(spot.x * contentRect.width / 1280f, spot.y * contentRect.height / 720f);
        }

        internal static void AddQuad(MeshWriteData mesh, ref ushort cursor, Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl,
            float u0, float u1, float v0, float v1, Color32 color)
        {
            mesh.SetNextVertex(MakeVertex(tl, u0, v0, color));
            mesh.SetNextVertex(MakeVertex(tr, u1, v0, color));
            mesh.SetNextVertex(MakeVertex(br, u1, v1, color));
            mesh.SetNextVertex(MakeVertex(bl, u0, v1, color));
            mesh.SetNextIndex(cursor);
            mesh.SetNextIndex((ushort)(cursor + 1));
            mesh.SetNextIndex((ushort)(cursor + 2));
            mesh.SetNextIndex((ushort)(cursor + 2));
            mesh.SetNextIndex((ushort)(cursor + 3));
            mesh.SetNextIndex(cursor);
            cursor += 4;
        }

        private static Vertex MakeVertex(Vector2 position, float u, float v, Color32 color)
        {
            return new Vertex { position = new Vector3(position.x, position.y, Vertex.nearZ), uv = new Vector2(u, v), tint = color };
        }
    }
}
