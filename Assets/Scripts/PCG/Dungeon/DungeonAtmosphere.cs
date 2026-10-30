using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Local, reversible presentation. Does not modify shared profiles or global render settings.</summary>
public sealed class DungeonAtmosphere : MonoBehaviour
{
    private sealed class Torch { public GameObject root, flame; public Light light; public bool lit; public float phase; }
    private readonly List<Torch> torches=new List<Torch>();
    private Transform avatar;
    private GameObject playerTorch;
    private Light playerLight;
    private ParticleSystem mist;
    private Material mistMaterial;
    private Material flameMaterial;
    private Texture2D mistTexture;
    private float emissionTimer;
    private DungeonFloorData floor;
    private System.Random random;

    public void Initialize(DungeonFloorData data,Transform player,GameObject support,bool enabledAtmosphere)
    {
        if(!enabledAtmosphere){enabled=false;return;}
        floor=data;avatar=player;random=new System.Random(unchecked(data.seed^0x71A37));
        playerTorch=new GameObject("Player torch");playerTorch.transform.SetParent(player,false);
        playerTorch.transform.localPosition=new Vector3(.25f,1.35f,.2f);
        playerLight=playerTorch.AddComponent<Light>();playerLight.type=LightType.Point;
        playerLight.color=new Color(1,.65f,.3f);playerLight.intensity=12;playerLight.range=9;
        playerLight.shadows=LightShadows.Soft;
        // URP exposes this serialized tier read-only; use the pipeline's low-resolution tier.
        JsonUtility.FromJsonOverwrite("{\"m_AdditionalLightsShadowResolutionTier\":0}", playerLight.gameObject.AddComponent<UniversalAdditionalLightData>());
        var flame=playerTorch.AddComponent<DungeonRunFlameLight>();flame.targetLight=playerLight;
        flame.baseIntensity=12;flame.baseRange=9;flame.poolIntensity=0;flame.positionJitter=0;
        flameMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));flameMaterial.color=new Color(2f,1f,.2f);
        CreateFlame(playerTorch.transform,playerTorch.transform.position);
        var coordinates=new HashSet<Vector2Int>();foreach(var c in floor.cells)coordinates.Add(c.coordinate);
        foreach(var room in floor.rooms)
        {
            int placed=0;
            foreach(var cell in floor.cells)
            {
                if(cell.roomId!=room.id||placed>=2)continue;
                foreach(var d in DungeonPopulation.Directions)
                {
                    if(coordinates.Contains(cell.coordinate+d))continue;
                    var root=GameObject.CreatePrimitive(PrimitiveType.Cylinder);root.name="Wall torch (unlit)";
                    root.transform.SetParent(transform,false);
                    root.transform.position=floor.CellCenter(cell.coordinate)+new Vector3(d.x,0,d.y)*floor.cellSize*.43f+Vector3.up*1.3f;
                    root.transform.localScale=new Vector3(.09f,.22f,.09f);
                    var collider=root.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                    var lamp=new GameObject("Flame light");lamp.transform.SetParent(root.transform,false);
                    lamp.transform.position=root.transform.position+Vector3.up*.25f;
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=6;light.intensity=0;
                    light.color=new Color(1,.55f,.24f);light.enabled=false;
                    light.shadows=LightShadows.Hard;
                    JsonUtility.FromJsonOverwrite("{\"m_AdditionalLightsShadowResolutionTier\":0}", light.gameObject.AddComponent<UniversalAdditionalLightData>());
                    var visualFlame=CreateFlame(transform,light.transform.position);visualFlame.SetActive(false);
                    torches.Add(new Torch{root=root,flame=visualFlame,light=light,phase=(float)random.NextDouble()*6});
                    placed++;break;
                }
            }
        }
        var fog=new GameObject("Ambient mist");fog.transform.SetParent(transform,false);mist=fog.AddComponent<ParticleSystem>();
        var main=mist.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=4;main.startSpeed=0;main.startSize=.75f;main.startColor=new Color(.5f,.58f,.65f,.055f);main.maxParticles=120;
        var emission=mist.emission;emission.enabled=false;var shape=mist.shape;shape.enabled=false;
        mistMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mistTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        mistTexture.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
            float alpha=Mathf.Pow(Mathf.Clamp01(1-radius),2);
            mistTexture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        mistTexture.Apply();mistMaterial.SetTexture("_BaseMap",mistTexture);
        mistMaterial.SetFloat("_Surface",1);mistMaterial.SetFloat("_Blend",0);mistMaterial.SetFloat("_SrcBlend",5);mistMaterial.SetFloat("_DstBlend",10);
        mistMaterial.SetFloat("_ZWrite",0);mistMaterial.renderQueue=3000;mistMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mist.GetComponent<ParticleSystemRenderer>().sharedMaterial=mistMaterial;mist.Play();
    }
    private void Update()
    {
        if(avatar==null)return;
        // At most four nearby wall lights are active; ignition state remains persistent for this floor.
        torches.Sort((a,b)=>(a.root.transform.position-avatar.position).sqrMagnitude.CompareTo((b.root.transform.position-avatar.position).sqrMagnitude));
        int active=0;
        foreach(var torch in torches)
        {
            Vector3 from=avatar.position+Vector3.up*1.5f;
            Vector3 delta=torch.light.transform.position-from;
            if(!torch.lit&&delta.sqrMagnitude<16)
            {
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(avatar)){blocked=true;break;}
                if(!blocked){torch.lit=true;torch.root.name="Wall torch (lit)";torch.flame.SetActive(true);}
            }
            torch.light.enabled=torch.lit&&active<4&&delta.sqrMagnitude<100;
            if(torch.light.enabled){active++;torch.light.intensity=1.8f+Mathf.Sin(Time.time*7+torch.phase)*.15f;}
        }
        emissionTimer+=Time.deltaTime;
        if(emissionTimer<.18f)return;emissionTimer=0;
        for(int i=0;i<6;i++)
        {
            var cell=floor.cells[random.Next(floor.cells.Count)];
            Vector3 point=floor.CellCenter(cell.coordinate)+Vector3.up*.3f;
            if((point-avatar.position).sqrMagnitude>81)continue;
            mist.Emit(new ParticleSystem.EmitParams{position=point,startSize=floor.cellSize*.7f},1);
        }
    }
    private void OnEnable(){if(playerTorch!=null)playerTorch.SetActive(true);}
    private void OnDisable(){if(playerTorch!=null)playerTorch.SetActive(false);foreach(var t in torches)if(t.light!=null)t.light.enabled=false;}
    private GameObject CreateFlame(Transform parent,Vector3 position)
    {
        var visual=GameObject.CreatePrimitive(PrimitiveType.Sphere);visual.name="Flame";visual.transform.SetParent(parent,false);
        visual.transform.position=position;visual.transform.localScale=new Vector3(.08f,.19f,.08f);
        var collider=visual.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        visual.GetComponent<Renderer>().sharedMaterial=flameMaterial;return visual;
    }
    private void OnDestroy(){if(playerTorch!=null)Destroy(playerTorch);if(mistMaterial!=null)Destroy(mistMaterial);if(flameMaterial!=null)Destroy(flameMaterial);if(mistTexture!=null)Destroy(mistTexture);}
}
