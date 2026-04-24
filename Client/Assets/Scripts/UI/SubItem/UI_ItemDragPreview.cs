using UnityEngine;
using UnityEngine.UI;

public class UI_ItemDragPreview : MonoBehaviour
{
    private RectTransform _rt;
    private Canvas _canvas;
    private Image _image;

    public void Setup(Canvas canvas, Image image)
    {
        _rt = GetComponent<RectTransform>();
        _canvas = canvas;
        _image = image;
    }

    public void UpdateIcon(Sprite icon)
    {
        _image.sprite = icon;
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

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
