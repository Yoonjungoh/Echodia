using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.UI;

public class UI_DropItem : UI_Base
{
    enum Images
    {
        ItemImage
    }

    private Camera _mainCamera;
    private int _gameObjectId;  // 게임 오브젝트 ID (서버에 줍기 요청 시 사용)
    private int _specItemId;    // SpecData 아이템 ID (아이콘/이름 표시용)
    private int _count;

    public int SpecItemId { get { return _specItemId; } }
    public int Count { get { return _count; } }

    public override void Init()
    {
        _mainCamera = Camera.main;
        Bind<Image>(typeof(Images));
    }

    public void SetItem(int objectId, int specItemId, int count)
    {
        _gameObjectId = objectId;
        _specItemId = specItemId;
        _count = count;

        GetImage((int)Images.ItemImage).sprite = Managers.Image.GetAssetImage(_specItemId);
    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward,
                         _mainCamera.transform.rotation * Vector3.up);
    }

    // 아이템 줍기 요청 (게임 오브젝트 ID를 서버에 전송)
    public void RequestPickUpDropItem()
    {
        Managers.Network.Send(new C_PickUpDropItem() { ItemId = _gameObjectId });
    }
}
