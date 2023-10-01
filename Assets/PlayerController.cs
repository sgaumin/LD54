using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Facade;

public enum PlayerMoveType
{
    Normal,
    Ascend,
    Descend
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStartLinePointsSO startData;

    [SerializeField] private float defaultSize = 0.5f;

    [Header("References")]
    [SerializeField] private Transform lineHolder;
    [SerializeField] private Transform collidersHolder;

    [Header("Debug")]
    [SerializeField] private Vector2 debugSplitPoint;

    private int currentStratumIndex;
    private List<BoxCollider2D> lineColliders = new List<BoxCollider2D>();
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private List<PlayerLinePoint> linePoints;

    private void Awake()
    {
        InputManager.OnTopEvent += MoveTop;
        InputManager.OnDownEvent += MoveDown;
        InputManager.OnLeftEvent += MoveLeft;
        InputManager.OnRightEvent += MoveRight;
        InputManager.OnAction1Event += Ascend;
        InputManager.OnAction2Event += Descend;

        StratumManager.OnStratumChanged += UpdateVisuals;

        // Assign start positions
        if (startData != null)
        {
            linePoints = startData.LinePoints.ToList();
            RegisterPositions();

            // Set starting stratum index
            currentStratumIndex = startData.LinePoints[^1].StratumIndex;
        }
    }

    public void Initialize(List<PlayerLinePoint> linePoints)
    {
        this.linePoints = linePoints;
        RegisterPositions();

        currentStratumIndex = linePoints[^1].StratumIndex;
        UpdateVisuals();
    }

    private void OnDestroy()
    {
        InputManager.OnTopEvent -= MoveTop;
        InputManager.OnDownEvent -= MoveDown;
        InputManager.OnLeftEvent -= MoveLeft;
        InputManager.OnRightEvent -= MoveRight;
        InputManager.OnAction1Event -= Ascend;
        InputManager.OnAction2Event -= Descend;

        StratumManager.OnStratumChanged -= UpdateVisuals;

        ReturnLineRenderers();
    }

    private List<Vector3[]> GetLines(int stratumIndex = -1)
    {
        void Register(List<Vector3[]> lines, List<Vector3> positions)
        {
            if (positions.Count > 0)
            {
                // We add a duplicate in order to make this point visible
                if (positions.Count == 1)
                    positions.Add(positions[0]);

                lines.Add(positions.ToArray());
                positions.Clear();
            }
        }

        if (stratumIndex == -1)
            stratumIndex = Stratums.CurrentStratumIndex;

        List<Vector3[]> lines = new List<Vector3[]>();
        List<Vector3> positions = new List<Vector3>();
        foreach (PlayerLinePoint linePoint in linePoints)
        {
            if (linePoint.StratumIndex != stratumIndex)
            {
                Register(lines, positions);
                continue;
            }

            positions.Add(linePoint.Position);
        }

        Register(lines, positions);

        return lines;
    }

    private void UpdateVisuals(int stratumIndex = -1)
    {
        ReturnLineRenderers();
        RemoveColliders();

        // If using default, get current stratum index
        if (stratumIndex == -1)
            stratumIndex = Stratums.CurrentStratumIndex;

        List<Vector3[]> lines = GetLines(stratumIndex);
        foreach (Vector3[] line in lines)
        {
            bool first = lines.IndexOf(line) == 0;
            bool last = lines.IndexOf(line) == lines.Count - 1;

            // Get a line from pool, and update its visuals
            LineRenderer lineRenderer = Lines.Get();
            lineRenderers.Add(lineRenderer);
            lineRenderer.positionCount = line.Length;
            lineRenderer.SetPositions(line);
            lineRenderer.transform.SetParent(lineHolder);

            // Setup colliders
            int index = 0;
            AnimationCurve widthCurve = new AnimationCurve();
            List<Vector3> distinctLinePoints = line.Select(x => x).Distinct().ToList();
            foreach (Vector3 linePoint in distinctLinePoints)
            {
                // Create tail
                if (first && index == 0 && linePoints[0].StratumIndex == Stratums.CurrentStratumIndex)
                {
                    widthCurve.AddKey((float)index / distinctLinePoints.Count, distinctLinePoints.Count > 1f ? 0f : 0.3f);
                }
                else
                {
                    widthCurve.AddKey((float)index / distinctLinePoints.Count, defaultSize);
                }

                BoxCollider2D box = Instantiate(Prefabs.playerLineColliderPrefab, collidersHolder);
                box.transform.position = linePoint;
                lineColliders.Add(box);

                index++;
            }

            // Add last point
            if (last && currentStratumIndex == stratumIndex)
            {
                // Head detected
            }

            widthCurve.AddKey(1f, defaultSize);

            // Assign back line width after modifications
            lineRenderer.widthCurve = widthCurve;
        }
    }

    private void RemoveColliders()
    {
        lineColliders.ForEach(x => Destroy(x.gameObject));
        lineColliders.Clear();
    }

    private void ReturnLineRenderers()
    {
        // Prevents singleton null reference
        if (Lines == null) return;

        foreach (LineRenderer lineRenderer in lineRenderers)
            Lines.Return(lineRenderer);

        lineRenderers.Clear();
    }

