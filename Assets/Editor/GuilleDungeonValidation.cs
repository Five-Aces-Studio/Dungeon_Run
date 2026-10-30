using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>Local validation bridge for this prototype when the Unity MCP connector is unavailable.</summary>
[InitializeOnLoad]
public static class GuilleDungeonValidation
{
    private const string ScenePath="Assets/Scenes/SceneGuille/SceneGuilleDungeon.unity";
    private static string Folder => Path.GetFullPath("artifacts/guille-dungeon");
    [Serializable] private sealed class Request { public string command; }
    [Serializable] private sealed class Result { public bool success; public string message; }
    private static double nextPoll;
    static GuilleDungeonValidation(){EditorApplication.update+=Poll;}
    private static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        nextPoll=EditorApplication.timeSinceStartup+.5;
        string path=Path.Combine(Folder,"request.json");
        if(!File.Exists(path))return;
        try
        {
            string command=JsonUtility.FromJson<Request>(File.ReadAllText(path)).command;File.Delete(path);
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open SceneGuilleDungeon first.");
            switch(command)
            {
                case "setup": Setup();break;
                case "refresh": AssetDatabase.Refresh();break;
                case "validate": ValidateData();break;
                case "play": EditorApplication.isPlaying=true;break;
                case "stop": EditorApplication.isPlaying=false;break;
                case "capture": Capture();break;
                case "smoke": StartSmoke();break;
                case "tests": RunTests();break;
                case "large":
                    Require(EditorApplication.isPlaying,"Large floor requires Play Mode");
                    var large=UnityEngine.Object.FindFirstObjectByType<DungeonRunController>();
                    large.minimumRooms=large.maximumRooms=large.roomLimit=16;large.StartNewRun();break;
                case "capture_base":
                    var style=Camera.main.GetComponent<DungeonRunWorldStyleSettings>();
                    bool previous=style.enabled;
                    try{style.enabled=false;Capture();File.Copy(Path.Combine(Folder,"capture.png"),Path.Combine(Folder,"base.png"),true);}
                    finally{style.enabled=previous;}break;
                case "status":
                    var run=UnityEngine.Object.FindFirstObjectByType<DungeonRunController>();
                    Write(true,$"playing={EditorApplication.isPlaying}; floor={run?.State?.floorNumber}; rooms={run?.Floor?.rooms.Count}; objects={run?.Floor?.objects.Count}; {run?.LastMessage}");return;
                default:throw new ArgumentException("Unknown validation command.");
            }
            Write(true,command+" completed");
        }
        catch(Exception e){Write(false,e.ToString());Debug.LogException(e);}
    }
    private static void Write(bool ok,string text){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.json"),JsonUtility.ToJson(new Result{success=ok,message=text},true));}
    private static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/MazePrototype/Prefabs/"+name+".prefab");
    [MenuItem("Tools/Guille Dungeon/Configure prototype")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before setup.");
        var probe=UnityEngine.Object.FindFirstObjectByType<DungeonDataProbe>();
        if(probe==null)throw new InvalidOperationException("Missing probe.");
        var run=probe.GetComponent<DungeonRunController>();if(run==null)run=Undo.AddComponent<DungeonRunController>(probe.gameObject);
        run.chestPrefab=Prefab("PF_PROP_Maze_Chest");run.altarPrefab=Prefab("PF_PROP_Maze_EventAltar");
        run.upgradePrefab=Prefab("PF_PROP_Maze_Shop");run.pillarPrefab=Prefab("PF_ENV_Maze_Pillar");run.enemyPrefab=Prefab("PF_MazeTraveler");
        var so=new SerializedObject(probe);so.FindProperty("enableFullPrototype").boolValue=true;
        so.FindProperty("previewNavigationOnClick").boolValue=true;so.ApplyModifiedProperties();
        var camera=Camera.main;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.032f,.05f);
        RenderSettings.fogColor=new Color(.025f,.035f,.055f);
        const string profilePath="Assets/Settings/Dungeon/GuilleWorldStyle.asset";
        if(AssetDatabase.LoadAssetAtPath<DungeonRunWorldStyleProfile>(profilePath)==null)
            AssetDatabase.CopyAsset("Assets/Settings/AcrylicV3/DungeonRun_World_AcrylicSoft.asset",profilePath);
        var profile=AssetDatabase.LoadAssetAtPath<DungeonRunWorldStyleProfile>(profilePath);
        profile.paintStrength=.35f;profile.pixelInfluence=.18f;profile.shadowLift=.08f;profile.hazeStrength=.08f;EditorUtility.SetDirty(profile);
        var style=camera.GetComponent<DungeonRunWorldStyleSettings>();if(style==null)style=Undo.AddComponent<DungeonRunWorldStyleSettings>(camera.gameObject);
        style.profile=profile;EditorUtility.SetDirty(style);
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if(pipeline!=null)
        {
            var pipelineData=new SerializedObject(pipeline);var renderers=pipelineData.FindProperty("m_RendererDataList");
            var additional=camera.GetUniversalAdditionalCameraData();
            if(renderers!=null&&renderers.arraySize>1)additional.SetRenderer(1);else additional.SetRenderer(0);
            EditorUtility.SetDirty(additional);
        }
        EditorUtility.SetDirty(camera);
        // Only this dedicated scene's directional light is changed, no shared materials/profiles.
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional){Undo.RecordObject(light,"Dungeon lighting");light.intensity=.32f;light.color=new Color(.64f,.71f,.85f);}
        AssetDatabase.SaveAssets();EditorUtility.SetDirty(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    [MenuItem("Tools/Guille Dungeon/Validate deterministic data")]
    public static void ValidateData()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run data validation outside Play Mode.");
        var probe=UnityEngine.Object.FindFirstObjectByType<DungeonDataProbe>();
        int passed=0,controlled=0;
        for(int i=0;i<24;i++)
        {
            int seed=12345+i*7919;int count=i%3==0?16:6+i%4;
            DungeonFloorData source;
            try{source=probe.GenerateForRun(seed,count);}catch(ArgumentException){controlled++;continue;}
            string original=JsonUtility.ToJson(source);
            var a=DungeonPopulation.Prepare(source,i%4==0,3,3);
            var b=DungeonPopulation.Prepare(source,i%4==0,3,3);
            Require(original==JsonUtility.ToJson(source),"Population mutated structural source");
            Require(JsonUtility.ToJson(a)==JsonUtility.ToJson(b),"Population not repeatable");
            Require(a.objects.Select(o=>o.cell).Distinct().Count()==a.objects.Count,"Overlapping content");
            Require(a.objects.Count(o=>o.kind==DungeonObjectKind.Key)==1,"Key count");
            Require(a.objects.Count(o=>o.kind==DungeonObjectKind.Stairs)==1,"Stairs count");
            Require(a.objects.All(o=>o.kind!=DungeonObjectKind.Decoration||(source.cells.Find(c=>c.coordinate==o.cell).occupancy&CellOccupancy.Reserved)==0),"Decoration on protected route");
            DungeonPopulation.Validate(a);passed++;
        }
        Require(passed>=12,"Too many generation failures");
        var state=new DungeonRunState{masterSeed=91};
        Require(state.CollectKey(1,0,1)&&state.nextIsBoss,"Boss roll");
        Require(!state.CollectKey(0,0,0)&&state.nextIsBoss,"Boss rerolled on duplicate key");
        Require(!state.CanDescend,"Locked exit accepted");state.doorsOpen=true;
        var next=state.Next(true);Require(next.isBossFloor&&!next.hasKey&&next.floorNumber==2,"Progression state");
        next.CollectKey(1,0,1);next.doorsOpen=true;Require(!next.CanDescend,"Boss exit accepted before resolution");
        next.bossDefeated=true;Require(next.CanDescend&&!next.nextIsBoss&&next.Next(true).floorsSinceBoss==0,"Boss reset");
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"data-validation.txt"),$"PASS: {passed} seeds; {controlled} controlled layout failures. Determinism, source isolation, footprints, gates, key, approaches, boss state and duplicate interactions.");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static TestRunnerApi testApi;
    private static void RunTests()
    {
        testApi=ScriptableObject.CreateInstance<TestRunnerApi>();testApi.RegisterCallbacks(new TestCallbacks());
        testApi.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"WorldStyleGateTests"}}));
    }
    private sealed class TestCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor test){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {TestRunnerApi.SaveResultToFile(result,Path.Combine(Folder,"world-style-tests.xml"));UnityEngine.Object.DestroyImmediate(testApi);}
    }
    public static void Capture()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture requires Play Mode.");
        Directory.CreateDirectory(Folder);
        var camera=Camera.main;var previous=camera.targetTexture;
        var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var previousActive=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Folder,"capture.png"),texture.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"game-view.png"));
        var run=UnityEngine.Object.FindFirstObjectByType<DungeonRunController>();
        string stats=$"Rooms: {run.Floor.rooms.Count}; smoothed frame time: {Time.smoothDeltaTime*1000:F2} ms. Editor frame: {UnityStats.drawCalls} draw calls; {UnityStats.batches} batches; {UnityStats.triangles} triangles. Not a standalone performance benchmark.";
        File.WriteAllText(Path.Combine(Folder,"render-stats.txt"),stats);
        File.WriteAllText(Path.Combine(Folder,$"render-stats-{run.Floor.rooms.Count}-rooms.txt"),stats);
    }
    private static DungeonRunController smokeRun;
    private static DungeonAvatarMove smokeAvatar;
    private static int smokeTarget=-1, smokeSteps;
    private static double smokeDeadline;
    private static bool previousBackground;
    private static void StartSmoke()
    {
        Require(EditorApplication.isPlaying,"Smoke requires Play Mode");
        smokeRun=UnityEngine.Object.FindFirstObjectByType<DungeonRunController>();
        smokeAvatar=UnityEngine.Object.FindFirstObjectByType<DungeonAvatarMove>();
        Require(smokeRun!=null&&smokeRun.State!=null,"No running floor");
        previousBackground=Application.runInBackground;Application.runInBackground=true;
        smokeRun.initialBossChance=smokeRun.maximumBossChance=1;smokeRun.bossChanceIncrease=0;
        var speed=new SerializedObject(smokeAvatar);speed.FindProperty("moveSpeed").floatValue=18;speed.ApplyModifiedProperties();
        smokeTarget=-1;smokeSteps=0;smokeDeadline=EditorApplication.timeSinceStartup+600;
        EditorApplication.update-=SmokeTick;EditorApplication.update+=SmokeTick;
    }
    private static void SmokeTick()
    {
        try
        {
            if(!EditorApplication.isPlaying||smokeRun==null)throw new InvalidOperationException("Smoke interrupted");
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup>smokeDeadline)throw new TimeoutException("Smoke timed out");
            if(smokeRun.IsTransitioning||smokeAvatar.IsMoving)return;
            if(smokeRun.State.floorNumber>=4)
            {
                Require(UnityEngine.Object.FindObjectsByType<DungeonContentView>(FindObjectsSortMode.None).Length==1,"Old content survived transition");
                Require(UnityEngine.Object.FindObjectsByType<DungeonAtmosphere>(FindObjectsSortMode.None).Length==1,"Old atmosphere survived transition");
                Require(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.name=="Player torch")==1,"Player torches accumulated");
                File.WriteAllText(Path.Combine(Folder,"runtime-validation.txt"),$"PASS: {smokeSteps} real movement/interactions; three descents including provisional boss; floor {smokeRun.State.floorNumber}; content, atmosphere and torch cleanup.");
                EditorApplication.update-=SmokeTick;Capture();Application.runInBackground=previousBackground;return;
            }
            if(smokeTarget>=0)
            {
                int id=smokeTarget;smokeTarget=-1;
                Require(smokeRun.Interact(id),"Interaction rejected after walking to approach: "+id);
                smokeSteps++;
                if(smokeRun.State.floorNumber>=4)
                {
                    return; // Allow deferred destruction before checking cleanup on the next tick.
                }
            }
            DungeonObjectData item=null;
            if(smokeRun.State.isBossFloor&&!smokeRun.State.bossDefeated)item=smokeRun.Floor.objects.Find(o=>o.kind==DungeonObjectKind.Boss&&!o.consumed);
            else if(!smokeRun.State.hasKey)item=smokeRun.Floor.objects.Find(o=>o.kind==DungeonObjectKind.Key&&!o.consumed);
            else if(!smokeRun.State.doorsOpen)item=smokeRun.Floor.objects.FirstOrDefault(o=>o.kind==DungeonObjectKind.Door&&!o.consumed&&Approach(o)!=null);
            else item=smokeRun.Floor.objects.Find(o=>o.kind==DungeonObjectKind.Stairs);
            Require(item!=null,"No next interaction");
            var path=Approach(item);Require(path!=null,"No physical route to "+item.kind);
            smokeAvatar.Follow(path);smokeTarget=item.id;
        }
        catch(Exception e){EditorApplication.update-=SmokeTick;Application.runInBackground=previousBackground;File.WriteAllText(Path.Combine(Folder,"runtime-validation.txt"),"FAIL: "+e);Debug.LogException(e);}
    }
    private static System.Collections.Generic.List<Vector2Int> Approach(DungeonObjectData item)
    {
        var positions=DungeonPopulation.Directions.Select(d=>item.cell+d).ToList();
        if(!item.blocking)positions.Insert(0,item.cell);
        System.Collections.Generic.List<Vector2Int> best=null;
        foreach(var position in positions)
            if(smokeAvatar.TryFindPath(position,out var path)&&(best==null||path.Count<best.Count))best=path;
        return best;
    }
}
