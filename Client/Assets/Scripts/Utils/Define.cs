using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Define
{
    public enum CameraMode
    {
        CommonView,
        FirstPersonView,
        QuarterView,
        None,
    }
    public enum UIEvent
    {
        Click,
        Drag,
    }

    public enum Scene
    {
        Unknown,
        Loading,
        Login,
        ServerSelect,
        PlayerSelect,
        Lobby,
        WaitingRoom,
        GameRoom,
    }

    public enum Sound
    {
        Bgm,
        Effect,
        MaxCount,
    }

    public enum MouseEvent
    {
        LeftClick,
        RightClick,
        Press,
        PointerDown,
        PointerUp,
        Click,
    }
}
