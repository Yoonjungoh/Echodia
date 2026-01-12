using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class UI_Popup : UI_Base
{
    public override void Init()
    {
        Managers.UI.SetCanvas(gameObject, true);
    }

    public virtual void ClosePopupUI()
    {
        Managers.UI.ClosePopupUI(this);
    }
}

public abstract class UI_Popup<TData> : UI_Popup
{
    protected TData _data;

    // 팝업에 데이터를 설정하고 UI를 갱신할 메서드
    public virtual void SetData(TData data)
    {
        _data = data;
    }
    protected abstract void UpdateUI();
    
    public override void Init()
    {
        base.Init();
    }
}
