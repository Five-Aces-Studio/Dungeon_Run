using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-floor variants; never changes the kit's shared materials.</summary>
public sealed class DungeonMaterialScope : MonoBehaviour
{
    private readonly Dictionary<Material,Material> variants=new Dictionary<Material,Material>();
    public void Apply()
    {
        foreach(var renderer in GetComponentsInChildren<Renderer>())
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(source==null||!source.HasProperty("_AmbientStrength"))continue;
                if(!variants.TryGetValue(source,out var variant))
                {
                    variant=new Material(source){name=source.name+" (Guille floor)"};
                    variant.SetFloat("_AmbientStrength",.025f);
                    var bands=variant.GetVector("_BandLevels");bands.x=.002f;bands.y=.055f;
                    variant.SetVector("_BandLevels",bands);
                    if(variant.HasProperty("_RimStrength"))variant.SetFloat("_RimStrength",0);
                    variant.enableInstancing=true;variants.Add(source,variant);
                }
                materials[i]=variant;
            }
            renderer.sharedMaterials=materials;
        }
    }
    private void OnDestroy(){foreach(var material in variants.Values)if(material!=null)Destroy(material);}
}
