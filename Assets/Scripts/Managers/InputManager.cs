using System;
using UnityEngine;
using static Facade;

public class InputManager : MonoBehaviour
{
    private const KeyCode RESTART = KeyCode.R;
    private const KeyCode ACTION1 = KeyCode.X;
    private const KeyCode ACTION2 = KeyCode.C;

    public static Action OnTopEvent;
    public static Action OnDownEvent;
    public static Action OnLeftEvent;
    public static Action OnRightEvent;
    public static Action OnAction1Event;
    public static Action OnAction2Event;

    private void Update()
    {
#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_EDITOR
        if (Input.GetButtonDown("Quit"))
        {
            Level.Quit();
        }
#endif

        ListenRestart();
        ListenKeyAxis();
    }

    private static void ListenRestart()
    {
        if (!Level.IsRunning && Input.GetKeyDown(RESTART))
        {
            Level.ReloadScene();
        }
    }

    private void ListenKeyAxis()
    {
        //if (!Level.IsRunning) return;

        // UP
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            Debug.Log($"InputManager: OnTopEvent");
            OnTopEvent?.Invoke();
        }

        // DOWN
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            Debug.Log($"InputManager: OnDownEvent");
            OnDownEvent?.Invoke();
        }

        // LEFT
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Debug.Log($"InputManager: OnLeftEvent");
            OnLeftEvent?.Invoke();
        }

        // RIGHT
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Debug.Log($"InputManager: OnRightEvent");
            OnRightEvent?.Invoke();
        }

        // Action 1
        if (Input.GetKeyDown(ACTION1))
        {
            Debug.Log($"InputManager: OnAction1Event");
            OnAction1Event?.Invoke();
        }

        // Action 2
        if (Input.GetKeyDown(ACTION2))
        {
            Debug.Log($"InputManager: OnAction2Event");
            OnAction2Event?.Invoke();
        }
    }
}