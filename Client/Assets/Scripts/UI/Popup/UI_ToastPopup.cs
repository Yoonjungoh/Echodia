using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class UI_ToastPopup : UI_Popup
{
    [SerializeField] private float _fadeLerpTime = 1.0f;  // 사라지는 속도
    private TextMeshProUGUI _toastPopupText;             // 노출되는 텍스트
    private Coroutine _loopCoroutine;

    // 현재 표시할 메시지 상태
    private string _currentMessage;
    private float _currentDuration;
    private Color _currentColor;
    private bool _hasMessage = false;

    // 카운트다운 변수
    private const float COUNTDOWN_DLEAY_TIME = 0.1f;
    private WaitForSeconds _countdownDelay = new WaitForSeconds(0);
    private bool _countdownActive = false;
    private Coroutine _countdownCoroutine;

    enum Texts
    {
        ToastPopupText,
    }

    public override void Init()
    {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));
        _toastPopupText = GetTextMeshProUGUI((int)Texts.ToastPopupText);
        _countdownDelay = new WaitForSeconds(COUNTDOWN_DLEAY_TIME);
    }

    public void ShowCountdown(float time, Action callBack = null, bool isHideCountdownText = false)
    {
        // 이미 카운트다운 중이면 무시
        if (_countdownActive)
            return;

        // 토스트 루프 메시지 무시 상태로 전환
        _countdownActive = true;
        _hasMessage = false;

        // 기존 카운트다운 코루틴 있으면 중단
        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        _countdownCoroutine = StartCoroutine(CoCountdown(time, callBack, isHideCountdownText));
    }

    private IEnumerator CoCountdown(float time, Action callBack = null, bool isHideCountdownText = false)
    {
        float remain = time;
        _toastPopupText.gameObject.SetActive(isHideCountdownText == false);

        while (remain > 0f)
        {
            // 0.1 단위 표시
            float displayValue = Mathf.Max(0f, remain);
            _toastPopupText.text = displayValue.ToString("0.0");

            Color c = _toastPopupText.color;
            c.a = 1f;
            _toastPopupText.color = c;

            remain -= COUNTDOWN_DLEAY_TIME;
            yield return _countdownDelay;
        }

        // 카운트다운 종료
        _toastPopupText.text = "";
        Color fade = _toastPopupText.color;
        fade.a = 0f;
        _toastPopupText.color = fade;

        _countdownActive = false;
        _countdownCoroutine = null;

        // 콜백 호출
        callBack?.Invoke();
    }

    public void ShowToastPopup(string message, float duration, Color? colorOverride = null)
    {
        // 새로운 메시지로 덮어쓰기 (항상 마지막 호출이 우선)
        _currentMessage = message;
        _currentDuration = duration;
        _currentColor = colorOverride ?? _toastPopupText.color;
        _currentColor.a = 1f;
        _hasMessage = true;

        // 코루틴이 존재하지 않으면 시작 (따라서 최초 1회만 할당)
        if (_loopCoroutine == null)
        {
            _loopCoroutine = StartCoroutine(CoToastLoop());
        }
    }

    // 하나의 지속 루프를 돌며, 메시지가 들어올 때만 처리
    private IEnumerator CoToastLoop()
    {
        while (true)
        {
            // 메시지가 들어올 때까지 빈 루프
            while (_hasMessage == false)
            {
                yield return null;
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

                yield return null;
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

                yield return null;
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

            yield return null;
        }
    }
}
