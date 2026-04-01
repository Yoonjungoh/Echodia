using TMPro;
using UnityEngine;


public class UI_DamageViewer : UI_Base
{
    enum Texts
    {
        DamageText,
    }

    private TextMeshProUGUI _damageText;
    private Camera _mainCamera;
    private Animator _animator;

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        _damageText = GetTextMeshProUGUI((int)Texts.DamageText);
        _mainCamera = Camera.main;
        _animator = GetComponent<Animator>();
    }

    public void ShowDamage(int damage, Vector3 worldPosition, float returnDelay = 5.0f)
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
