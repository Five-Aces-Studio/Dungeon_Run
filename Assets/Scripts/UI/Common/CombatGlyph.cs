using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Small native-resolution vector fallback; supplied card artwork takes priority.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatGlyph : MaskableGraphic
    {
        public enum Kind { Attack, Defence, Dodge, Heal, Piercing, Deck, Discard, Diamond }
        public Kind kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (kind)
            {
                case Kind.Defence: Shield(vh); break;
                case Kind.Dodge:
                    Polygon(vh, new Vector2(-.2f,-.1f), new Vector2(.08f,-.1f), new Vector2(.16f,.43f), new Vector2(-.22f,.43f));
                    Polygon(vh, new Vector2(-.24f,-.4f), new Vector2(.43f,-.4f), new Vector2(.42f,-.22f), new Vector2(.08f,-.1f), new Vector2(-.2f,-.1f)); break;
                case Kind.Heal:
                    Polygon(vh, new Vector2(0,-.43f), new Vector2(.43f,.07f), new Vector2(.4f,.31f), new Vector2(.19f,.43f),
                        new Vector2(0,.25f), new Vector2(-.19f,.43f), new Vector2(-.4f,.31f), new Vector2(-.43f,.07f)); break;
                case Kind.Deck: case Kind.Discard:
                    Box(vh,-.4f,-.3f,.32f,.3f); Box(vh,-.3f,-.4f,.42f,.23f); break;
                case Kind.Diamond: Polygon(vh,new Vector2(0,.5f),new Vector2(.4f,0),new Vector2(0,-.5f),new Vector2(-.4f,0)); break;
                default:
                    if (kind == Kind.Piercing) Shield(vh);
                    Box(vh,-.075f,-.17f,.075f,.28f);
                    Polygon(vh,new Vector2(-.075f,.28f),new Vector2(.075f,.28f),new Vector2(0,.5f));
                    Box(vh,-.26f,-.24f,.26f,-.14f); Box(vh,-.055f,-.45f,.055f,-.24f); break;
            }
        }
        private void Shield(VertexHelper vh) => Polygon(vh,new Vector2(0,-.47f),new Vector2(.34f,-.08f),new Vector2(.39f,.31f),
            new Vector2(0,.45f),new Vector2(-.39f,.31f),new Vector2(-.34f,-.08f));
        private void Box(VertexHelper vh,float x0,float y0,float x1,float y1) => Polygon(vh,
            new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1));
        private void Polygon(VertexHelper vh,params Vector2[] points)
        {
            var rect = GetPixelAdjustedRect();
            int start = vh.currentVertCount;
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            Add(vh,rect,center);
            foreach(var p in points) Add(vh,rect,p);
            for(int i=0;i<points.Length;i++) vh.AddTriangle(start,start+i+1,start+(i+1)%points.Length+1);
        }
        private void Add(VertexHelper vh,Rect rect,Vector2 p) => vh.AddVert(new Vector3(rect.center.x+p.x*rect.width,
            rect.center.y+p.y*rect.height),color,Vector2.zero);
    }
}
