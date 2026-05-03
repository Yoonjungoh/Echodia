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

    private static readonly Color DefaultNameColor = Color.white;
    private static readonly Color PartyMemberColor = Color.yellow;

    public void SetData(string name, Vector3 offset)
    {
        _offset = offset;
        _playerNameText.text  = name;
        _playerNameText.color = DefaultNameColor;
        transform.localPosition = _offset;
        _cameraTransform = Camera.main.transform;
    }

    public void SetPartyColor(bool isPartyMember)
    {
        _playerNameText.color = isPartyMember ? PartyMemberColor : DefaultNameColor;
    }

    private void LateUpdate()
    {
        transform.rotation = _cameraTransform.rotation;
    }
}
