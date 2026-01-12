using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_HpBar : UI_Base
{
    enum GameObjects
    {
        HPBar
    }

    private Slider _hpBarSlider;
    private Vector3 _offset;
    private Transform _cameraTransform;

    public override void Init()
    {
        Bind<GameObject>(typeof(GameObjects));
        _hpBarSlider = GetObject((int)GameObjects.HPBar).GetComponent<Slider>();
    }

    public void SetData(Vector3 offset)
    {
        _offset = offset;
        transform.localPosition = _offset; // 자식 로컬 오프셋만 설정
        _cameraTransform = Camera.main.transform;   // 캐싱한 카메라 사용
    }

    private void LateUpdate()
    {
        transform.rotation = _cameraTransform.rotation;
    }

    public void UpdateHpBar(float cur, float max)
    {
        if (_hpBarSlider == null)
            return;
        
        _hpBarSlider.value = cur / max;
    }
}
