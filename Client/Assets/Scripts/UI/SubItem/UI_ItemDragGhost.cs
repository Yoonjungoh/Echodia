using UnityEngine;
using UnityEngine.UI;

public class UI_ItemDragGhost : MonoBehaviour
{
    private RectTransform _rt;
    private Canvas _canvas;

    public void Setup(Canvas canvas)
    {
        _rt = GetComponent<RectTransform>();
        _canvas = canvas;
    }

    private void Update()
    {
        if (_canvas == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            Input.mousePosition,
            null,
            out Vector2 pos
        );
        _rt.anchoredPosition = pos;
    }
}
