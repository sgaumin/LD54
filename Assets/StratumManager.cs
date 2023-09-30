using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StratumManager : MonoBehaviour
{
    public static StratumManager Instance;
    public static Action<int> OnStratumChanged;

    [SerializeField] private int startIndex = 0;
    [SerializeField] private List<Stratum> stratums;

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
    }

    [Button]
    public void GoUp()
    {
        // We decrease index to go up, this is expected
        CurrentStratumIndex--;
    }

    [Button]
    public void GoDown()
    {
        // We increase index to go down, this is expected
        CurrentStratumIndex++;
    }
}