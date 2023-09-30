using Cysharp.Threading.Tasks.Triggers;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

    private int currentStratumIndex;
    private List<PlayerLinePoint> linePoints;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

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
        linePoints = startData.LinePoints.ToList();

        // Set starting stratum index
        currentStratumIndex = startData.LinePoints[^1].StratumIndex;
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
    }

    #region MOVEMENTS

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
        // Return all lines currently used
        foreach (LineRenderer lineRenderer in lineRenderers)
            Lines.Return(lineRenderer);

        lineRenderers.Clear();

        // If using default, get current stratum index
        if (stratumIndex == -1)
            stratumIndex = Stratums.CurrentStratumIndex;

        List<Vector3[]> lines = GetLines(stratumIndex);
        foreach (Vector3[] linePoints in lines)
        {
            // Get a line from pool, and update its visuals
            LineRenderer line = Lines.Get();
            lineRenderers.Add(line);
            line.positionCount = linePoints.Length;
            line.SetPositions(linePoints);
            line.transform.SetParent(transform);
        }
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
            Debug.LogWarning($"PlayerController: Trying to Ascend when no stratum has been find above");
            return;
        }

        // Check if there is a stratum below
        if (type == PlayerMoveType.Descend && currentStratumIndex >= Stratums.Count - 1)
        {
            Debug.LogWarning($"PlayerController: Trying to descend when no stratum has been find below");
            return;
        }

        // We don't want to move the player if we are not on the correct stratum
        if (currentStratumIndex != Stratums.CurrentStratumIndex)
        {
            Debug.LogWarning($"PlayerController: Trying to more when not seeing the head");
            return;
        }

        // TODO: Check if we can actually go in this direction

        // Compute next position
        List<Vector3[]> positions = GetLines();
        Vector3 next = positions[^1][^1];
        if (type == PlayerMoveType.Normal)
            next += (Vector3)direction;

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

    #endregion
}