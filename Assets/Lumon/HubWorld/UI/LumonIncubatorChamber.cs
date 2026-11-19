using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    public sealed class LumonIncubatorChamber : VisualElement
    {
        private static readonly Color Ink = new(0.079f, 0.115f, 0.155f);
        private static readonly Color Deep = new(0.14f, 0.20f, 0.24f);
        private static readonly Color Violet = new(0.25f, 0.34f, 0.36f);
        private static readonly Color Paper = new(0.79f, 0.68f, 0.50f);
        private static readonly Color Light = new(0.93f, 0.89f, 0.78f);
        private static readonly Color Gold = new(0.62f, 0.47f, 0.32f);
        private static readonly Color Mint = new(0.43f, 0.69f, 0.67f);
        private static readonly Color Teal = new(0.17f, 0.31f, 0.36f);

        private readonly bool reducedMotion;
        private IVisualElementScheduledItem animation;
        private int phase;

        public bool Ready { get; private set; }

        public LumonIncubatorChamber(bool ready, bool reduceMotion)
        {
            Ready = ready;
            reducedMotion = reduceMotion;
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (!reducedMotion) animation = schedule.Execute(Tick).Every(120);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause());
        }

        public void SetReady(bool value)
        {
            if (Ready == value) return;
            Ready = value;
            MarkDirtyRepaint();
        }

        private void Tick()
        {
            phase = (phase + 1) % 120;
            MarkDirtyRepaint();
        }

        private static void Rect(Painter2D p, float x, float y, float w, float h, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        private static void Bolt(Painter2D p, float x, float y)
        {
            Rect(p, x, y, 7, 7, Ink);
            Rect(p, x + 2, y + 2, 3, 3, Gold);
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 250 || h < 250) return;
            Painter2D p = context.painter2D;
            Color signal = Ready ? Gold : Mint;

            // Stepped outer casting: a physical chamber, not a generic UI card.
            Rect(p, 12, 24, w - 24, h - 38, Ink);
            Rect(p, 20, 16, w - 40, h - 38, Ink);
            Rect(p, 28, 8, w - 56, h - 38, Ink);
            Rect(p, 20, 26, w - 40, h - 54, Violet);
            Rect(p, 30, 18, w - 60, h - 54, Deep);

            // Recessed glass and deliberately stable ordered dithering.
            Rect(p, 52, 66, w - 104, h - 142, Ink);
            Rect(p, 57, 71, w - 114, h - 152, Teal);
            Rect(p, 61, 75, w - 122, h - 160, new Color(0.22f, 0.39f, 0.41f));
            for (int y = 0; y < 15; y++)
                for (int x = 0; x < 17; x++)
                    if ((x + y) % 4 == 0)
                        Rect(p, 65 + x * 11, 79 + y * 9, 3, 3, new Color(0.28f, 0.43f, 0.44f));
            Rect(p, 65, 79, 7, h - 178, new Color(0.8f, 0.91f, 0.76f, 0.55f));
            Rect(p, 77, 79, 3, h - 178, new Color(0.8f, 0.91f, 0.76f, 0.35f));

            // Crown, rails and locking jaws.
            Rect(p, 24, 28, w - 48, 36, Gold);
            Rect(p, 24, 60, w - 48, 7, Ink);
            Rect(p, 39, 38, w - 78, 8, Paper);
            Rect(p, 31, 65, 21, h - 137, Ink);
            Rect(p, w - 52, 65, 21, h - 137, Ink);
            Rect(p, 36, 74, 8, h - 157, Violet);
            Rect(p, w - 44, 74, 8, h - 157, Violet);
            for (int i = 0; i < 4; i++)
            {
                float y = 88 + i * 38;
                Rect(p, 26, y, 31, 13, Deep);
                Rect(p, w - 57, y, 31, 13, Deep);
                Rect(p, 49, y + 3, 13, 7, Gold);
                Rect(p, w - 62, y + 3, 13, 7, Gold);
            }

            // Heat exchanger, service vents and fasteners.
            for (int i = 0; i < 5; i++)
            {
                Rect(p, 26, h - 93 + i * 6, 25, 3, Ink);
                Rect(p, w - 51, h - 93 + i * 6, 25, 3, Ink);
            }
            Bolt(p, 32, 34);
            Bolt(p, w - 39, 34);
            Bolt(p, 32, h - 49);
            Bolt(p, w - 39, h - 49);

            // Egg cradle and magnetic suspension rings.
            float cx = w * 0.5f;
            Rect(p, cx - 78, h - 82, 156, 15, Ink);
            Rect(p, cx - 66, h - 88, 132, 8, Gold);
            Rect(p, cx - 49, h - 96, 98, 9, Deep);
            Rect(p, cx - 28, h - 101, 56, 6, signal);
            Rect(p, 20, h - 35, w - 40, 18, Ink);
            Rect(p, 36, h - 35, w - 72, 8, Violet);
            Rect(p, 28, h - 17, 50, 8, Ink);
            Rect(p, w - 78, h - 17, 50, 8, Ink);

            // State LEDs and small in-chamber motes. No bloom or elemental symbols.
            for (int i = 0; i < 3; i++)
            {
                bool lit = Ready || ((phase / 8 + i) % 3 != 0);
                Rect(p, w - 90 + i * 14, 41, 8, 8, lit ? signal : Violet);
            }
            for (int i = 0; i < 6; i++)
            {
                float travel = reducedMotion ? i * 17 : (phase * 1.4f + i * 29) % 118;
                float x = cx - 72 + ((i * 47) % 137);
                float y = h - 116 - travel;
                Rect(p, x, y, i % 2 == 0 ? 4 : 3, i % 2 == 0 ? 4 : 3, i % 3 == 0 ? Light : signal);
            }
            if (Ready)
            {
                int pulse = reducedMotion ? 0 : (phase / 6) % 2;
                for (int i = 0; i < 4; i++)
                {
                    float x = i % 2 == 0 ? 67 : w - 75;
                    float y = i < 2 ? 94 + pulse * 4 : h - 131 - pulse * 4;
                    Rect(p, x - 5, y, 14, 4, Gold);
                    Rect(p, x, y - 5, 4, 14, Gold);
                }
            }
        }
    }
}
