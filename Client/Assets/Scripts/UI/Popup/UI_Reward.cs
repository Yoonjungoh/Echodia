using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.UI;

public class UI_Reward : UI_Popup
{
    enum Transforms
    {
        RewardItemContent,
    }

    enum Buttons
    {
        CloseButton,
        ConfirmButton,
        BackgroundButton,
    }

    private RepeatedField<RewardItem> _rewardItemList;
    private Transform _rewardItemContent;

    public override void Init()
    {
        base.Init();

        Bind<Transform>(typeof(Transforms));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickCloseButton);
        GetButton((int)Buttons.ConfirmButton).onClick.AddListener(OnClickConfirmButton);
        GetButton((int)Buttons.BackgroundButton).onClick.AddListener(OnClickBackgroundButton);

        _rewardItemContent = Get<Transform>((int)Transforms.RewardItemContent);
    }

    public void SetData(RepeatedField<RewardItem> rewardItemList)
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

    private void OnClickCloseButton()
    {
        ClosePopupUI();
    }

    private void OnClickConfirmButton()
    {
        ClosePopupUI();
    }

    private void OnClickBackgroundButton()
    {
        ClosePopupUI();
    }


}
