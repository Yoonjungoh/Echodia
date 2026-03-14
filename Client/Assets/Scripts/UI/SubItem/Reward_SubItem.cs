using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Reward_SubItem : UI_SubItem<RewardItem>
{
    enum Texts
    {
        RewardAmountText,
    }

    enum Images
    {
        RewardImage,
    }

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));
    }

    public override void SetData(RewardItem rewardItem)
    {
        base.SetData(rewardItem);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.RewardAmountText).text = $"{_data.RewardAmount}";
        GetImage((int)Images.RewardImage).sprite = Managers.Image.GetCurrencyImage(_data.RewardId);
    }
}
