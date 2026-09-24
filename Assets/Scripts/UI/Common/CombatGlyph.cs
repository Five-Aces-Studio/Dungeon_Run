using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Small native-resolution vector fallback; supplied card artwork takes priority.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatGlyph : MaskableGraphic
    {
        public enum Kind { Attack, Defence, Dodge, Heal, Piercing, Deck, Discard, Diamond, Miss, Charge }
        public Kind kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (kind)
            {
                case Kind.Defence: Shield(vh); break;
                case Kind.Miss: Box(vh, -.35f, -.045f, .35f, .045f); break;
                case Kind.Charge:
                    Box(vh, -.35f, .37f, .35f, .44f); Box(vh, -.35f, -.44f, .35f, -.37f);
                    Polygon(vh, new Vector2(-.28f,.32f), new Vector2(.28f,.32f), new Vector2(0,0));
                    Polygon(vh, new Vector2(-.28f,-.32f), new Vector2(.28f,-.32f), new Vector2(0,0)); break;
                case Kind.Dodge:
                    Polygon(vh, new Vector2(-.2f,-.1f), new Vector2(.08f,-.1f), new Vector2(.16f,.43f), new Vector2(-.22f,.43f));
                    Polygon(vh, new Vector2(-.24f,-.4f), new Vector2(.43f,-.4f), new Vector2(.42f,-.22f), new Vector2(.08f,-.1f), new Vector2(-.2f,-.1f)); break;
                case Kind.Heal:
                    Polygon(vh, new Vector2(0,-.43f), new Vector2(.43f,.07f), new Vector2(.4f,.31f), new Vector2(.19f,.43f),
                        new Vector2(0,.25f), new Vector2(-.19f,.43f), new Vector2(-.4f,.31f), new Vector2(-.43f,.07f)); break;
                case Kind.Deck:
                    Box(vh,-.38f,-.27f,-.31f,.39f); Box(vh,-.28f,-.35f,-.21f,.31f);
                    Box(vh,-.16f,-.42f,.36f,.23f);
                    Polygon(vh,new Vector2(.1f,.4f),new Vector2(.24f,.27f),new Vector2(.1f,.14f),new Vector2(-.04f,.27f)); break;
                case Kind.Discard:
                    Polygon(vh,new Vector2(-.3f,.08f),new Vector2(-.3f,.35f),new Vector2(0,.46f),new Vector2(.3f,.35f),new Vector2(.3f,.08f),new Vector2(.15f,-.08f),new Vector2(-.15f,-.08f));
                    Box(vh,-.17f,-.22f,-.07f,-.08f); Box(vh,.07f,-.22f,.17f,-.08f);
                    Polygon(vh,new Vector2(-.38f,-.3f),new Vector2(-.32f,-.4f),new Vector2(.38f,-.15f),new Vector2(.32f,-.05f));
                    Polygon(vh,new Vector2(.38f,-.3f),new Vector2(.32f,-.4f),new Vector2(-.38f,-.15f),new Vector2(-.32f,-.05f)); break;
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
