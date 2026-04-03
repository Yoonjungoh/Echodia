using TMPro;
using UnityEngine;


public class UI_DamageViewer : UI_Base
{
    public const string IDLE_CLIP_NAME = "DAMAGE_VIEWER_IDLE";

    enum Texts
    {
        DamageText,
    }

    private TextMeshProUGUI _damageText;
    private Camera _mainCamera;
    private Animator _animator;

    private bool _isCritical;

    public override void Init()
    {
        if (IsInitialized)
            return;

        IsInitialized = true;

        Bind<TextMeshProUGUI>(typeof(Texts));
        _damageText = GetTextMeshProUGUI((int)Texts.DamageText);
        _mainCamera = Camera.main;
        _animator = GetComponent<Animator>();
    }

    public void SetData(bool isCritical)
    {
        _isCritical = isCritical;
        SetTextColor();
    }

    private void SetTextColor()
    {
        // 크리티컬이면 빨간색 텍스트 표시
        if (_damageText != null)
        {
            _damageText.color = _isCritical ? Color.red : Color.yellow;
        }
    }

    public void Reset()
    {
        _isCritical = false;
        _animator?.Rebind();
    }

    public void ShowDamage(int damage, Vector3 worldPosition, float returnDelay = 3.0f)
    {
        transform.position = worldPosition;
        _damageText.text = damage.ToString();

        if (_animator != null)
        {
            _animator.Play(0, -1, 0f);
        }

        Managers.Resource.Destroy(gameObject, returnDelay);
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
            return;

        transform.LookAt(
            transform.position + _mainCamera.transform.rotation * Vector3.forward,
            _mainCamera.transform.rotation * Vector3.up
        );
    }
}
