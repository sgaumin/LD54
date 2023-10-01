using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class StratumManager : MonoBehaviour
{
    public static StratumManager Instance;
    public static Action<int> OnStratumChanged;

    [SerializeField] private int startIndex = 0;
    [SerializeField] private List<Stratum> stratums;

    private bool alreadyPendingUpdate;

    public int Count => stratums.Count;
    public Stratum CurrentStratum => stratums[CurrentStratumIndex];

    private int currentStratumIndex;
    public int CurrentStratumIndex
    {
        get => currentStratumIndex;
        private set
        {
            // Check new value
            if (value < 0 || value >= stratums.Count)
            {
                Debug.LogWarning($"StratumManager: CurrentStratumIndex tried to change to {value}. Canceled");
                return;
            }

            // Apply new value
            currentStratumIndex = value;
            Debug.Log($"StratumManager: CurrentStratumIndex changed to {value}");

            // Update visuals
            foreach (Stratum stratum in stratums)
            {
                stratum.gameObject.SetActive(stratums.IndexOf(stratum) == currentStratumIndex);
            }

            // Callbacks
            OnStratumChanged?.Invoke(currentStratumIndex);
        }
    }

    private void Reset()
    {
        stratums = GetComponentsInChildren<Stratum>().ToList();
    }

    private void OnValidate()
    {
        // Show start stratum in editor
        foreach (Stratum stratum in stratums)
        {
            stratum.gameObject.SetActive(stratums.IndexOf(stratum) == startIndex);
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CurrentStratumIndex = startIndex;

        stratums.ForEach(x => x.GetGrounds());
    }

    [Button]
    public void GoUp()
    {
        GoUpAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid GoUpAsync(CancellationToken cancellationToken)
    {
        if (alreadyPendingUpdate) return;

        alreadyPendingUpdate = true;
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

        // We decrease index to go up, this is expected
        CurrentStratumIndex--;
        alreadyPendingUpdate = false;
    }

    [Button]
    public void GoDown()
    {
        GoDownAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public bool HasFreeSpaceAt(Vector2 position, int stratumIndex = -1)
    {
        if (stratumIndex == -1)
            stratumIndex = CurrentStratumIndex;

        if (!HasGroundAt(position, stratumIndex))
        {
            Debug.Log($"StratumManager: HasFreeSpaceAt: No ground detected on stratum {stratumIndex} at {position}");
            return false;
        }

        // Loop through all stored data
        foreach (List<Vector2> positions in stratums[stratumIndex].UsedPositions.Values)
        {
            if (positions.Contains(position))
                return false;
        }

        return true;
    }

    private bool HasGroundAt(Vector2 position, int stratumIndex = -1)
    {
        if (stratumIndex == -1)
            stratumIndex = CurrentStratumIndex;

        return stratums[stratumIndex].GroundPositions.Contains(position);
    }

    private async UniTaskVoid GoDownAsync(CancellationToken cancellationToken)
    {
        if (alreadyPendingUpdate) return;

        alreadyPendingUpdate = true;
        await UniTask.Yield(cancellationToken);

        // We increase index to go down, this is expected
        CurrentStratumIndex++;
        alreadyPendingUpdate = false;
    }

    public void UpdatePositions(GameObject gameObject, int stratumIndex, List<Vector2> positions)
    {
        if (!stratums[stratumIndex].UsedPositions.ContainsKey(gameObject))
            stratums[stratumIndex].UsedPositions.Add(gameObject, new List<Vector2>());

        stratums[stratumIndex].UsedPositions[gameObject] = positions;
    }
}