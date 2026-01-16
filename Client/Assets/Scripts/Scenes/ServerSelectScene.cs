using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerSelectScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        Managers.UI.ShowSceneUI<UI_ServerSelect>();
    }

    private void Awake()
    {
        Init();
    }
}
