using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class DungeonDataProbe : MonoBehaviour
{
    [SerializeField] private bool enableFullPrototype = true;
    public DungeonGenerationSettings NavigationSettings => settings;
    public DungeonFloorData CurrentFloor => generatedFloor;
    public DungeonAvatarMove Avatar => avatarMove;
    public DungeonRoomView View => roomView;
    public int InitialSeed => settings.seed;
    public void UseRuntimeFloor(DungeonFloorData floor) { generatedFloor = floor; navigationPreview = null; }

    public DungeonFloorData GenerateForRun(int seed, int count)
    {
        var previousSettings = settings;
        int previousCount = roomCount;
        var previousArea = bspArea;
        bool previousBsp = useBsp, previousCorridors = buildCorridors;
        try
        {
            settings = JsonUtility.FromJson<DungeonGenerationSettings>(JsonUtility.ToJson(previousSettings));
            settings.seed = seed;
            roomCount = count;
            useBsp = buildCorridors = true;
            int side = Mathf.CeilToInt(Mathf.Sqrt(count)) * minimumRegionSide + minimumRegionSide;
            bspArea = new Vector2Int(Mathf.Max(bspArea.x, side), Mathf.Max(bspArea.y, side));
            return GenerateData();
        }
        finally
        {
            settings = previousSettings; roomCount = previousCount; bspArea = previousArea;
            useBsp = previousBsp; buildCorridors = previousCorridors;
        }
    }
    [SerializeField]
    private DungeonGenerationSettings settings = new DungeonGenerationSettings();

    [SerializeField]
    private RoomPreset preset;

    [SerializeField]
    private Vector2Int roomOrigin = Vector2Int.zero;

    [SerializeField, Range(0, 3)]
    private int quarterTurns;

    [SerializeField]
    private DungeonFloorData generatedFloor;

    [SerializeField]
    private DungeonRoomView roomView;

    [SerializeField, Min(0)]
    private int chestRoomCount = 1;

    [SerializeField, Min(0)]
    private int healingRoomCount = 1;

    [SerializeField, Min(0)]
    private int upgradeRoomCount = 1;

    [SerializeField, Min(0)]
    private int eventRoomCount = 1;

    [Header("BSP")]
    [SerializeField] private bool useBsp;
    [SerializeField] private RoomPreset[] roomPresets;

    [SerializeField]
    private Vector2Int bspArea = new Vector2Int(52, 39);

    [SerializeField, Min(1)] private int minimumRegionSide = 13;
    [SerializeField, Min(1)] private int roomCount = 6;
    [SerializeField, Min(1)] private int roomMargin = 1;

    [Header("Previsualización de pasillo")]
    [SerializeField, Min(0)]
    private int connectionToPreview;

    [SerializeField]
    private Corridor corridorPreview;

    [SerializeField]
    private bool buildCorridors;

    [SerializeField, Range(1, 32)]
    private int maximumCorridorAttempts = 8;

    [Header("Previsualización de navegación")]
    [SerializeField, Min(0)]
    private int navigationStartRoom = 0;

    [SerializeField, Min(0)]
    private int navigationDestinationRoom = 1;

    [SerializeField]
    private bool previewNavigationOnClick;

    [SerializeField]
    private Camera navigationCamera;

    [SerializeField]
    private DungeonAvatarMove avatarMove;

    [Header("Content")]
    [SerializeField, Range(0f, 1f)]
    private float combatProbability = 0.6f;

    private List<Vector2Int> navigationPreview;


    private void OnDisable()
    {
        if (avatarMove == null)
            return;

        if (Application.isPlaying)
            avatarMove.ResetNavigation();

        avatarMove.RoomChanged -= HandleRoomChanged;
    }

    private void Start()
    {
        if (enableFullPrototype)
        {
            var run = GetComponent<DungeonRunController>();
            if (run == null) run = gameObject.AddComponent<DungeonRunController>();
            run.Initialize(this);
            run.StartNewRun();
            return;
        }
        Generate();
    }

    [ContextMenu("Generar datos")]
    private void Generate()
    {
        if (Application.isPlaying && enableFullPrototype && GetComponent<DungeonRunController>() != null)
        { GetComponent<DungeonRunController>().StartNewRun(); return; }
        GenerateFloor(true);
    }

    private void Update()
    {
        if (!previewNavigationOnClick ||
            generatedFloor == null ||
            roomView == null)
        {
            return;
        }

        if (avatarMove != null && (!avatarMove.AcceptsClickMovement || !avatarMove.IsReady || avatarMove.IsMoving))
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera camera = navigationCamera != null
            ? navigationCamera
            : Camera.main;

        if (camera == null)
        {
            Debug.LogWarning(
                "Asigna Navigation Camera o una cámara con tag MainCamera.",
                this);
            return;
        }

        Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                1000f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (!roomView.TryGetFloorCell(
                hit.collider,
                out Vector2Int destination))
        {
            return;
        }

        navigationPreview = null;

        try
        {
            Vector2Int start = avatarMove != null ? avatarMove.CurrentCell : FindNavigationCell(navigationStartRoom);

            bool found = avatarMove != null && avatarMove.IsReady
            ? avatarMove.TryFindPath(destination, out navigationPreview)
            : DungeonPathfinder.TryFind(
                generatedFloor,
                start,
                destination,
                out navigationPreview);

            if (!found)
            {
                Debug.LogWarning(
                    $"No existe una ruta transitable hasta {destination}.",
                    this);
                return;
            }

            if (avatarMove != null) avatarMove.Follow(navigationPreview);

            Debug.Log(
                $"Destino seleccionado: {destination}. " +
                $"Ruta de {navigationPreview.Count - 1} tramos.",
                this);
        }
        catch (ArgumentException exception)
        {
            navigationPreview = null;
            Debug.LogError(exception.Message, this);
        }
    }

    private void GenerateFloor(bool includeCorridors)
    {
        corridorPreview = null;
        navigationPreview = null;

        if (Application.isPlaying && avatarMove != null)
            avatarMove.ResetNavigation();

        try
        {
            generatedFloor = GenerateData(includeCorridors);

            if (Application.isPlaying && roomView != null)
            {
                roomView.Build(generatedFloor);

                bool canNavigate = includeCorridors && (!useBsp || buildCorridors);

                if (canNavigate && avatarMove != null)
                {
                    avatarMove.Initialize(generatedFloor, settings, FindNavigationCell(navigationStartRoom));
                }
            }

            //RectInt bounds = generatedFloor.rooms[0].bounds;

            Debug.Log(
                $"Semilla: {generatedFloor.seed}. " +
                $"Habitaciones: {generatedFloor.rooms.Count}. " +
                $"Pasillos: {generatedFloor.corridors.Count}/" +
                $"{generatedFloor.plannedConnections.Count}. " +
                $"Intentos de pasillos: {generatedFloor.corridorBuildAttempts}. " +
                $"Celdas: {generatedFloor.cells.Count}.",
                this);
        }
        catch (ArgumentException exception)
        {
            generatedFloor = null;

            if (Application.isPlaying && avatarMove != null)
                avatarMove.ResetNavigation();

            if (Application.isPlaying && roomView != null)
                roomView.Clear();

            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar aislamiento de pasillos")]
    private void CheckCorridorIsolation()
    {
        if (!useBsp)
        {
            Debug.LogWarning(
                "Activa Use BSP para esta comprobación.", this);
            return;
        }

        DungeonFloorData layout = null;
        string before = null;

        try
        {
            layout = GenerateData(false);
            before = JsonUtility.ToJson(layout);

            DungeonFloorData result = DungeonCorridorBuilder.Build(
                layout,
                maximumCorridorAttempts);

            Debug.Log(
                $"Construcción completada en " +
                $"{result.corridorBuildAttempts} intentos.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning(
                $"La construcción no terminó: {exception.Message}",
                this);
        }
        finally
        {
            if (layout != null && before != null)
            {
                bool unchanged = before == JsonUtility.ToJson(layout);

                if (unchanged)
                {
                    Debug.Log(
                        "Aislamiento correcto: la distribución original " +
                        "no ha cambiado.",
                        this);
                }
                else
                {
                    Debug.LogError(
                        "La construcción ha modificado la distribución original.",
                        this);
                }
            }
        }
    }

    [ContextMenu("Previsualizar pasillo")]
    private void PreviewCorridor()
    {
        corridorPreview = null;

        if (!useBsp)
        {
            Debug.LogWarning(
                "Activa Use BSP para previsualizar un pasillo.", this);
            return;
        }

        if (settings.minimumOpeningCells != 1)
        {
            Debug.LogWarning(
                "Este primer trazador requiere Minimum Opening Cells = 1.",
                this);
            return;
        }

        GenerateFloor(false);

        if (generatedFloor == null)
            return;

        try
        {
            if (connectionToPreview < 0 ||
                connectionToPreview >= generatedFloor.plannedConnections.Count)
            {
                throw new ArgumentException(
                    "El índice de conexión no existe en este piso.");
            }

            RoomConnection connection =
                generatedFloor.plannedConnections[connectionToPreview];

            bool found = SimpleCorridorPlanner.TryFind(
                generatedFloor,
                connection,
                out corridorPreview);

            if (!found)
            {
                Debug.LogWarning(
                    $"No hay una ruta válida entre las salas " +
                    $"{connection.roomA} y {connection.roomB} " +
                    "con las restricciones actuales.",
                    this);
                return;
            }

            Debug.Log(
                $"Pasillo propuesto: {connection.roomA} -> " +
                $"{connection.roomB}, " +
                $"{corridorPreview.cells.Count} casillas exteriores.",
                this);
        }
        catch (ArgumentException exception)
        {
            corridorPreview = null;
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Previsualizar navegación")]
    private void PreviewNavigation()
    {
        navigationPreview = null;

        if (!useBsp || !buildCorridors)
        {
            Debug.LogWarning(
                "Activa Use BSP y Build Corridors para esta comprobación.",
                this);
            return;
        }

        GenerateFloor(true);

        if (generatedFloor == null)
            return;

        try
        {
            Vector2Int start = FindNavigationCell(navigationStartRoom);
            Vector2Int destination =
                FindNavigationCell(navigationDestinationRoom);

            if (!DungeonPathfinder.TryFind(
                    generatedFloor,
                    start,
                    destination,
                    out navigationPreview))
            {
                Debug.LogWarning(
                    "No existe una ruta transitable entre los puntos elegidos.",
                    this);
                return;
            }

            Debug.Log(
                $"Ruta encontrada: {navigationPreview.Count - 1} pasos, " +
                $"desde {start} hasta {destination}.",
                this);
        }
        catch (ArgumentException exception)
        {
            navigationPreview = null;
            Debug.LogError(exception.Message, this);
        }
    }

    private Vector2Int FindNavigationCell(int roomId)
    {
        return DungeonRoomTypeAssigner.FindReferenceCell(generatedFloor, roomId);
    }

    [ContextMenu("Comprobar rodeo BFS")]
    private void CheckCorridorBfs()
    {
        try
        {
            var connection = new RoomConnection
            {
                roomA = 0,
                roomB = 1
            };

            DungeonFloorData open = CreateCorridorTestFloor(false);

            if (!SimpleCorridorPlanner.TryFind(
                    open, connection, out Corridor first))
            {
                throw new ArgumentException(
                    "BFS no encontró el rodeo que debería existir.");
            }

            if (!SimpleCorridorPlanner.TryFind(
                    open, connection, out Corridor second) ||
                JsonUtility.ToJson(first) != JsonUtility.ToJson(second))
            {
                throw new ArgumentException(
                    "El rodeo no es reproducible.");
            }

            int turns = 0;

            for (int i = 2; i < first.cells.Count; i++)
            {
                Vector2Int before = first.cells[i - 1] - first.cells[i - 2];
                Vector2Int after = first.cells[i] - first.cells[i - 1];

                if (before != after)
                    turns++;
            }

            if (turns < 2)
            {
                throw new ArgumentException(
                    "La prueba no ha producido el rodeo esperado.");
            }

            DungeonFloorData closed = CreateCorridorTestFloor(true);

            if (SimpleCorridorPlanner.TryFind(
                    closed, connection, out Corridor unexpected))
            {
                throw new ArgumentException(
                    "El trazador ha atravesado una barrera que cierra el área.");
            }

            Debug.Log(
                $"BFS correcto: rodeo de {first.cells.Count} casillas, " +
                $"{turns} giros, resultado reproducible " +
                "y barrera completa rechazada.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private static DungeonFloorData CreateCorridorTestFloor(
        bool blockWholeHeight)
    {
        var floor = new DungeonFloorData
        {
            cellSize = 1f
        };

        floor.regions.Add(new RectInt(0, 0, 17, 11));

        RectInt[] bounds =
        {
        new RectInt(1, 4, 3, 3),
        new RectInt(13, 4, 3, 3),
        blockWholeHeight
            ? new RectInt(7, 0, 3, 11)
            : new RectInt(7, 3, 3, 5)
    };

        for (int id = 0; id < bounds.Length; id++)
        {
            RectInt rectangle = bounds[id];

            floor.rooms.Add(new RoomData
            {
                id = id,
                bounds = rectangle
            });

            for (int y = rectangle.yMin; y < rectangle.yMax; y++)
            {
                for (int x = rectangle.xMin; x < rectangle.xMax; x++)
                {
                    floor.cells.Add(new DungeonCellData
                    {
                        coordinate = new Vector2Int(x, y),
                        roomId = id,
                        occupancy = CellOccupancy.None
                    });
                }
            }
        }

        floor.rooms[0].doorCandidates.Add(new RoomDoorCandidate
        {
            startCell = new Vector2Int(3, 5),
            outward = Vector2Int.right,
            width = 1
        });

        floor.rooms[1].doorCandidates.Add(new RoomDoorCandidate
        {
            startCell = new Vector2Int(13, 5),
            outward = Vector2Int.left,
            width = 1
        });

        return floor;
    }

    [ContextMenu("Comprobar repetibilidad")]
    private void CheckRepeatability()
    {
        try
        {
            var first = GenerateData();
            var second = GenerateData();

            bool same = JsonUtility.ToJson(first) ==
                        JsonUtility.ToJson(second);

            if (same)
                Debug.Log("Repetibilidad correcta.", this);
            else
                Debug.LogError("La generación no se repite.", this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar distribución BSP")]
    private void CheckDistribution()
    {
        if (!useBsp)
        {
            Debug.LogWarning("Activa Use BSP para esta comprobación.", this);
            return;
        }

        try
        {
            DungeonFloorData floor = GenerateData(false);

            if (floor.rooms.Count != roomCount ||
                floor.regions.Count != roomCount)
            {
                throw new ArgumentException(
                    "La cantidad de habitaciones o regiones no coincide.");
            }

            bool valid = true;
            var coordinates = new HashSet<Vector2Int>();

            for (int i = 0; i < floor.rooms.Count; i++)
            {
                RoomData room = floor.rooms[i];
                RectInt region = floor.regions[i];

                valid &= room.id == i;
                valid &= room.bounds.xMin >= region.xMin + roomMargin;
                valid &= room.bounds.yMin >= region.yMin + roomMargin;
                valid &= room.bounds.xMax <= region.xMax - roomMargin;
                valid &= room.bounds.yMax <= region.yMax - roomMargin;

                for (int j = 0; j < i; j++)
                    valid &= !room.bounds.Overlaps(floor.rooms[j].bounds);
            }

            foreach (DungeonCellData cell in floor.cells)
            {
                valid &= coordinates.Add(cell.coordinate);

                valid &= cell.roomId >= 0 &&
                         cell.roomId < floor.rooms.Count &&
                         floor.rooms[cell.roomId].bounds.Contains(cell.coordinate);
            }

            if (valid)
                Debug.Log("Distribución correcta: IDs, márgenes y ocupación.", this);
            else
                Debug.LogError("La distribución no cumple sus condiciones.", this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private DungeonFloorData GenerateData(bool includeCorridors = true)
    {
        if (useBsp)
        {
            DungeonFloorData layout = BSPFloorGenerator.Generate(
                settings,
                roomPresets,
                bspArea,
                minimumRegionSide,
                roomCount,
                roomMargin);

            if (includeCorridors && buildCorridors)
            {
                if (settings.minimumOpeningCells != 1)
                {
                    throw new ArgumentException(
                        "El constructor actual requiere puertas de una casilla.");
                }

                DungeonFloorData floor = DungeonCorridorBuilder.Build(
                    layout,
                    maximumCorridorAttempts);

                DungeonRoomTypeAssigner.Assign(floor, navigationStartRoom);
                DungeonKeyPlanner.Assign(floor);

                int assignedChests = DungeonRoomTypeAssigner.AssignChests(
                    floor,
                    chestRoomCount);

                if (assignedChests < chestRoomCount)
                {
                    Debug.Log(
                        $"Cofres: se asignaron {assignedChests} de " +
                        $"{chestRoomCount} habitaciones solicitadas " +
                        "por falta de candidatas.",
                        this);
                }

                int assignedHealing = DungeonRoomTypeAssigner.AssignHealing(
                    floor,
                    healingRoomCount);

                if (assignedHealing < healingRoomCount)
                {
                    Debug.Log(
                        $"Curación: se asignaron {assignedHealing} de " +
                        $"{healingRoomCount} habitaciones solicitadas " +
                        "por falta de candidatas.",
                        this);
                }

                int assignedUpgrades = DungeonRoomTypeAssigner.AssignUpgrades(
                floor,
                upgradeRoomCount);

                if (assignedUpgrades < upgradeRoomCount)
                {
                    Debug.Log(
                        $"Mejoras: se asignaron {assignedUpgrades} de " +
                        $"{upgradeRoomCount} habitaciones solicitadas " +
                        "por falta de candidatas.",
                        this);
                }

                int assignedEvents = DungeonRoomTypeAssigner.AssignEvents(floor, eventRoomCount);

                if (assignedEvents < eventRoomCount)
                {
                    Debug.Log(
                        $"Eventos: se asignaron {assignedEvents} de " +
                        $"{eventRoomCount} habitaciones solicitadas " +
                        "por falta de candidatas compatibles.",
                        this);
                }

                DungeonRoomTypeAssigner.AssignRemaining(
                floor,
                combatProbability);

                DungeonContentReservations.Build(floor);
                DungeonPassageReservations.Build(floor);
                DungeonRoomRouteReservations.Build(floor);

                return floor;
            }

            return layout;
        }

        return SimpleDungeonGenerator.Generate(
            settings, preset, roomOrigin, quarterTurns);
    }

    [ContextMenu("Comprobar red reservada")]
    private void CheckReservedNetwork()
    {
        if (generatedFloor == null)
        {
            Debug.LogWarning("Genera primero un piso.", this);
            return;
        }

        try
        {
            RoomData startRoom = generatedFloor.rooms.Find(
                room => room.type == DungeonRoomType.Start);

            if (startRoom == null || !startRoom.hasReservedCenter)
                throw new ArgumentException("Falta el inicio reservado.");

            var blockedCenters = new HashSet<Vector2Int>();

            foreach (RoomData room in generatedFloor.rooms)
            {
                if (room.hasReservedCenter &&
                    room.type != DungeonRoomType.Start)
                {
                    blockedCenters.Add(room.reservedCenter);
                }
            }

            var testFloor = new DungeonFloorData
            {
                cellSize = generatedFloor.cellSize
            };

            foreach (DungeonCellData original in generatedFloor.cells)
            {
                DungeonCellData copy = original;

                if ((copy.occupancy & CellOccupancy.Reserved) == 0 ||
                    blockedCenters.Contains(copy.coordinate))
                {
                    copy.occupancy |= CellOccupancy.Blocked;
                }

                testFloor.cells.Add(copy);
            }

            var reachable = DungeonDistanceMap.Build(
                testFloor,
                startRoom.reservedCenter);

            foreach (Corridor corridor in generatedFloor.corridors)
            {
                if (!reachable.ContainsKey(corridor.doorA.startCell) ||
                    !reachable.ContainsKey(corridor.doorB.startCell))
                {
                    throw new ArgumentException(
                        "Una puerta no es alcanzable utilizando solo reservas.");
                }
            }

            if (!reachable.ContainsKey(generatedFloor.keyCell))
            {
                throw new ArgumentException(
                    "La llave no es alcanzable utilizando solo reservas.");
            }

            foreach (Vector2Int center in blockedCenters)
            {
                bool hasAccess =
                    reachable.ContainsKey(center + Vector2Int.up) ||
                    reachable.ContainsKey(center + Vector2Int.right) ||
                    reachable.ContainsKey(center + Vector2Int.down) ||
                    reachable.ContainsKey(center + Vector2Int.left);

                if (!hasAccess)
                {
                    throw new ArgumentException(
                        $"El contenido de {center} no tiene aproximación accesible.");
                }
            }

            Debug.Log(
                "Red reservada correcta: puertas, llave y aproximaciones " +
                "accesibles incluso bloqueando los centros especiales.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar acceso a la llave")]
    private void CheckKeyAccess()
    {
        if (generatedFloor == null || generatedFloor.keyRoomId < 0)
        {
            Debug.LogWarning(
                "Genera primero un piso con la llave planificada.",
                this);
            return;
        }

        try
        {
            RoomData startRoom = generatedFloor.rooms.Find(
                room => room.type == DungeonRoomType.Start);

            RoomData stairsRoom = generatedFloor.rooms.Find(
                room => room.type == DungeonRoomType.Stairs);

            if (startRoom == null || stairsRoom == null)
                throw new ArgumentException("Faltan inicio o escaleras.");

            Vector2Int start =
                DungeonRoomTypeAssigner.FindReferenceCell(
                    generatedFloor,
                    startRoom.id);

            string before = JsonUtility.ToJson(generatedFloor);

            var reachable = DungeonDistanceMap.Build(
                generatedFloor,
                start,
                stairsRoom.id);

            if (before != JsonUtility.ToJson(generatedFloor))
            {
                throw new ArgumentException(
                    "La consulta de accesibilidad ha modificado el piso.");
            }

            bool belongsToRoom = generatedFloor.cells.Exists(
                cell =>
                    cell.coordinate == generatedFloor.keyCell &&
                    cell.roomId == generatedFloor.keyRoomId);

            if (!belongsToRoom ||
                generatedFloor.keyRoomId == stairsRoom.id ||
                !reachable.TryGetValue(
                    generatedFloor.keyCell,
                    out int distance))
            {
                throw new ArgumentException(
                    "La ubicación de la llave no es válida o no es alcanzable.");
            }

            Debug.Log(
                $"Llave en habitación {generatedFloor.keyRoomId}, " +
                $"casilla {generatedFloor.keyCell}: accesible en " +
                $"{distance} pasos sin entrar en las escaleras.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar grafo de conexiones")]
    private void CheckConnectionGraph()
    {
        if (!useBsp)
        {
            Debug.LogWarning(
                "Activa Use BSP para esta comprobación.", this);
            return;
        }

        try
        {
            DungeonFloorData floor = GenerateData(false);
            int count = floor.rooms.Count;

            if (count == 0 ||
                floor.plannedConnections.Count - (count - 1) !=
                settings.extraConnections)
            {
                throw new ArgumentException(
                    "La cantidad de conexiones no coincide con la configuración.");
            }

            var neighbours = new List<int>[count];
            var uniqueConnections = new HashSet<(int, int)>();

            for (int i = 0; i < count; i++)
                neighbours[i] = new List<int>();

            foreach (RoomConnection connection in floor.plannedConnections)
            {
                int a = connection.roomA;
                int b = connection.roomB;

                if (a < 0 || a >= count ||
                    b < 0 || b >= count ||
                    a == b)
                {
                    throw new ArgumentException(
                        "Hay una conexión con extremos inválidos.");
                }

                var key = (Math.Min(a, b), Math.Max(a, b));

                if (!uniqueConnections.Add(key))
                {
                    throw new ArgumentException(
                        "Hay una conexión repetida.");
                }

                neighbours[a].Add(b);
                neighbours[b].Add(a);
            }

            var visited = new HashSet<int>();
            var pending = new Queue<int>();

            visited.Add(0);
            pending.Enqueue(0);

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();

                foreach (int neighbour in neighbours[current])
                {
                    if (visited.Add(neighbour))
                        pending.Enqueue(neighbour);
                }
            }

            if (visited.Count != count)
            {
                throw new ArgumentException(
                    "Hay habitaciones desconectadas.");
            }

            bool hasLeaf = false;

            for (int i = 0; i < count; i++)
            {
                if (neighbours[i].Count == 1)
                    hasLeaf = true;
            }

            if (count > 1 && !hasLeaf)
            {
                throw new ArgumentException(
                    "No se ha conservado ninguna habitación con una sola conexión.");
            }

            Debug.Log(
                $"Grafo correcto: {count} habitaciones, " +
                $"{floor.plannedConnections.Count} conexiones, " +
                $"{settings.extraConnections} extras. " +
                "Conectividad y condición de hoja correctas.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar navegación con obstáculos")]
    private void CheckNavigationObstacles()
    {
        try
        {
            var floor = new DungeonFloorData { cellSize = 1f };

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    CellOccupancy occupancy = CellOccupancy.None;

                    if (x == 1)
                    {
                        occupancy = y == 0
                            ? CellOccupancy.Blocked
                            : CellOccupancy.Reserved;
                    }

                    floor.cells.Add(new DungeonCellData
                    {
                        coordinate = new Vector2Int(x, y),
                        roomId = 0,
                        occupancy = occupancy
                    });
                }
            }

            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int destination = new Vector2Int(2, 0);

            bool found = DungeonPathfinder.TryFind(
                floor, start, destination, out List<Vector2Int> path);

            if (!found ||
                path.Count != 5 ||
                path[0] != start ||
                path[path.Count - 1] != destination ||
                path.Contains(new Vector2Int(1, 0)) ||
                !path.Contains(new Vector2Int(1, 1)))
            {
                throw new ArgumentException(
                    "La ruta no ha respetado el rodeo y la reserva.");
            }

            DungeonCellData cell = floor.cells[4];
            cell.occupancy |= CellOccupancy.Blocked;
            floor.cells[4] = cell;

            if (DungeonPathfinder.TryFind(
                    floor, start, destination, out _))
            {
                throw new ArgumentException(
                    "Se ha encontrado una ruta a través de una barrera cerrada.");
            }

            if (DungeonPathfinder.TryFind(
                    floor, start, new Vector2Int(20, 20), out _))
            {
                throw new ArgumentException(
                    "Se ha aceptado un destino sin suelo.");
            }

            Debug.Log(
                "Navegación correcta: rodeo, reserva transitable, " +
                "bloqueo combinado y destino inválido.",
                this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (generatedFloor == null || generatedFloor.cells == null || generatedFloor.cellSize <= 0f)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.cyan;

        float size = generatedFloor.cellSize;

        foreach (DungeonCellData cell in generatedFloor.cells)
        {
            Vector3 center = generatedFloor.CellCenter(cell.coordinate);

            Gizmos.DrawWireCube(
                center,
                new Vector3(size * 0.95f, 0.02f, size * 0.95f));
        }

        Gizmos.color = Color.yellow;

        foreach (RoomData room in generatedFloor.rooms)
        {
            if (room.doorCandidates == null)
                continue;

            foreach (RoomDoorCandidate door in room.doorCandidates)
            {
                Vector3 outward =
                    new Vector3(door.outward.x, 0f, door.outward.y);

                Vector3 along =
                    new Vector3(door.Along.x, 0f, door.Along.y);

                Vector3 center =
                    generatedFloor.CellCenter(door.startCell);

                center += along * ((door.width - 1) * size * 0.5f);

                center += outward * (size * 0.5f);
                center += Vector3.up * 0.05f;

                Vector3 halfWidth =
                    along * (door.width * size * 0.5f);

                Gizmos.DrawLine(center - halfWidth, center + halfWidth);
                Gizmos.DrawRay(center, outward * size * 0.4f);
            }
        }

        Gizmos.color = Color.magenta;

        if (generatedFloor.plannedConnections != null)
        {
            int treeConnectionCount = generatedFloor.rooms.Count - 1;

            for (int i = 0; i < generatedFloor.plannedConnections.Count; i++)
            {
                RoomConnection connection =
                    generatedFloor.plannedConnections[i];

                Gizmos.color = i < treeConnectionCount
                    ? Color.magenta
                    : Color.green;

                Vector2 centerA = RoomConnectionPlanner.RoomCenter(
                    generatedFloor.rooms[connection.roomA]);

                Vector2 centerB = RoomConnectionPlanner.RoomCenter(
                    generatedFloor.rooms[connection.roomB]);

                Vector3 worldA = new Vector3(
                    centerA.x * size, 2.2f, centerA.y * size);

                Vector3 worldB = new Vector3(
                    centerB.x * size, 2.2f, centerB.y * size);

                Gizmos.DrawLine(worldA, worldB);
            }
        }

        if (corridorPreview != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);

            foreach (Vector2Int coordinate in corridorPreview.cells)
            {
                Vector3 center = generatedFloor.CellCenter(coordinate);

                Gizmos.DrawWireCube(
                    center + Vector3.up * 0.1f,
                    new Vector3(size * 0.9f, 0.1f, size * 0.9f));
            }

            Gizmos.color = Color.white;

            Vector3 entrance = generatedFloor.CellCenter(
                corridorPreview.doorA.startCell);

            Vector3 exit = generatedFloor.CellCenter(
                corridorPreview.doorB.startCell);

            Gizmos.DrawWireCube(
                entrance + Vector3.up * 0.15f,
                new Vector3(size * 0.7f, 0.2f, size * 0.7f));

            Gizmos.DrawWireCube(
                exit + Vector3.up * 0.15f,
                new Vector3(size * 0.7f, 0.2f, size * 0.7f));
        }

        if (navigationPreview != null && navigationPreview.Count > 0)
        {
            Gizmos.color = Color.blue;

            Vector3 offset = Vector3.up * 2.4f;

            for (int i = 1; i < navigationPreview.Count; i++)
            {
                Vector3 previous = generatedFloor.CellCenter(
                    navigationPreview[i - 1]) + offset;

                Vector3 current = generatedFloor.CellCenter(
                    navigationPreview[i]) + offset;

                Gizmos.DrawLine(previous, current);
            }

            Vector3 start = generatedFloor.CellCenter(
                navigationPreview[0]) + offset;

            Vector3 destination = generatedFloor.CellCenter(
                navigationPreview[navigationPreview.Count - 1]) + offset;

            Gizmos.DrawWireSphere(start, size * 0.2f);
            Gizmos.DrawWireSphere(destination, size * 0.2f);
        }

        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }

    private void OnEnable()
    {
        if (avatarMove != null)
            avatarMove.RoomChanged += HandleRoomChanged;
    }

    [ContextMenu("Comprobar distancias desde el inicio")]
    private void CheckDistancesFromStart()
    {
        if (generatedFloor == null)
        {
            Debug.LogWarning("Genera primero un piso.", this);
            return;
        }

        try
        {
            Vector2Int start =
                FindNavigationCell(navigationStartRoom);

            Dictionary<Vector2Int, int> distances =
                DungeonDistanceMap.Build(generatedFloor, start);

            bool valid = true;

            foreach (RoomData room in generatedFloor.rooms)
            {
                Vector2Int destination = FindNavigationCell(room.id);

                if (!distances.TryGetValue(destination, out int distance))
                {
                    valid = false;

                    Debug.LogError(
                        $"Habitación {room.id}: su punto de referencia " +
                        "es inalcanzable desde el inicio.",
                        this);

                    continue;
                }

                bool found = DungeonPathfinder.TryFind(
                    generatedFloor,
                    start,
                    destination,
                    out List<Vector2Int> path);

                if (!found || path.Count - 1 != distance)
                {
                    valid = false;

                    Debug.LogError(
                        $"Habitación {room.id}: BFS y A* no coinciden.",
                        this);

                    continue;
                }

                if (room.type != DungeonRoomType.Unassigned && room.distanceFromStart != distance)
                {
                    valid = false;

                    Debug.LogError(
                        $"La distancia guardada de la habitación {room.id} " +
                        "no coincide con el cálculo actual.",
                        this);
                }

                Debug.Log(
                $"Habitación {room.id}: tipo {room.type}, " +
                $"distancia guardada {room.distanceFromStart}, " +
                $"distancia comprobada {distance}, " +
                $"casilla de referencia {destination}.",
                this);
            }

            if (valid)
            {
                Debug.Log(
                    "Distancias comprobadas: todos los puntos de " +
                    "referencia de las habitaciones son alcanzables " +
                    "y coinciden con A*.",
                    this);
            }
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void HandleRoomChanged(
        int? previousRoomId,
        int? currentRoomId)
    {
        if (previousRoomId.HasValue)
        {
            Debug.Log(
                $"Salida de la habitación {previousRoomId.Value}.",
                this);
        }

        if (currentRoomId.HasValue)
        {
            Debug.Log(
                $"Entrada en la habitación {currentRoomId.Value}.",
                this);
        }
    }
}
