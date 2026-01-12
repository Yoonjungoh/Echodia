using System;
using Unity;
using UnityEngine;

public class TimeManager
{
    public void Start()
    {
        Time.timeScale = 1.0f;
    }

    public void Stop()
    {
        Time.timeScale = 0.0f;
    }
}
