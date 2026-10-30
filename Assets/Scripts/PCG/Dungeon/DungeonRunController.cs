using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Exploration prototype, isolated to SceneGuilleDungeon. No combat rules are reimplemented here.</summary>
public sealed class DungeonRunController : MonoBehaviour
{
    [Header("Progression (prototype values)")]
    [Min(3)] public int minimumRooms = 6;
    [Min(3)] public int maximumRooms = 8;
    [Range(0, 2)] public int roomsPerFloor = 1;
    [Range(3, 32)] public int roomLimit = 16;
    [Range(0,1)] public float initialBossChance = .1f;
    [Range(0,1)] public float bossChanceIncrease = .08f;
    [Range(0,1)] public float maximumBossChance = .65f;
    public bool resetChanceAfterBoss = true;
    [Header("Content")]
    [Range(0,12)] public int decorationPerRoom = 3;
    [Range(0,3)] public int maximumEnemiesPerRoom = 3;
    public bool enablePrototypeEncounterResolution = true;
    public GameObject chestPrefab, altarPrefab, upgradePrefab, pillarPrefab, enemyPrefab;
    public bool enableAtmosphere = true;
    public DungeonRunState State { get; private set; }
    public DungeonFloorData Floor { get; private set; }
    public string LastMessage { get; private set; } = "";
    public bool IsTransitioning { get; private set; }
    public event Action<DungeonObjectData> EncounterRequested;
    public event Action<DungeonObjectData> InteractionCompleted;
    private DungeonDataProbe probe;
    private DungeonContentView content;
    private DungeonAtmosphere atmosphere;
    private DungeonObjectData target;
    private float messageUntil;

    public void Initialize(DungeonDataProbe source) { probe = source; }
    public void StartNewRun()
    {
        if (probe == null) Initialize(GetComponent<DungeonDataProbe>());
        if (probe == null || IsTransitioning) return;
        TryInstall(new DungeonRunState { masterSeed = probe.InitialSeed });
    }

    public bool TryDescend()
    {
        if (State == null || !State.CanDescend || IsTransitioning) return false;
        return TryInstall(State.Next(resetChanceAfterBoss));
    }

