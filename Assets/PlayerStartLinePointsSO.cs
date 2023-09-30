using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Player Data/Start Line Points Data", order = 1)]
public class PlayerStartLinePointsSO : ScriptableObject
{
    [SerializeField] private List<PlayerLinePoint> linePoints;

    public List<PlayerLinePoint> LinePoints => linePoints;
}