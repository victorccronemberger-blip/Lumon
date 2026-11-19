using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    internal sealed class ObservatoryChart : VisualElement
    {
        private static readonly Vector2[] Lyra = { new(.22f,.24f), new(.42f,.40f), new(.64f,.34f), new(.75f,.67f), new(.53f,.74f) };
        private static readonly Vector2[] Cygnus = { new(.26f,.20f), new(.48f,.48f), new(.72f,.81f), new(.28f,.71f), new(.67f,.27f), new(.83f,.18f) };

        internal ObservatoryChart(bool west)
        {
            style.height = 158;
            style.flexShrink = 0;
            style.backgroundColor = (Color)new Color32(17,31,48,255);
            style.marginBottom = 10;
            generateVisualContent += ctx =>
            {
                var pen = ctx.painter2D;
                var size = contentRect.size;
                var stars = west ? Lyra : Cygnus;
                var edges = west ? new[] { 0,1,1,2,2,3,3,4,4,1 } : new[] { 0,1,1,2,3,1,1,4,4,5 };
                void Star(Vector2 p,float radius,Color tint)
                {
                    pen.fillColor = tint; pen.BeginPath(); pen.MoveTo(p + Vector2.up*radius);
                    pen.LineTo(p + Vector2.right*radius); pen.LineTo(p + Vector2.down*radius);
                    pen.LineTo(p + Vector2.left*radius); pen.ClosePath(); pen.Fill();
                }
                for (int i = 1; i < 47; i++)
                    Star(Vector2.Scale(new Vector2(Mathf.Repeat(i*.618f,1),Mathf.Repeat(i*i*.213f,1)), size),
                        i%5 == 0 ? 1.6f : 1, new Color32(116,139,161,255));
                pen.lineWidth = 1.5f; pen.strokeColor = new Color32(107,174,175,255); pen.BeginPath();
                for (int j=0; j<edges.Length; j+=2)
                {
                    pen.MoveTo(Vector2.Scale(stars[edges[j]],size));
                    pen.LineTo(Vector2.Scale(stars[edges[j+1]],size));
                }
                pen.Stroke();
                for(int i=0; i<stars.Length; i++) Star(Vector2.Scale(stars[i],size),i==0 ? 7f : 4f,new Color32(247,219,163,255));
            };
        }
    }
}
