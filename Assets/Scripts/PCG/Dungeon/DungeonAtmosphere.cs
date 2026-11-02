using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Local, reversible presentation. Does not modify shared profiles or global render settings.</summary>
public sealed class DungeonAtmosphere : MonoBehaviour
{
    [System.Serializable]
    public sealed class Settings
    {
        [Header("Interior: subtle mist")]
        [Range(0,1)] public float mistOpacity = .10f;
        [Range(.5f,4)] public float mistSize = 1.6f;
        [Range(.1f,2)] public float mistHeight = .45f;
        [Header("Exterior: dense animated backdrop")]
        public Shader exteriorFogShader;
        public Color exteriorFogColor = new Color(.16f,.20f,.24f,1);
        [Tooltip("Exterior fog drift speed. Zero freezes the backdrop.")]
        [Range(0,2)] public float exteriorFogSpeed = .65f;
        [Tooltip("Faint visibility of the exterior mist. Does not illuminate the dungeon.")]
        [Range(0,1)] public float exteriorFogBrightness = .22f;
        [Range(1,12)] public float torchRange = 5;
        [Range(0,20)] public float torchIntensity = 9;
    }
    private Settings settings;
    private readonly List<Vector3> mistCells = new List<Vector3>();
    private sealed class Torch { public GameObject root, flame; public Light light; public bool lit; public float phase; }
    private readonly List<Torch> torches=new List<Torch>();
    private Transform avatar;
    private GameObject playerTorch;
    private Light playerLight;
    private ParticleSystem mist;
    private Material mistMaterial;
    private Material flameMaterial;
    private Material exteriorMaterial;
    private Texture2D mistTexture;
    private float emissionTimer;
    private DungeonFloorData floor;
    private System.Random random;

    public void Initialize(DungeonFloorData data,Transform player,GameObject support,bool enabledAtmosphere,Settings configuration)
    {
        if(!enabledAtmosphere){enabled=false;return;}
        floor=data;avatar=player;settings=configuration;random=new System.Random(unchecked(data.seed^0x71A37));
        playerTorch=new GameObject("Player torch");playerTorch.transform.SetParent(player,false);
        playerTorch.transform.localPosition=new Vector3(.25f,1.35f,.2f);
        playerLight=playerTorch.AddComponent<Light>();playerLight.type=LightType.Point;
        playerLight.color=new Color(1,.65f,.3f);playerLight.intensity=settings.torchIntensity;playerLight.range=settings.torchRange;
        playerLight.shadows=LightShadows.Soft;
        // URP exposes this serialized tier read-only; use the pipeline's low-resolution tier.
        JsonUtility.FromJsonOverwrite("{\"m_AdditionalLightsShadowResolutionTier\":0}", playerLight.gameObject.AddComponent<UniversalAdditionalLightData>());
        var flame=playerTorch.AddComponent<DungeonRunFlameLight>();flame.targetLight=playerLight;
        flame.baseIntensity=settings.torchIntensity;flame.baseRange=settings.torchRange;flame.poolIntensity=0;flame.positionJitter=0;
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
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=3.5f;light.intensity=0;
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
        main.startLifetime=6;main.startSpeed=0;main.startSize=settings.mistSize;
        main.startColor=new Color(.075f,.095f,.12f,settings.mistOpacity);main.maxParticles=400;
        var fade=mist.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(1,.7f),new GradientAlphaKey(0,1)});
        fade.color=gradient;
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
        var mistRenderer=mist.GetComponent<ParticleSystemRenderer>();mistRenderer.sharedMaterial=mistMaterial;
        mistRenderer.maxParticleSize=.5f;mistRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        mist.Play();RefreshMistCells();EmitMist(45,true);
        CreateExteriorFog();
    }
    private void CreateExteriorFog()
    {
        var shader=settings.exteriorFogShader!=null?settings.exteriorFogShader:Shader.Find("DungeonRun/Guille Exterior Fog");
        if(shader==null)throw new System.InvalidOperationException("Assign the exterior fog shader in Atmosphere Settings.");
        exteriorMaterial=new Material(shader);exteriorMaterial.SetColor("_FogColor",settings.exteriorFogColor);
        exteriorMaterial.SetFloat("_FlowSpeed",settings.exteriorFogSpeed);
        exteriorMaterial.SetFloat("_Brightness",settings.exteriorFogBrightness);
        var bounds=new Bounds(floor.CellCenter(floor.cells[0].coordinate),Vector3.zero);
        foreach(var cell in floor.cells)bounds.Encapsulate(floor.CellCenter(cell.coordinate));
        var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);backdrop.name="Exterior fog backdrop";
        backdrop.transform.SetParent(transform,false);
        // Below the floor: geometry occludes dense fog without a discovery mask or extra lighting.
        backdrop.transform.position=new Vector3(bounds.center.x,-.4f,bounds.center.z);
        backdrop.transform.rotation=Quaternion.Euler(90,0,0);
        backdrop.transform.localScale=new Vector3(bounds.size.x+200,bounds.size.z+200,1);
        var collider=backdrop.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        var renderer=backdrop.GetComponent<Renderer>();renderer.sharedMaterial=exteriorMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private void Update()
    {
        if(avatar==null)return;
        if(exteriorMaterial!=null)
        {
            exteriorMaterial.SetFloat("_FlowSpeed",settings.exteriorFogSpeed);
            exteriorMaterial.SetFloat("_Brightness",settings.exteriorFogBrightness);
            exteriorMaterial.SetColor("_FogColor",settings.exteriorFogColor);
        }
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
        if(emissionTimer<.25f)return;emissionTimer=0;
        RefreshMistCells();EmitMist(4,false);
    }
    private void RefreshMistCells()
    {
        mistCells.Clear();
        foreach(var cell in floor.cells)
        {
            Vector3 point=floor.CellCenter(cell.coordinate);
            if((point-avatar.position).sqrMagnitude<144)mistCells.Add(point);
        }
    }
    private void EmitMist(int count,bool warmup)
    {
        if(mistCells.Count==0||settings.mistOpacity<=0)return;
        for(int i=0;i<count;i++)
        {
            var point=mistCells[random.Next(mistCells.Count)]+Vector3.up*settings.mistHeight;
            mist.Emit(new ParticleSystem.EmitParams{position=point,startSize=floor.cellSize*settings.mistSize,
                startLifetime=warmup?2+(float)random.NextDouble()*4:6},1);
        }
        if(warmup)mist.Simulate(1.25f,true,false,true);
        mist.Play();
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
    private void OnDestroy(){if(playerTorch!=null)Destroy(playerTorch);if(mistMaterial!=null)Destroy(mistMaterial);if(flameMaterial!=null)Destroy(flameMaterial);if(mistTexture!=null)Destroy(mistTexture);if(exteriorMaterial!=null)Destroy(exteriorMaterial);}
}
