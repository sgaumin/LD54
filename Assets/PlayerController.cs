using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;
using static Facade;

public enum PlayerMoveType
{
    Normal,
    Ascend,
    Descend
}

public struct PlayerLine
{
    public int stratumIndex;
    public Vector3[] positions;
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStartLinePointsSO startData;

    [SerializeField] private float defaultSize = 0.5f;

    [Header("References")]
    [SerializeField] private Transform lineHolder;
    [SerializeField] private Transform colliderHolder;
    [SerializeField] private Transform visualHolder;

    [Header("Line Animation")]
    [SerializeField] private float lineAnimationOffset = 0.1f;
    [SerializeField] private float lineAnimationSpeed = 10f;
    [SerializeField] private float lineAnimationStep = 10f;

    [Header("Debug")]
    [SerializeField] private Vector2 debugSplitPoint;

    private int currentStratumIndex;
    private List<LineRenderer> lineRenderers = new();
    private List<BoxCollider2D> colliders = new();
    private List<GameObject> visualHelpers = new();
    private List<PlayerLinePoint> linePoints;
    private CancellationTokenSource lineWaveAnimationCancellationSource = new();

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

        CleanUp();
    }

    private List<PlayerLine> GetLines()
    {
        void Register(List<PlayerLine> lines, int stratumIndex, List<Vector3> positions)
        {
            if (positions.Count > 0)
            {
                // We add a duplicate in order to make this point visible
                if (positions.Count == 1)
                    positions.Add(positions[0]);

                PlayerLine line = new PlayerLine();
                line.stratumIndex = stratumIndex;
                line.positions = positions.ToArray();

                lines.Add(line);
                positions.Clear();
            }
        }

        List<PlayerLine> lines = new List<PlayerLine>();
        List<Vector3> positions = new List<Vector3>();
        int previousStratumIndex = -1;
        foreach (PlayerLinePoint linePoint in linePoints)
        {
            if (previousStratumIndex < 0)
                previousStratumIndex = linePoint.StratumIndex;

            if (linePoint.StratumIndex != previousStratumIndex)
            {
                Register(lines, previousStratumIndex, positions);
                previousStratumIndex = linePoint.StratumIndex;
            }

            positions.Add(linePoint.Position);
        }

        Register(lines, previousStratumIndex, positions);

        return lines;
    }

    private void UpdateVisuals(int stratumIndex = -1)
    {
        CleanUp();

        // If using default, get current stratum index
        if (stratumIndex == -1)
            stratumIndex = Stratums.CurrentStratumIndex;

        int currentLineIndex = 0;
        int previousStratumIndex = -1;
        int nextStratumIndex = -1;
        List<PlayerLine> lines = GetLines();
        foreach (PlayerLine line in lines)
        {
            if (line.stratumIndex == stratumIndex)
            {
                // Caching parameters
                bool first = lines.IndexOf(line) == 0;
                bool last = lines.IndexOf(line) == lines.Count - 1;
                nextStratumIndex = currentLineIndex < lines.Count - 1 ? lines[currentLineIndex + 1].stratumIndex : -1;

                // Get a line from pool, and update its visuals
                LineRenderer lineRenderer = Lines.Get();
                lineRenderers.Add(lineRenderer);
                lineRenderer.positionCount = line.positions.Length;
                lineRenderer.SetPositions(line.positions);
                lineRenderer.transform.SetParent(lineHolder);

                // Setup colliders
                int i = 0;
                AnimationCurve widthCurve = new AnimationCurve();
                List<Vector3> distinctLinePoints = line.positions.Select(x => x).Distinct().ToList();
                foreach (Vector3 linePoint in distinctLinePoints)
                {
                    // Create tail
                    if (first && i == 0 && linePoints[0].StratumIndex == Stratums.CurrentStratumIndex)
                    {
                        widthCurve.AddKey((float)i / distinctLinePoints.Count, distinctLinePoints.Count > 1f ? 0.2f : 0.35f);
                    }
                    else
                    {
                        widthCurve.AddKey((float)i / distinctLinePoints.Count, defaultSize);
                    }

                    // Show visual indicators on first index
                    if (i == 0 && previousStratumIndex >= 0 && previousStratumIndex != stratumIndex)
                    {
                        GameObject visual = Instantiate(previousStratumIndex > stratumIndex ? Prefabs.playerHolePrefab : Prefabs.playerInnerBodyPrefab, visualHolder);
                        visual.transform.position = linePoint;
                        visualHelpers.Add(visual);
                    }

                    // Show visual indicators on last index
                    if (i == distinctLinePoints.Count - 1 && nextStratumIndex >= 0 && nextStratumIndex != stratumIndex)
                    {
                        GameObject visual = Instantiate(nextStratumIndex > stratumIndex ? Prefabs.playerHolePrefab : Prefabs.playerInnerBodyPrefab, visualHolder);
                        visual.transform.position = linePoint;
                        visualHelpers.Add(visual);
                    }

                    BoxCollider2D box = Instantiate(Prefabs.playerLineColliderPrefab, colliderHolder);
                    box.transform.position = linePoint;
                    visualHelpers.Add(box.gameObject);

                    i++;
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

            // Caching
            previousStratumIndex = line.stratumIndex;

            currentLineIndex++;
        }

        LineWaveAnimation(lineWaveAnimationCancellationSource.Token).Forget();
    }

    private async UniTask LineWaveAnimation(CancellationToken cancellationToken)
    {
        List<Keyframe[]> startKeys = new();
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            LineRenderer line = lineRenderers[i];
            AnimationCurve curve = line.widthCurve;
            startKeys.Add(curve.keys);
        }

        if (lineRenderers.Count > 0)
        {
            while (true)
            {
                await UniTask.Yield(cancellationToken);

                for (int i = 0; i < lineRenderers.Count; i++)
                {
                    LineRenderer line = lineRenderers[i];
                    AnimationCurve curve = line.widthCurve;
                    curve = line.widthCurve;
                    for (int j = 0; j < curve.keys.Length; j++)
                    {
                        curve.MoveKey(j,
                            new Keyframe(
                                startKeys[i][j].time,
                                startKeys[i][j].value + lineAnimationOffset * Mathf.Sin(Time.time * lineAnimationSpeed + j * lineAnimationStep)));
                        line.widthCurve = curve;
                    }
                }
            }
        }
    }

    private void CleanUp()
    {
        ReturnLineRenderers();

        lineWaveAnimationCancellationSource = lineWaveAnimationCancellationSource.SafeReset();

        colliders.ForEach(x => Destroy(x.gameObject));
        colliders.Clear();

        visualHelpers.ForEach(x => Destroy(x.gameObject));
        visualHelpers.Clear();
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
        Vector3 current = linePoints[^1].Position;
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

        // CLear out all positions cached
        Stratums.ClearPositions(gameObject);

        // Update stratum used locations
        foreach (KeyValuePair<int, List<Vector2>> positions in positionPerStratum)
            Stratums.UpdatePositions(gameObject, positions.Key, positions.Value);
    }
}