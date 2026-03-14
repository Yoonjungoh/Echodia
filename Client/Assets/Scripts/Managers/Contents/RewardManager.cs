

using System.Collections.Generic;
using Google.Protobuf.Protocol;

public class RewardManager
{
    UI_Reward _rewardUI;
    public void ShowRewardList(List<RewardItem> rewardItemList)
    {
        if (_rewardUI == null)
        {
            _rewardUI = Managers.UI.ShowPopupUI<UI_Reward>();
        }
        _rewardUI.SetData(rewardItemList);
    }
}