using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class DungeonAvatarMove : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private CameraFollowNode cameraFollow;

    [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(1f)] private float turnSpeed = 720f;
    [SerializeField, Min(0.1f)] private float bodyHeight = 1.8f;

    [SerializeField] private bool simplifyRoutes = true;

    private static readonly int RunningId = Animator.StringToHash("Running");

    private CharacterController characterController;
    private Animator animator;
    private DungeonFloorData floorData;

    private List<Vector2Int> route;
    private int routeIndex;
    private float stalledTime;

    private readonly Dictionary<Vector2Int, int> roomByCell = new Dictionary<Vector2Int, int>();

    public int? CurrentRoomId { get; private set; }

    public event Action<int?, int?> RoomChanged;

    public bool IsReady => floorData != null && isActiveAndEnabled && characterController.enabled;

    public bool IsMoving => route != null;

    public Vector2Int CurrentCell
    {
        get
        {
            if (floorData == null)
                throw new InvalidOperationException("No hay un piso activo");
            return new Vector2Int(Mathf.RoundToInt(transform.position.x / floorData.cellSize), Mathf.RoundToInt(transform.position.z / floorData.cellSize));
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (visual != null)
        {
            animator = visual.GetComponentInChildren<Animator>(true);
        } 

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        ResetNavigation();
    }

    public void Initialize(DungeonFloorData data, DungeonGenerationSettings settings, Vector2Int start)
    {
        ResetNavigation();

        if (!isActiveAndEnabled) throw new ArgumentException("Activar primero DungeonAvatarMove");

        if (visual == null || visual == transform || !visual.IsChildOf(transform))
        {
            throw new ArgumentException("El visual debe ser hijo del controlador");
        }

        settings.Validate();

        if ((transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            throw new ArgumentException(
                "El controlador debe tener escala mundial (1,1,1).");
        }

        if (!DungeonPathfinder.TryFind(data, start, start, out _))
        {
            throw new ArgumentException(
                "La casilla inicial no es transitable.");
        }

        float radius = settings.avatarRadius + settings.clearance;
        float height = Mathf.Max(bodyHeight, radius * 2f);

        characterController.radius = radius;
        characterController.height = height;
        characterController.center = Vector3.up * (height * 0.5f + 0.02f);
        characterController.skinWidth = Mathf.Min(0.01f, radius * 0.1f);
        characterController.stepOffset = 0f;
        characterController.minMoveDistance = 0f;
        characterController.detectCollisions = true;

        Vector3 spawn = data.CellCenter(start);

        Physics.SyncTransforms();

        Vector3 bottom = spawn + Vector3.up * (radius + 0.02f);
        Vector3 top = spawn + Vector3.up * (height - radius + 0.02f);

        if (Physics.CheckCapsule(bottom, top, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            throw new ArgumentException("El volumen del avatar no cabe en la posición inicial");
        }

        transform.SetPositionAndRotation(spawn, Quaternion.identity);

        floorData = data;

        foreach (DungeonCellData cell in floorData.cells)
        {
            if (cell.roomId == DungeonCellData.CorridorRoomId)
                continue;

            roomByCell.Add(cell.coordinate, cell.roomId);
        }

        characterController.enabled = true;
        visual.gameObject.SetActive(true);

        SetRunning(false);

        if (cameraFollow != null)
            cameraFollow.SetTarget(transform);

        RefreshCurrentRoom();
    }

    private void RefreshCurrentRoom()
    {
        int? nextRoomId = null;

        if (IsReady &&
            roomByCell.TryGetValue(CurrentCell, out int roomId))
        {
            nextRoomId = roomId;
        }

        SetCurrentRoom(nextRoomId);
    }

    private void SetCurrentRoom(int? nextRoomId)
    {
        if (CurrentRoomId == nextRoomId)
            return;

        int? previousRoomId = CurrentRoomId;

        CurrentRoomId = nextRoomId;

        RoomChanged?.Invoke(previousRoomId, nextRoomId);
    }

    public void Follow(List<Vector2Int> path)
    {
        if (!IsReady || IsMoving || path == null || path.Count == 0) return;

        if (path[0] != CurrentCell)
        {
            Debug.LogWarning("La ruta no empieza en la casilla actual del jugador/avatar", this);
            return;
        }

        route = new List<Vector2Int>(path);
        routeIndex = 0;
        stalledTime = 0f;
    }

    private void Update()
    {
        if (!IsReady || route == null || Time.deltaTime <= 0f) return;

        Vector3 delta = Vector3.zero;

        while (routeIndex < route.Count)
        {
            Vector3 target = floorData.CellCenter(route[routeIndex]);
            delta = target - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude > 0.0001f) break;

            routeIndex++;
        }

        if (routeIndex >= route.Count)
        {
            StopMovement();
            return;
        }

        SetRunning(true);

        Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

        visual.rotation = Quaternion.RotateTowards(visual.rotation, rotation, turnSpeed * Time.deltaTime);

        float distanceBefore = delta.magnitude;

        Vector3 motion = Vector3.ClampMagnitude(delta, moveSpeed * Time.deltaTime);

        if (!CanTravelWorld(
                transform.position,
                transform.position + motion))
        {
            StopMovement();

            Debug.LogWarning(
                "Movimiento detenido: un obstáculo impide el siguiente desplazamiento.",
                this);

            return;
        }

        CollisionFlags collisions = characterController.Move(motion);

        Vector3 remaining = floorData.CellCenter(route[routeIndex]) - transform.position;

        remaining.y = 0f;

        if (remaining.magnitude < distanceBefore - 0.00001f)
        {
            stalledTime = 0f;
        }
        else
        {
            stalledTime += Time.deltaTime;
        }

        if ((collisions & CollisionFlags.Sides) != CollisionFlags.None || stalledTime >= 0.5f)
        {
            StopMovement();

            Debug.LogWarning("Movimiento detenido: la ruta de casillas no permite " +
                "pasar al volumen del avatar con la geometría actual.",
                this);
        }
        RefreshCurrentRoom();
    }

    private void StopMovement()
    {
        route = null;
        routeIndex = 0;
        stalledTime = 0f;
        SetRunning(false);
    }

    public void ResetNavigation()
    {
        StopMovement();
        floorData = null;
        roomByCell.Clear();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        characterController.enabled = false;

        if (visual != null && visual != transform)
            visual.gameObject.SetActive(false);

        if (cameraFollow != null)
            cameraFollow.ClearTarget();

        SetCurrentRoom(null);
    }

    private void SetRunning(bool running)
    {
        if (animator != null)
            animator.SetBool(RunningId, running);
    }

    public bool TryFindPath(
        Vector2Int destination,
        out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

        if (!IsReady || IsMoving)
            return false;

        Physics.SyncTransforms();

        Vector2Int start = CurrentCell;

        if (!CanTravelWorld(
                transform.position,
                floorData.CellCenter(start)))
        {
            return false;
        }

        bool found = DungeonPathfinder.TryFind(
            floorData,
            start,
            destination,
            out path,
            CanTraverse);

        if (!found)
            return false;

        if (simplifyRoutes && path.Count > 2)
            path = SimplifyPath(path);

        return true;
    }

    private bool CanTraverse(Vector2Int from, Vector2Int to)
    {
        return CanTravelWorld(
            floorData.CellCenter(from),
            floorData.CellCenter(to));
    }

    public bool CanTravelBetweenCells(Vector2Int from, Vector2Int to)
    { return IsReady && CanTraverse(from, to); }

    private bool CanTravelWorld(Vector3 from, Vector3 to)
    {
        if (!IsCapsuleFree(from) || !IsCapsuleFree(to))
            return false;

        Vector3 displacement = to - from;
        float distance = displacement.magnitude;

        if (distance <= 0.00001f)
            return true;

        GetCapsulePoints(from, out Vector3 bottom, out Vector3 top);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            characterController.radius,
            displacement / distance,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (!BelongsToAvatar(hit.collider))
                return false;
        }

        return true;
    }

    private bool IsCapsuleFree(Vector3 position)
    {
        GetCapsulePoints(position, out Vector3 bottom, out Vector3 top);

        Collider[] overlaps = Physics.OverlapCapsule(
            bottom,
            top,
            characterController.radius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (!BelongsToAvatar(overlap))
                return false;
        }

        return true;
    }

    private void GetCapsulePoints(
        Vector3 position,
        out Vector3 bottom,
        out Vector3 top)
    {
        Vector3 center = position + characterController.center;

        float halfSegment = Mathf.Max(
            0f,
            characterController.height * 0.5f -
            characterController.radius);

        bottom = center - Vector3.up * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    private bool BelongsToAvatar(Collider candidate)
    {
        return candidate != null &&
               candidate.transform.IsChildOf(transform);
    }

    private void OnDisable()
    {
        ResetNavigation();
    }

    private List<Vector2Int> SimplifyPath(List<Vector2Int> original)
    {
        HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floorData.cells)
        {
            if ((cell.occupancy & CellOccupancy.Blocked) == 0)
                walkable.Add(cell.coordinate);
        }

        List<Vector2Int> simplified = new List<Vector2Int>
        {
            original[0]
        };

        int anchorIndex = 0;

        while (anchorIndex < original.Count - 1)
        {
            int nextIndex = anchorIndex + 1;

            for (int candidateIndex = original.Count - 1;
                 candidateIndex > anchorIndex + 1;
                 candidateIndex--)
            {
                if (!CanShortcut(
                        original[anchorIndex],
                        original[candidateIndex],
                        walkable))
                {
                    continue;
                }

                nextIndex = candidateIndex;
                break;
            }

            simplified.Add(original[nextIndex]);
            anchorIndex = nextIndex;
        }

        return simplified;
    }

    private bool CanShortcut(
        Vector2Int from,
        Vector2Int to,
        HashSet<Vector2Int> walkable)
    {
        if (!HasWalkableRectangle(from, to, walkable))
            return false;

        return CanTravelWorld(
            floorData.CellCenter(from),
            floorData.CellCenter(to));
    }

    private bool HasWalkableRectangle(
        Vector2Int from,
        Vector2Int to,
        HashSet<Vector2Int> walkable)
    {
        float radiusInCells =
            characterController.radius / floorData.cellSize;

        int minX = Mathf.CeilToInt(Mathf.Min(from.x, to.x) - radiusInCells - 0.5f);

        int maxX = Mathf.FloorToInt(Mathf.Max(from.x, to.x) + radiusInCells + 0.5f);

        int minY = Mathf.CeilToInt(Mathf.Min(from.y, to.y) - radiusInCells - 0.5f);

        int maxY = Mathf.FloorToInt(Mathf.Max(from.y, to.y) + radiusInCells + 0.5f);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!walkable.Contains(new Vector2Int(x, y)))
                    return false;
            }
        }

        return true;
    }
}
