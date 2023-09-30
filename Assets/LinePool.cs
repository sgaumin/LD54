using System.Collections.Generic;
using UnityEngine;

public class LinePool : MonoBehaviour
{
    public static LinePool Instance;

    [SerializeField] private LineRenderer linePrefab;

    private Queue<LineRenderer> lines = new Queue<LineRenderer>();

    private void Awake()
    {
        Instance = this;
    }

    public LineRenderer Get()
    {
        if (lines.Count == 0)
            lines.Enqueue(Instantiate(linePrefab, transform));

        LineRenderer line = lines.Dequeue();
        line.gameObject.SetActive(true);

        return line;
    }

    public void Return(LineRenderer line)
    {
        line.gameObject.SetActive(false);
        line.transform.SetParent(transform);
        lines.Enqueue(line);
    }
}