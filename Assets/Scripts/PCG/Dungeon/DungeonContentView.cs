using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DungeonContentView : MonoBehaviour
{
    private readonly Dictionary<int,GameObject> instances = new Dictionary<int,GameObject>();
    private readonly List<Material> ownedMaterials = new List<Material>();
    public void Build(DungeonFloorData floor, DungeonRunController run)
    {
        foreach (var item in floor.objects)
        {
            var root = new GameObject(item.kind + "_" + item.id);
            root.transform.SetParent(transform,false);
            root.transform.position = floor.CellCenter(item.cell);
            instances.Add(item.id,root);
            GameObject prefab = null;
            switch(item.kind)
            {
                case DungeonObjectKind.Chest: prefab=run.chestPrefab; break;
                case DungeonObjectKind.Event:
                case DungeonObjectKind.Healing: prefab=run.altarPrefab; break;
                case DungeonObjectKind.Upgrade: prefab=run.upgradePrefab; break;
                case DungeonObjectKind.Decoration: prefab=run.pillarPrefab; break;
                case DungeonObjectKind.Enemy:
                case DungeonObjectKind.Boss: prefab=run.enemyPrefab; break;
            }
            float height = item.kind == DungeonObjectKind.Decoration ? 1.6f : item.kind == DungeonObjectKind.Door ? 1.9f :
                item.kind == DungeonObjectKind.Enemy || item.kind == DungeonObjectKind.Boss ? 1.4f : .65f;
            float width = floor.cellSize * (item.kind == DungeonObjectKind.Door ? .99f : .70f);
            GameObject visual;
            if (prefab != null)
            {
                visual = Instantiate(prefab,root.transform);
                visual.transform.localPosition=Vector3.zero;
                visual.transform.localRotation=Quaternion.identity;
                foreach(var c in visual.GetComponentsInChildren<Collider>()) { c.enabled=false; Destroy(c); }
                var bounds = new Bounds(); bool first=true;
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
                { if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds); }
                if(first || bounds.size.x<=0 || bounds.size.y<=0 || bounds.size.z<=0) throw new ArgumentException("Invalid prefab bounds: "+prefab.name);
                Vector3 scale = new Vector3(width/bounds.size.x,height/bounds.size.y,width/bounds.size.z);
                Vector3 offset = bounds.center-root.transform.position;
                visual.transform.localScale=Vector3.Scale(visual.transform.localScale,scale);
                visual.transform.localPosition=Vector3.up*(height*.5f)-Vector3.Scale(offset,scale);
            }
            else
            {
                visual=GameObject.CreatePrimitive(item.kind == DungeonObjectKind.Key ? PrimitiveType.Sphere :
                    item.kind == DungeonObjectKind.Enemy || item.kind == DungeonObjectKind.Boss ? PrimitiveType.Capsule : PrimitiveType.Cube);
                visual.transform.SetParent(root.transform,false);
                var c=visual.GetComponent<Collider>(); c.enabled=false; Destroy(c);
                if(item.kind==DungeonObjectKind.Key){height=.25f;width=.25f;}
                visual.transform.localScale=new Vector3(width,height/(item.kind==DungeonObjectKind.Enemy||item.kind==DungeonObjectKind.Boss?2:1),width);
                visual.transform.localPosition=Vector3.up*(height*.5f+.02f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color=ColorFor(item.kind); ownedMaterials.Add(material);
                visual.GetComponent<Renderer>().sharedMaterial=material;
            }
            if(item.blocking)
            {
                var c=root.AddComponent<BoxCollider>();
                c.center=Vector3.up*(height*.5f);
                c.size=new Vector3(width,height,width);
            }
            if(item.kind!=DungeonObjectKind.Decoration)
            {
                var label=new GameObject("Label"); label.transform.SetParent(root.transform,false);
                label.transform.localPosition=Vector3.up*(height+.3f);
                label.transform.rotation=Quaternion.Euler(55,0,0);
                var text=label.AddComponent<TextMesh>(); text.text=ShortLabel(item.kind); text.fontSize=40; text.characterSize=.045f;
                text.anchor=TextAnchor.MiddleCenter; text.color=ColorFor(item.kind);
            }
        }
    }
    private static string ShortLabel(DungeonObjectKind kind)
    {
        switch(kind){case DungeonObjectKind.Key:return "LLAVE";case DungeonObjectKind.Door:return "CERRADO";
            case DungeonObjectKind.Stairs:return "DESCENSO";case DungeonObjectKind.Healing:return "CURACIÓN";
            case DungeonObjectKind.Upgrade:return "MEJORA";case DungeonObjectKind.Event:return "EVENTO";
            case DungeonObjectKind.Chest:return "COFRE";case DungeonObjectKind.Boss:return "JEFE · PRUEBA";default:return "ENEMIGO";}
    }
    private static Color ColorFor(DungeonObjectKind kind)
    {
        switch(kind){case DungeonObjectKind.Key:case DungeonObjectKind.Chest:return new Color(1,.72f,.18f);
            case DungeonObjectKind.Enemy:case DungeonObjectKind.Boss:return new Color(.8f,.2f,.2f);
            case DungeonObjectKind.Healing:return new Color(.35f,.9f,.6f);
            case DungeonObjectKind.Stairs:return new Color(.4f,.75f,1);
            default:return new Color(.65f,.55f,.8f);}
    }
    public bool IsPartOf(int id, Transform child) => instances.TryGetValue(id,out var root) && child.IsChildOf(root.transform);
    public void Remove(int id)
    { if(instances.TryGetValue(id,out var root)){root.SetActive(false);Destroy(root);instances.Remove(id);} }

    public void ValidatePhysicalAccess(DungeonAvatarMove avatar, DungeonFloorData floor)
    {
        var gates=new List<Collider>();
        foreach(var item in floor.objects)
            if(item.kind==DungeonObjectKind.Door) gates.AddRange(instances[item.id].GetComponentsInChildren<Collider>());
        try
        {
            foreach(var gate in gates) gate.enabled=false;
            Physics.SyncTransforms();
            var walkable=DungeonPopulation.OpenDistances(floor);
            var visited=new HashSet<Vector2Int>();var pending=new Queue<Vector2Int>();
            var start=floor.rooms.Find(r=>r.type==DungeonRoomType.Start).reservedCenter;
            visited.Add(start);pending.Enqueue(start);
            while(pending.Count>0)
            {
                var from=pending.Dequeue();
                foreach(var d in DungeonPopulation.Directions)
                {
                    var to=from+d;
                    if(!walkable.ContainsKey(to)||visited.Contains(to)||!avatar.CanTravelBetweenCells(from,to))continue;
                    visited.Add(to);pending.Enqueue(to);
                }
            }
            foreach(var corridor in floor.corridors)
                if(!visited.Contains(corridor.doorA.startCell)||!visited.Contains(corridor.doorB.startCell))
                    throw new ArgumentException($"The avatar cannot physically fit through a reserved route: {corridor.doorA.startCell} -> {corridor.doorB.startCell}; reachable {visited.Count}/{walkable.Count}.");
            if(!visited.Contains(floor.keyCell))throw new ArgumentException("The avatar cannot physically reach the key.");
            foreach(var item in floor.objects)
            {
                if(item.kind==DungeonObjectKind.Key||item.kind==DungeonObjectKind.Decoration)continue;
                bool approach=false;
                foreach(var d in DungeonPopulation.Directions)if(visited.Contains(item.cell+d))approach=true;
                if(!approach)throw new ArgumentException("No physical approach to "+item.kind);
            }
        }
        finally{foreach(var gate in gates)if(gate!=null)gate.enabled=true;Physics.SyncTransforms();}
    }
    private void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);}
}
