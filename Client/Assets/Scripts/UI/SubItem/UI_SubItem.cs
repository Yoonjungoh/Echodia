using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public abstract class UI_SubItem<TData> : UI_Base
{
    // TData 타입의 데이터를 받도록 통일
    protected TData _data;

    public virtual void SetData(TData data)
    {
        _data = data;
    }
    
    protected abstract void UpdateUI();
}
