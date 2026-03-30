using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.UI;

public class UI_DropItem : UI_Base
{
    enum Images
    {
        ItemImage
    }

    enum ParticleSystems
    {
        DropEffect
    }

    private Camera _mainCamera;
    private int _gameObjectId;  // 게임 오브젝트 ID (서버에 줍기 요청 시 사용, 서버에게 부여받은 Id)
    private int _specItemId;    // SpecData 아이템 ID (아이콘/이름 표시용)
    private int _count;

    private ParticleSystem _dropEffect;

    public int SpecItemId { get { return _specItemId; } }
    public int Count { get { return _count; } }

    public override void Init()
    {
        _mainCamera = Camera.main;
        Bind<Image>(typeof(Images));
        Bind<ParticleSystem>(typeof(ParticleSystems));

        _dropEffect = Get<ParticleSystem>((int)ParticleSystems.DropEffect);
        _dropEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void SetItem(int objectId, int specItemId, int count)
    {
        _gameObjectId = objectId;
        _specItemId = specItemId;
        _count = count;

        GetImage((int)Images.ItemImage).sprite = Managers.Image.GetAssetImage(_specItemId);

        ApplyGradeColor(_specItemId);
    }

    private void ApplyGradeColor(int specItemId)
    {
        // 파티클 재생 전 카메라 방향으로 회전 적용
        _dropEffect.transform.LookAt(
            _dropEffect.transform.position + _mainCamera.transform.rotation * Vector3.forward,
            _mainCamera.transform.rotation * Vector3.up
        );

        ItemMetaData itemMetaData = Managers.SpecData.GetItem(specItemId);
        if (itemMetaData == null)
            return;
            
        Color gradeColor = Managers.Color.GetGradeColor(itemMetaData.ItemGrade);

        foreach (ParticleSystem child in _dropEffect.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = child.main;
            main.startColor = gradeColor;
        }

        _dropEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _dropEffect.Play(true);
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
