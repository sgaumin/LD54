using System.Collections.Generic;
using UnityEngine;

public class Stratum : MonoBehaviour
{
    [SerializeField] private Transform groundHolder;

    private List<Vector2> groundPositions = new List<Vector2>();
    private Dictionary<GameObject, List<Vector2>> usedPositions = new Dictionary<GameObject, List<Vector2>>();

    public List<Vector2> GroundPositions => groundPositions;
    public Dictionary<GameObject, List<Vector2>> UsedPositions => usedPositions;

    public void GetGrounds()
    {
        foreach (Transform child in groundHolder)
        {
            if (child == groundHolder) continue;

            groundPositions.Add(child.position);
        }
    }
}