    private bool TryInstall(DungeonRunState next)
    {
        IsTransitioning = true;
        DungeonRoomView staging = null;
        GameObject stagingObject = null;
        GameObject previousRoot = null;
        var oldFloor = Floor;
        var oldState = State;
        var oldContent = content;
        Vector3 oldPosition = probe.Avatar != null ? probe.Avatar.transform.position : Vector3.zero;
        try
        {
            if (probe.Avatar == null || probe.View == null) throw new ArgumentException("Assign avatar and room view.");
            if (minimumRooms < 3 || maximumRooms < minimumRooms || roomLimit < maximumRooms || roomLimit > 32)
                throw new ArgumentException("Invalid room progression range.");
            int lower = Math.Min(roomLimit, minimumRooms + (next.floorNumber - 1) * roomsPerFloor);
            int upper = Math.Min(roomLimit, maximumRooms + (next.floorNumber - 1) * roomsPerFloor);
            int floorSeed = unchecked(next.masterSeed ^ (next.floorNumber * 0x19A713));
            int count = new System.Random(floorSeed).Next(lower, upper + 1);
            DungeonFloorData prepared = null;
            string failure = "";
            for (int attempt = 0; attempt < 12 && prepared == null; attempt++)
            {
                try
                {
                    var structural = probe.GenerateForRun(unchecked(floorSeed + attempt * 7919), count);
                    prepared = DungeonPopulation.Prepare(structural, next.isBossFloor, decorationPerRoom, maximumEnemiesPerRoom);
                }
                catch (ArgumentException e) { failure = e.Message; }
            }
            if (prepared == null) throw new ArgumentException("No valid floor after 12 bounded attempts: " + failure);

            // Build before touching the live floor. The old floor remains recoverable until spawn validation passes.
            stagingObject = new GameObject("Dungeon staging view");
            staging = stagingObject.AddComponent<DungeonRoomView>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(probe.View), staging);
            staging.Build(prepared);
            var newContent = staging.Root.AddComponent<DungeonContentView>();
            newContent.Build(prepared, this);
            staging.Root.AddComponent<DungeonMaterialScope>().Apply();
            staging.Root.SetActive(false);

            previousRoot = probe.View.Root;
            if (previousRoot != null) previousRoot.SetActive(false);
            if (oldContent != null && oldContent.gameObject != previousRoot) oldContent.gameObject.SetActive(false);
            probe.Avatar.ResetNavigation();
            staging.Root.SetActive(true);
            Physics.SyncTransforms();
            var start = prepared.rooms.Find(r => r.type == DungeonRoomType.Start).reservedCenter;
            probe.Avatar.Initialize(prepared, probe.NavigationSettings, start);
            newContent.ValidatePhysicalAccess(probe.Avatar, prepared);

            var newAtmosphere = staging.Root.AddComponent<DungeonAtmosphere>();
            newAtmosphere.Initialize(prepared, probe.Avatar.transform, pillarPrefab, enableAtmosphere);

            var installedRoot = staging.ReleaseRoot();
            probe.View.AdoptRoot(installedRoot, prepared);
            Floor = prepared; State = next; content = newContent;
            probe.UseRuntimeFloor(prepared);
            if (previousRoot != null) Destroy(previousRoot);
            if (oldContent != null && oldContent.gameObject != previousRoot) Destroy(oldContent.gameObject);
            atmosphere = newAtmosphere;
            Message(next.isBossFloor ? "Piso de JEFE PROVISIONAL. Resuelve la prueba y encuentra la llave." : "Encuentra la llave y abre las escaleras.");
            return true;
        }
        catch (Exception e)
        {
            if (staging != null) staging.Clear();
            if (previousRoot != null) previousRoot.SetActive(true);
            if (oldContent != null) oldContent.gameObject.SetActive(true);
            if (oldFloor != null)
            {
                Floor = oldFloor; State = oldState; content = oldContent;
                probe.UseRuntimeFloor(oldFloor);
                Physics.SyncTransforms();
                var restore = new Vector2Int(Mathf.RoundToInt(oldPosition.x / oldFloor.cellSize), Mathf.RoundToInt(oldPosition.z / oldFloor.cellSize));
                try { probe.Avatar.Initialize(oldFloor, probe.NavigationSettings, restore); }
                catch { probe.Avatar.Initialize(oldFloor, probe.NavigationSettings, oldFloor.rooms.Find(r=>r.type==DungeonRoomType.Start).reservedCenter); }
            }
            else if (probe.Avatar != null) probe.Avatar.ResetNavigation();
            Message("No se pudo generar el piso; puedes reintentar. " + e.Message);
            Debug.LogError(e, this);
            return false;
        }
        finally { if (stagingObject != null) Destroy(stagingObject); IsTransitioning = false; }
    }

    private void Update()
    {
        target = null;
        if (Floor == null || probe == null || !probe.Avatar.IsReady || probe.Avatar.IsMoving || IsTransitioning) return;
        float best = float.PositiveInfinity;
        foreach (var item in Floor.objects)
        {
            if (item.consumed || item.kind == DungeonObjectKind.Decoration) continue;
            Vector3 delta = Floor.CellCenter(item.cell) - probe.Avatar.transform.position;
            float distance = delta.sqrMagnitude;
            if (distance > Floor.cellSize * Floor.cellSize * 1.5f || distance >= best) continue;
            if (!CanInteract(item)) continue;
            best = distance; target = item;
        }
        if (target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interact(target.id);
    }

    private bool CanInteract(DungeonObjectData item)
    {
        Vector2Int current = probe.Avatar.CurrentCell;
        int manhattan = Math.Abs(current.x-item.cell.x)+Math.Abs(current.y-item.cell.y);
        if (manhattan > 1 || (item.blocking && manhattan == 0)) return false;
        Vector3 origin = probe.Avatar.transform.position + Vector3.up * .65f;
        Vector3 destination = Floor.CellCenter(item.cell) + Vector3.up * .65f;
        Vector3 delta = destination-origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(probe.Avatar.transform)) continue;
            if (content.IsPartOf(item.id, hit.transform)) continue;
            return false;
        }
        return true;
    }

    public bool Interact(int id)
    {
        if (IsTransitioning || Floor == null || probe.Avatar.IsMoving) return false;
        var item = Floor.objects.Find(o=>o.id==id);
        if (item == null || item.consumed || !CanInteract(item)) return false;
        switch (item.kind)
        {
            case DungeonObjectKind.Key:
                State.CollectKey(initialBossChance,bossChanceIncrease,maximumBossChance);
                Consume(item); Message(State.nextIsBoss ? "Llave obtenida. El siguiente piso será de jefe (prueba)." : "Llave obtenida. Busca una puerta de escaleras."); break;
            case DungeonObjectKind.Door:
                if (!State.hasKey) { Message("Necesitas la llave de este piso."); return false; }
                foreach (var gate in Floor.objects) if (gate.kind == DungeonObjectKind.Door && !gate.consumed) Consume(gate);
                State.doorsOpen = true; // Key is kept as a floor credential; no repeated roll.
                Message("Accesos a las escaleras abiertos."); break;
            case DungeonObjectKind.Stairs:
                if (!State.CanDescend) { Message("Resuelve primero el jefe provisional y abre los accesos."); return false; }
                return TryDescend();
            case DungeonObjectKind.Enemy:
            case DungeonObjectKind.Boss:
                EncounterRequested?.Invoke(item);
                if (!enablePrototypeEncounterResolution) { Message("Encuentro solicitado al sistema de combate."); return true; }
                ResolveEncounter(item.id); break;
            case DungeonObjectKind.Decoration: return false;
            default:
                Consume(item);
                Message(Label(item.kind) + " completado (interacción de prueba; sin alterar cartas o vida)."); break;
        }
        return true;
    }

    public void ResolveEncounter(int id)
    {
        var item = Floor?.objects.Find(o=>o.id==id);
        if (item == null || item.consumed || (item.kind != DungeonObjectKind.Enemy && item.kind != DungeonObjectKind.Boss)) return;
        if (item.kind == DungeonObjectKind.Boss) State.bossDefeated = true;
        Consume(item); Message("Encuentro resuelto en modo de prueba.");
    }
    private void Consume(DungeonObjectData item)
    {
        item.consumed = true;
        if (item.blocking) DungeonPopulation.SetBlocked(Floor,item.cell,false);
        content.Remove(item.id);
        Physics.SyncTransforms();
        InteractionCompleted?.Invoke(item);
    }
    private void Message(string message) { LastMessage = message; messageUntil = Time.unscaledTime + 7; }
    public static string Label(DungeonObjectKind kind)
    {
        switch(kind) {
            case DungeonObjectKind.Key:return "Recoger llave";
            case DungeonObjectKind.Door:return "Abrir puerta";
            case DungeonObjectKind.Stairs:return "Descender";
            case DungeonObjectKind.Chest:return "Cofre";
            case DungeonObjectKind.Healing:return "Curación";
            case DungeonObjectKind.Upgrade:return "Mejora";
            case DungeonObjectKind.Event:return "Evento";
            case DungeonObjectKind.Boss:return "Resolver JEFE PROVISIONAL";
            default:return "Resolver enemigo de prueba";
        }
    }
    private void OnGUI()
    {
        if (State == null)
        {
            GUI.Box(new Rect(16,16,590,75),LastMessage);
            if(GUI.Button(new Rect(16,96,180,35),"Reintentar generación"))StartNewRun();
            return;
        }
        GUI.Box(new Rect(16,16,440,72), $"PISO {State.floorNumber}  ·  {(State.isBossFloor ? "JEFE PROVISIONAL" : "EXPLORACIÓN")}\n" +
            $"Llave: {(State.hasKey ? "sí" : "no")}   |   Escaleras: {(State.doorsOpen ? "abiertas" : "cerradas")}\nClic para caminar · E para interactuar · Encuentros de prueba");
        if (target != null) GUI.Box(new Rect(Screen.width/2-185,Screen.height-85,370,40), "[E] " + Label(target.kind));
        if (Time.unscaledTime < messageUntil) GUI.Box(new Rect(16,96,590,55),LastMessage);
    }
    private void OnDisable() { if (atmosphere != null) atmosphere.enabled = false; }
}
