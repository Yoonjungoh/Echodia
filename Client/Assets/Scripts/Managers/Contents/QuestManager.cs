
using Google.Protobuf.Protocol;
using UnityEngine;

public class QuestManager
{
    public void Init()
    {
        
    }

    public UI_Quest GetQuestPopup()
    {
        UI_Quest questUI = null;
        if (Managers.UI.IsPopupActive<UI_Quest>())
        {
            questUI = Managers.UI.GetPopupUI<UI_Quest>();
        }
        else
        {
            questUI = Managers.UI.ShowPopupUI<UI_Quest>();
        }
        
        return questUI;
    }
}