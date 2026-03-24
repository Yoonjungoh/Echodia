using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class UI_ToastPopup : UI_Popup
{
    [SerializeField] private float _fadeLerpTime = 1.0f;  // 사라지는 속도
    private TextMeshProUGUI _toastPopupText;             // 노출되는 텍스트
    private bool _loopRunning = false;

    // 현재 표시할 메시지 상태
    private string _currentMessage;
    private float _currentDuration;
    private Color _currentColor;
    private bool _hasMessage = false;

    enum Texts
    {
        ToastPopupText,
    }

    public override void Init()
    {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));
        _toastPopupText = GetTextMeshProUGUI((int)Texts.ToastPopupText);
    }

    public void ShowToastPopup(string message, float duration, Color? colorOverride = null)
    {
        // 새로운 메시지로 덮어쓰기 (항상 마지막 호출이 우선)
        _currentMessage = message;
        _currentDuration = duration;
        _currentColor = colorOverride ?? _toastPopupText.color;
        _currentColor.a = 1f;
        _hasMessage = true;

        if (!_loopRunning)
        {
            CoToastLoop(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    // 하나의 지속 루프를 돌며, 메시지가 들어올 때만 처리
    private async UniTask CoToastLoop(CancellationToken token)
    {
        _loopRunning = true;
        while (true)
        {
            // 메시지가 들어올 때까지 빈 루프
            while (_hasMessage == false)
            {
                await UniTask.Yield(token);
            }

            // 메시지 표시
            _toastPopupText.text = _currentMessage;
            _toastPopupText.color = _currentColor;

            // 지정된 시간 동안 유지
            float elapsed = 0f;
            while (elapsed < _currentDuration)
            {
                // 표시 중에 새로운 메시지가 들어오면 즉시 중단하고 다음 메시지를 표시
                if (_hasMessage == false)
                    break;

                elapsed += Time.deltaTime;

                await UniTask.Yield(token);
            }

            // Fade out 처리
            float fadeElapsed = 0f;
            Color colorBeforeFade = _toastPopupText.color;
            while (fadeElapsed < _fadeLerpTime)
            {
                // 새로운 메시지가 들어오면 즉시 중단하고 다음 메시지를 표시
                if (_currentMessage != _toastPopupText.text)
                    break;

                fadeElapsed += Time.deltaTime;
                float percent = Mathf.Clamp01(fadeElapsed / _fadeLerpTime);

                colorBeforeFade.a = Mathf.Lerp(1f, 0f, percent);
                _toastPopupText.color = colorBeforeFade;

                await UniTask.Yield(token);
            }

            // 만약 마지막에 페이드가 완료되었고 메시지가 변경되지 않았다면 텍스트를 숨김
            if (_currentMessage == _toastPopupText.text)
            {
                var c = _toastPopupText.color;
                c.a = 0f;
                _toastPopupText.color = c;
                _toastPopupText.text = string.Empty;

                _hasMessage = false;
            }

            await UniTask.Yield(token);
        }
    }

}