    private void MoveTop()
    {
        Move(direction: new Vector2(0, 1));
    }

    private void MoveDown()
    {
        Move(direction: new Vector2(0, -1));
    }

    private void MoveLeft()
    {
        Move(direction: new Vector2(-1, 0));
    }

    private void MoveRight()
    {
        Move(direction: new Vector2(1, 0));
    }

    private void Ascend()
    {
        Move(PlayerMoveType.Ascend);
    }

    private void Descend()
    {
        Move(PlayerMoveType.Descend);
    }

    private void Move(PlayerMoveType type = PlayerMoveType.Normal, Vector2 direction = default)
    {
        // Check if there is a stratum above
        if (type == PlayerMoveType.Ascend && currentStratumIndex == 0)
        {
            Debug.LogWarning($"PlayerController: Trying to Ascend when no stratum has been find above", this);
            return;
        }

        // Check if there is a stratum below
        if (type == PlayerMoveType.Descend && currentStratumIndex >= Stratums.Count - 1)
        {
            Debug.LogWarning($"PlayerController: Trying to descend when no stratum has been find below", this);
            return;
        }

        // We don't want to move the player if we are not on the correct stratum
        if (currentStratumIndex != Stratums.CurrentStratumIndex)
        {
            Debug.LogWarning($"PlayerController: Trying to move when not seeing the head", this);
            return;
        }

        // Compute next position
        List<Vector3[]> positions = GetLines();
        Vector3 current = positions[^1][^1];
        Vector3 next = current;
        if (type == PlayerMoveType.Normal)
            next += (Vector3)direction;

        // Check if next position is accessible
        if (type == PlayerMoveType.Normal)
        {
            if (!Stratums.HasFreeSpaceAt(next))
            {
                Debug.LogWarning($"PlayerController: Trying to move but location not accessible at {next}", this);
                return;
            }
        }
        else
        {
            if (type == PlayerMoveType.Ascend && !Stratums.HasFreeSpaceAt(next, currentStratumIndex - 1))
            {
                Debug.LogWarning($"PlayerController: Trying to ascend to -{currentStratumIndex - 1} but location not accessible at {next}", this);
                return;
            }
            else if (type == PlayerMoveType.Descend && !Stratums.HasFreeSpaceAt(next, currentStratumIndex + 1))
            {
                Debug.LogWarning($"PlayerController: Trying to descend to -{currentStratumIndex + 1} but location not accessible at {next}", this);
                return;
            }
        }

        // Update parameter value
        switch (type)
        {
            case PlayerMoveType.Ascend:
                currentStratumIndex--;
                break;

            case PlayerMoveType.Descend:
                currentStratumIndex++;
                break;
        }

        // Add next positions
        PlayerLinePoint nextLinePoint;
        nextLinePoint.Position = next;
        nextLinePoint.StratumIndex = currentStratumIndex;
        linePoints.Add(nextLinePoint);
        linePoints.RemoveAt(0);
        RegisterPositions();

        // Update visuals
        switch (type)
        {
            case PlayerMoveType.Normal:
                UpdateVisuals();
                break;

            case PlayerMoveType.Ascend:
                Stratums.GoUp();
                break;

            case PlayerMoveType.Descend:
                Stratums.GoDown();
                break;
        }
    }

    [Button]
    private void SplitDebug()
    {
        Split(debugSplitPoint);
    }

    private void Split(Vector2 splitPoint)
    {
        // Find line point index
        int splitIndex = -1;
        foreach (PlayerLinePoint line in linePoints)
        {
            if (line.Position == splitPoint)
            {
                splitIndex = linePoints.IndexOf(line);
                break;
            }
        }

        // Checking split point existence
        if (splitIndex == -1)
        {
            Debug.LogWarning($"PlayerController: Split: No line point found at {splitPoint}", this);
            return;
        }

        // Split line points in half
        List<PlayerLinePoint> firstHalf = linePoints.Take(splitIndex).ToList();
        List<PlayerLinePoint> secondHalf = linePoints.TakeLast(linePoints.Count - 1 - splitIndex).ToList();

        // Check if cuts have at least 3 points
        if (firstHalf.Count >= 3)
        {
            // Instantiate a new player
            PlayerController newPlayer = Instantiate(Prefabs.playerPrefab);
            newPlayer.Initialize(firstHalf);
        }

        if (secondHalf.Count >= 3)
        {
            linePoints = secondHalf;
            RegisterPositions();

            UpdateVisuals();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void RegisterPositions()
    {
        // Split position per stratum
        Dictionary<int, List<Vector2>> positionPerStratum = new Dictionary<int, List<Vector2>>();
        foreach (PlayerLinePoint linePoint in linePoints)
        {
            if (!positionPerStratum.ContainsKey(linePoint.StratumIndex))
                positionPerStratum.Add(linePoint.StratumIndex, new List<Vector2>());

            positionPerStratum[linePoint.StratumIndex].Add(linePoint.Position);
        }

        // Update stratum used locations
        foreach (KeyValuePair<int, List<Vector2>> positions in positionPerStratum)
            Stratums.UpdatePositions(gameObject, positions.Key, positions.Value);
    }
}