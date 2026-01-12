using System;
using System.Collections.Generic;
using UnityEngine;

public class InputManager
{
    // 키보드 키와 Action 매핑
    private Dictionary<KeyCode, Action> _keyActions = new Dictionary<KeyCode, Action>();

    // 마우스 버튼과 Action 매핑 (0: 좌클릭, 1: 우클릭, 2: 휠클릭 등)
    private Dictionary<int, Action> _mouseActions = new Dictionary<int, Action>();

    public Action<Define.MouseEvent> MouseEventAction = null;
    private bool _pressed = false;

    // 키보드 입력 등록
    public void RegisterKeyAction(KeyCode key, Action action)
    {
        if (_keyActions.ContainsKey(key))
            _keyActions[key] += action;
        else
            _keyActions[key] = action;
    }

    // 마우스 버튼 입력 등록
    public void RegisterMouseAction(Define.MouseEvent mouseEvent, Action action)
    {
        int button = (int)mouseEvent;

        if (_mouseActions.ContainsKey(button))
            _mouseActions[button] += action;
        else
            _mouseActions[button] = action;
    }

    public void OnUpdate()
    {
        if (_keyActions.Count == 0 && _mouseActions.Count == 0)
            return;
        
        // 키 입력 처리
        foreach (var pair in _keyActions)
        {
            if (Input.GetKeyDown(pair.Key))
                pair.Value?.Invoke();
        }

        // 마우스 버튼 입력 처리
        foreach (var pair in _mouseActions)
        {
            if (Input.GetMouseButtonDown(pair.Key))
                pair.Value?.Invoke();
        }

        //// 추가로 MouseEventAction 이벤트 방식 지원
        //if (MouseEventAction != null)
        //{
        //    if (Input.GetMouseButton(0))
        //    {
        //        MouseEventAction.Invoke(Define.MouseEvent.Press);
        //        _pressed = true;
        //    }
        //    else if (Input.GetMouseButtonUp(0))
        //    {
        //        if (_pressed)
        //            MouseEventAction.Invoke(Define.MouseEvent.Click);
        //        _pressed = false;
        //    }
        //}
    }

    public void Clear()
    {
        _keyActions.Clear();
        _mouseActions.Clear();
    }
}
