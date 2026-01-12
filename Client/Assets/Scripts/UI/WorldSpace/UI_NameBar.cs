using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_NameBar : UI_Base
{
    enum Texts
    {
        PlayerNameText
    }

    private TextMeshProUGUI _playerNameText;
    private Vector3 _offset;
    private Transform _cameraTransform;

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        _playerNameText = GetTextMeshProUGUI((int)Texts.PlayerNameText);
    }

    public void SetData(string name, Vector3 offset)
    {
        _offset = offset;
        _playerNameText.text = name;
        transform.localPosition = _offset; // 자식 로컬 오프셋만 설정
        _cameraTransform = Camera.main.transform;   // 캐싱한 카메라 사용
    }

    private void LateUpdate()
    {
        transform.rotation = _cameraTransform.rotation;
    }
}
