using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using UnityEngine;

public class UI_Reward : UI_Popup
{
    enum Transforms
    {
        RewardItemContents,
    }
    private List<RewardItem> _rewardItemList;
    private Transform _rewardItemContent;

    public override void Init()
    {
        base.Init();
        Bind<Transform>(typeof(Transforms));
        _rewardItemContent = Get<Transform>((int)Transforms.RewardItemContents);
    }

    public void SetData(List<RewardItem> rewardItemList)
    {
        _rewardItemList = rewardItemList;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_rewardItemContent == null)
            return;

        
        // TODO - 서브 아이템 최적화
        foreach (Transform child in _rewardItemContent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }
    
        foreach (RewardItem rewardItem in _rewardItemList)
        {
            Reward_SubItem rewardSubItem = Managers.UI.MakeSubItem<Reward_SubItem>(_rewardItemContent);
            rewardSubItem.SetData(rewardItem);
        }
    }
}
