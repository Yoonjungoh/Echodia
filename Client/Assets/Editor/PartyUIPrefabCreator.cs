using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PartyUIPrefabCreator
{
    private const string PopupPath  = "Assets/Resources/Prefabs/UI/Popup";
    private const string SubItemPath = "Assets/Resources/Prefabs/UI/SubItem";

    [MenuItem("Tools/Create Party UI Prefabs")]
    public static void CreateAll()
    {
        CreateUI_Party();
        CreatePartyPlayer_SubItem();
        CreateUI_PartyInviteNotification();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PartyUI] 3개 프리팹 생성 완료");
    }

    // ────────────────────────────────────────────────────────────────────────
    // UI_Party
    // ────────────────────────────────────────────────────────────────────────
    static void CreateUI_Party()
    {
        var root = CreatePanel("UI_Party", new Vector2(480, 600));
        root.AddComponent<UI_Party>();

        // 버튼 3개 (우상단 닫기 / 하단 좌우)
        var closeBtn = CreateButton(root, "CloseButton", "✕", new Vector2(40, 40));
        Anchor(closeBtn, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-25, -25));

        var createBtn = CreateButton(root, "CreatePartyButton", "파티 생성", new Vector2(130, 40));
        Anchor(createBtn, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 30));

        var leaveBtn = CreateButton(root, "LeavePartyButton", "파티 탈퇴", new Vector2(130, 40));
        Anchor(leaveBtn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 30));

        // Scroll View
        CreateScrollView(root, "PlayerListContent",
            anchoredPos: new Vector2(0, 25),
            sizeDelta:   new Vector2(-20, -120));

        Save(root, PopupPath, "UI_Party");
    }

    // ────────────────────────────────────────────────────────────────────────
    // PartyPlayer_SubItem
    // ────────────────────────────────────────────────────────────────────────
    static void CreatePartyPlayer_SubItem()
    {
        var root = CreatePanel("PartyPlayer_SubItem", new Vector2(440, 60));
        root.AddComponent<PartyPlayer_SubItem>();

        // HorizontalLayoutGroup으로 자식 자동 정렬
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.spacing = 8;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        CreateTMPText(root, "NameText",        150, 20);
        CreateTMPText(root, "LevelText",       60,  20);
        CreateTMPText(root, "JobText",         60,  20);
        CreateTMPText(root, "PartyStatusText", 60,  20);
        CreateButton(root,  "InviteButton", "초대", new Vector2(60, 36));
        CreateButton(root,  "KickButton",   "추방", new Vector2(60, 36));

        Save(root, SubItemPath, "PartyPlayer_SubItem");
    }

    // ────────────────────────────────────────────────────────────────────────
    // UI_PartyInviteNotification
    // ────────────────────────────────────────────────────────────────────────
    static void CreateUI_PartyInviteNotification()
    {
        var root = CreatePanel("UI_PartyInviteNotification", new Vector2(340, 140));
        root.AddComponent<UI_PartyInviteNotification>();

        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 12;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        CreateTMPText(root, "InviteMessageText", 308, 40, fontSize: 18, center: true);

        // 버튼 한 줄에 나란히 → 별도 Row 오브젝트
        var row = new GameObject("ButtonRow");
        row.transform.SetParent(root.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0, 48);
        var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing = 16;
        rowHlg.childForceExpandWidth  = false;
        rowHlg.childForceExpandHeight = true;
        rowHlg.childAlignment = TextAnchor.MiddleCenter;

        CreateButton(row, "AcceptButton",  "O", new Vector2(80, 40));
        CreateButton(row, "DeclineButton", "X", new Vector2(80, 40));

        Save(root, PopupPath, "UI_PartyInviteNotification");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────
    static GameObject CreatePanel(string name, Vector2 size)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        return go;
    }

    static GameObject CreateButton(GameObject parent, string name, string label, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.75f, 1f);
        go.AddComponent<Button>();

        // label 텍스트
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        StretchAll(labelGo.AddComponent<RectTransform>());
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 15;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // LayoutElement — Layout Group 안에서 크기 고정
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = size.x;
        le.preferredHeight = size.y;

        return go;
    }

    static GameObject CreateTMPText(GameObject parent, string name,
        float width, float height, float fontSize = 16, bool center = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = name;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = height;

        return go;
    }

    static void CreateScrollView(GameObject parent, string contentName,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        // Scroll View
        var svGo = new GameObject("Scroll View");
        svGo.transform.SetParent(parent.transform, false);
        var svRt = svGo.AddComponent<RectTransform>();
        svRt.anchorMin     = new Vector2(0, 0);
        svRt.anchorMax     = new Vector2(1, 1);
        svRt.anchoredPosition = anchoredPos;
        svRt.sizeDelta     = sizeDelta;
        svGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
        var scrollRect = svGo.AddComponent<ScrollRect>();

        // Viewport
        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(svGo.transform, false);
        StretchAll(vpGo.AddComponent<RectTransform>());
        vpGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        vpGo.AddComponent<Mask>().showMaskGraphic = false;

        // Content (이름이 Bind 키)
        var contentGo = new GameObject(contentName);
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin     = new Vector2(0, 1);
        contentRt.anchorMax     = new Vector2(1, 1);
        contentRt.pivot         = new Vector2(0.5f, 1);
        contentRt.sizeDelta     = Vector2.zero;
        contentRt.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport    = vpGo.GetComponent<RectTransform>();
        scrollRect.content     = contentRt;
        scrollRect.horizontal  = false;
        scrollRect.vertical    = true;
    }

    // RectTransform을 부모에 꽉 채우기
    static void StretchAll(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 앵커를 특정 코너로 고정 + 오프셋 위치 지정
    static void Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 anchoredPos)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin       = min;
        rt.anchorMax       = max;
        rt.anchoredPosition = anchoredPos;
    }

    static void Save(GameObject go, string folder, string name)
    {
        string path = $"{folder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[PartyUI] Saved → {path}");
    }
}
