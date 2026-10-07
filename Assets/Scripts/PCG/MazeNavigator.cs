using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MazeMapView))]
public sealed class MazeNavigator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform avatar;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private CameraFollowNode cameraFollow;

    [Header("Interaction")]
    [SerializeField] private TMP_Text interactionLabel;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(1f)] private float turnSpeed = 720f;

    [Header("Exploration")]
    [SerializeField] private string floorLayerName = "MazeCell";

    public event Action<CellType> CellEntered;

    private static readonly int RunningId =
        Animator.StringToHash("Running");

    private readonly HashSet<Vector2Int> revealed = new();

    private MazeMapView map;
    private Animator animator;
    private Vector2Int active;

    private Coroutine movement;
    private bool ready;
    private bool moving;
    private int floorMask;

    private void Awake()
    {
        map = GetComponent<MazeMapView>();

        if (avatar != null)
            animator = avatar.GetComponentInChildren<Animator>(true);

        if (inputCamera == null)
            inputCamera = Camera.main;

        if (cameraFollow == null && inputCamera != null)
            cameraFollow = inputCamera.GetComponent<CameraFollowNode>();
    }

    private void OnEnable()
    {
        map.MapReady += Initialize;
        map.MapCleared += ResetNavigation;

        if (map.IsReady)
            Initialize();
    }

    private void OnDisable()
    {
        map.MapReady -= Initialize;
        map.MapCleared -= ResetNavigation;
        ResetNavigation();
    }

    private void Initialize()
    {
        ResetNavigation();

        if (avatar == null || inputCamera == null)
        {
            Debug.LogError("Asigna Avatar e Input Camera en MazeNavigator.");
            return;
        }

        floorMask = LayerMask.GetMask(floorLayerName);

        if (floorMask == 0)
        {
            Debug.LogError($"No existe la layer '{floorLayerName}'.");
            return;
        }

        active = Vector2Int.zero;

        if (!map.Cells.TryGetValue(active, out MazeCellView start))
        {
            Debug.LogError("No existe la celda inicial (0,0).");
            return;
        }

        avatar.position = start.ArrivalPosition;
        avatar.rotation = Quaternion.identity;
        avatar.gameObject.SetActive(true);

        if (animator != null)
            animator.applyRootMotion = false;

        SetRunning(false);

        ready = true;
        RefreshDiscovery();

        if (cameraFollow != null)
            cameraFollow.SetTarget(avatar);
    }

    private bool TryGetInteractableCell(out MazeCellView cell)
    {
        cell = null;

        return ready &&
               !moving &&
               map.Cells.TryGetValue(active, out cell) &&
               cell.CanInteract;
    }

    private void LateUpdate()
    {
        if (interactionLabel == null)
            return;

        bool show = TryGetInteractableCell(out MazeCellView cell);
        interactionLabel.gameObject.SetActive(show);

        if (!show)
            return;

        interactionLabel.text = "E";
        interactionLabel.transform.position = cell.InteractionPosition;

        interactionLabel.transform.rotation = inputCamera.transform.rotation;
    }

    private void ResetNavigation()
    {
        if (interactionLabel != null)
            interactionLabel.gameObject.SetActive(false);

        ready = false;
        moving = false;

        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        SetRunning(false);
        revealed.Clear();

        if (cameraFollow != null)
            cameraFollow.ClearTarget();

        if (avatar != null)
            avatar.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!ready || moving)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.eKey.wasPressedThisFrame &&
            TryGetInteractableCell(out MazeCellView cell))
        {
            if (cell.TryClearContent())
            {
                if (interactionLabel != null)
                    interactionLabel.gameObject.SetActive(false);

                moving = true;
                movement = StartCoroutine(MoveToClearedCenter(cell));
                return;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            1000f,
            floorMask,
            QueryTriggerInteraction.Ignore))
            return;

        MazeCellView clicked =
            hit.collider.GetComponentInParent<MazeCellView>();

        if (clicked == null)
            return;

        Vector2Int destination = clicked.Coordinate;

        // La conexión del plano es la autoridad sobre el movimiento.
        if (!map.Generator.Layout.CanMove(active, destination))
            return;

        moving = true;
        movement = StartCoroutine(MoveTo(destination));
    }

    private IEnumerator MoveTo(Vector2Int destination)
    {
        MazeCellView source = map.Cells[active];
        MazeCellView target = map.Cells[destination];

        Vector2Int direction = destination - active;

        int exit = ExitIndex(direction);
        int entry = (exit + 4) % 8;

        //List<Vector3> path = new List<Vector3>();
        //source.AppendRingPath(path, 0, exit);
        //path.Add(target.RingPoint(entry));
        //target.AppendRingPath(path, entry, 0);
        List<Vector3> path = new List<Vector3>();

        if (!source.HasCenterObstacle && !target.HasCenterObstacle)
        {
            // Ambas están vacías:
            path.Add(target.ArrivalPosition);
        }
        else
        {
            if (source.HasCenterObstacle)
            {
                // Rodear 
                source.AppendRingPath(path, 0, exit);
            }
            else
            {
                // Ir recto desde el centro hacia la salida.
                path.Add(source.RingPoint(exit));
            }

            // Cruzar el paso entre habitaciones.
            path.Add(target.RingPoint(entry));

            if (target.HasCenterObstacle)
            {
                // Rodear el objeto de la habitación de destino.
                target.AppendRingPath(path, entry, 0);
            }
            else
            {
                // Llegar directamente al centro.
                path.Add(target.ArrivalPosition);
            }
        }

        yield return WalkPath(path);

        active = destination;
        moving = false;
        movement = null;

        RefreshDiscovery();

        // Todo: eventos
        CellEntered?.Invoke(target.Type);
    }

    private IEnumerator WalkPath(List<Vector3> path)
    {
        SetRunning(true);

        foreach (Vector3 waypoint in path)
        {
            while ((avatar.position - waypoint).sqrMagnitude > 0.0001f)
            {
                Vector3 directionToPoint = waypoint - avatar.position;
                directionToPoint.y = 0f;

                if (directionToPoint.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRotation =
                        Quaternion.LookRotation(directionToPoint);

                    avatar.rotation = Quaternion.RotateTowards(
                        avatar.rotation,
                        desiredRotation,
                        turnSpeed * Time.deltaTime);
                }

                avatar.position = Vector3.MoveTowards(
                    avatar.position,
                    waypoint,
                    moveSpeed * Time.deltaTime);

                yield return null;
            }

            avatar.position = waypoint;
        }

        SetRunning(false);
    }

    private IEnumerator MoveToClearedCenter(MazeCellView cell)
    {
        yield return WalkPath(new List<Vector3>
        {
            cell.CenterPosition
        });

        moving = false;
        movement = null;
    }

    private static int ExitIndex(Vector2Int direction)
    {
        if (direction == Vector2Int.down) return 1;
        if (direction == Vector2Int.right) return 3;
        if (direction == Vector2Int.up) return 5;
        return 7;
    }

    private void RefreshDiscovery()
    {
        //Dictionary<Vector2Int, int> distances =
        //    map.Generator.Layout.Distances(active, revealRadius);

        //foreach (KeyValuePair<Vector2Int, int> pair in distances)
        //{
        //    if (pair.Value < revealRadius)
        //        revealed.Add(pair.Key);
        //}

        //foreach (KeyValuePair<Vector2Int, MazeCellView> pair in map.Cells)
        //{
        //    bool discovered = revealed.Contains(pair.Key);

        //    bool penumbra =
        //        !discovered &&
        //        distances.TryGetValue(pair.Key, out int distance) &&
        //        distance == revealRadius;

        //    pair.Value.SetDiscovery(discovered, penumbra);
        //}
        //revealed.Add(active);

        //foreach (KeyValuePair<Vector2Int, MazeCellView> pair in map.Cells)
        //{
        //    bool discovered = revealed.Contains(pair.Key);

        //    pair.Value.SetDiscovery(discovered, penumbra: false);
        //}
        revealed.Add(active);

        List<FogParticles.FogPoint> fogPoints =
            new List<FogParticles.FogPoint>();

        foreach (KeyValuePair<Vector2Int, MazeCellView> pair in map.Cells)
        {
            bool discovered = revealed.Contains(pair.Key);

            pair.Value.SetDiscovery(
                discovered,
                penumbra: false);

            if (map.UseParticleFog && !discovered)
            {
                fogPoints.Add(new FogParticles.FogPoint
                {
                    position = pair.Value.CenterPosition,
                    density = 1f
                });
            }
        }

        map.SetParticleCoverage(fogPoints);
    }

    private void SetRunning(bool value)
    {
        if (animator != null)
            animator.SetBool(RunningId, value);
    }
}