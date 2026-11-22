#if ECHODIA_QUEST_VERIFICATION && DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Google.Protobuf.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Standalone development-player test for the existing UI. Each phase uses a fresh process.
public static class QuestRewardPlayerVerification
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        string phase = Environment.GetEnvironmentVariable("ECHODIA_QUEST_PHASE");
        if (!string.IsNullOrEmpty(phase))
            Run(phase).Forget();
    }

    static async UniTask Run(string phase)
    {
        try
        {
            if (phase != "prepare" && phase != "claim" && phase != "reconnect")
                throw new InvalidOperationException("Unknown verification phase.");
            string account = Environment.GetEnvironmentVariable("ECHODIA_QUEST_ACCOUNT") ?? "";
            string password = Environment.GetEnvironmentVariable("ECHODIA_QUEST_PASSWORD") ?? "";
            string character = Environment.GetEnvironmentVariable("ECHODIA_QUEST_CHARACTER") ?? "";
            if (!Regex.IsMatch(account, "^eqa[0-9a-f]{8}$") || character != "Q" + account.Substring(3) ||
                string.IsNullOrEmpty(password))
                throw new InvalidOperationException("A dedicated test account and character are required.");

            await Until(() => Managers.IsDataReady && Managers.Network.IsInitialized && Find<UI_Login>(), "login screen", 180);
            var login = Find<UI_Login>();
            SetField(login, "IdInputField", account);
            SetField(login, "PasswordInputField", password);
            Click(login, "LoginButton");
            if (phase == "prepare")
            {
                var idField = Field(login, "IdInputField");
                await Until(() => !login || !idField || idField.text.Length == 0 || Find<UI_ServerSelect>(), "signup or existing login");
                if (login && idField && idField.text.Length == 0)
                {
                    SetField(login, "IdInputField", account);
                    SetField(login, "PasswordInputField", password);
                    Click(login, "LoginButton");
                }
            }

            await Until(() => Find<ServerChannel_SubItem>(), "server channels");
            var channel = UnityEngine.Object.FindObjectsOfType<ServerChannel_SubItem>()
                .OrderBy(x => x.GetData().ChannelId).First();
            Click(channel, "SelectButton");
            await Until(() => Find<UI_PlayerSelect>(), "character list");
            if (phase == "prepare")
            {
                await UniTask.Delay(600, ignoreTimeScale:true);
                if (!OwnRow(character))
                {
                    Click(Find<UI_PlayerSelect>(), "CreatePlayerButton");
                    await Until(() => Find<UI_CreatePlayer>(), "character creation dialog");
                    var create = Find<UI_CreatePlayer>();
                    SetField(create, "PlayerNameInputField", character);
                    Click(create, "ConfirmButton");
                }
            }
            await Until(() => OwnRow(character), "own character listed");
            var row = OwnRow(character);
            // Accept only the exact fixture name, allowing TMP's invisible end-of-text marker.
            var selected = row.GetData();
            Debug.Log($"QUEST_QA character_list player={selected.PlayerId} gold={selected.CurrencyData.Gold} jewel={selected.CurrencyData.Jewel}");
            Click(row, "SelectButton");
            await Until(() => Find<UI_GameRoom>() && Managers.GameRoomObject.MyPlayer != null, "own character in game room");
            string recording = Environment.GetEnvironmentVariable("ECHODIA_QUEST_FRAMES");
            if (!string.IsNullOrEmpty(recording))
            {
                if (!System.IO.Path.IsPathRooted(recording))
                    throw new InvalidOperationException("Frame output requires an absolute folder.");
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Gameplay recording requires a graphics device.");
                System.IO.Directory.CreateDirectory(recording);
                RecordGameRoom(recording).Forget();
            }
            if (phase == "prepare")
            {
                await Until(() => Managers.Quest.UserQuestDataDict.ContainsKey((1, 1)), "first quest assigned");
            }
            else
            {
                Click(Find<UI_GameRoom>(), "QuestPopupButton");
                await Until(() => Find<UI_Quest>(), "quest popup");
                // Await a fresh server-backed reply even on reconnect with no eligible quest.
                bool received = false;
                Action changed = () => received = true;
                Managers.Quest.OnQuestListChanged += changed;
                try
                {
                    Managers.Network.Send(new C_RequestQuestData());
                    await Until(() => received, "quest state refreshed");
                }
                finally { Managers.Quest.OnQuestListChanged -= changed; }

                if (phase == "claim")
                {
                    var item = QuestRow();
                    if (!item || item.GetData().QuestStatus != QuestStatus.Completed)
                        throw new InvalidOperationException("Fixture quest is not eligible.");
                    Click(item, "SelectButton");
                    await Until(() => ActiveButton(Find<UI_Quest>(), "CompleteButton"), "claim button enabled");
                    if (!string.IsNullOrEmpty(recording))
                        await UniTask.Delay(1500, ignoreTimeScale:true);
                    Click(Find<UI_Quest>(), "CompleteButton");
                    await Until(() => Find<UI_Reward>() && !QuestRow(), "reward confirmation");
                }
                else
                {
                    var item = QuestRow();
                    if (item && item.GetData().QuestStatus != QuestStatus.RewardClaimed)
                        throw new InvalidOperationException("Claim became available after reconnect.");
                    if (item)
                    {
                        Click(item, "SelectButton");
                        if (ActiveButton(Find<UI_Quest>(), "CompleteButton"))
                            throw new InvalidOperationException("Reward button enabled after reconnect.");
                    }
                }

                // A disposable character starts with 1000 gold, no jewels, no equipment or items.
                await Until(() => Managers.Currency.Get(CurrencyType.Gold) == 1012 &&
                    Managers.Currency.Get(CurrencyType.Jewel) == 34 && ExpectedInventory(), "persisted reward balances");
                Debug.Log($"QUEST_QA verified phase={phase} gold=1012 jewel=34 equipment=7 consumable=5 misc=3");
                string screenshot = Environment.GetEnvironmentVariable("ECHODIA_QUEST_SCREENSHOT");
                if (!string.IsNullOrEmpty(screenshot) && System.IO.Path.IsPathRooted(screenshot) &&
                    SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    ScreenCapture.CaptureScreenshot(screenshot);
                    await Until(() => System.IO.File.Exists(screenshot) && new System.IO.FileInfo(screenshot).Length > 0, "capture saved", 8);
                }
            }
            if (!string.IsNullOrEmpty(recording))
                await UniTask.Delay(1800, ignoreTimeScale:true);
            Debug.Log("QUEST_QA PASS " + phase);
            Application.Quit(0);
        }
        catch (Exception error)
        {
            Debug.LogError("QUEST_QA FAIL " + phase + " " + error.Message);
            Application.Quit(2);
        }
    }

    static bool ExpectedInventory()
    {
        var items = new[] { ItemType.Equipment, ItemType.Consumable, ItemType.Misc }
            .SelectMany(type => Managers.Inventory.GetItems(type).Values)
            .GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
        if (items.Count != 9) return false;
        return Enumerable.Range(100000, 7).All(id => items.TryGetValue(id, out var count) && count == 1) &&
            items.TryGetValue(200000, out var consumables) && consumables == 5 &&
            items.TryGetValue(300000, out var misc) && misc == 3;
    }

    static PlayerSelectInfo_SubItem OwnRow(string name) =>
        UnityEngine.Object.FindObjectsOfType<PlayerSelectInfo_SubItem>()
            .SingleOrDefault(row => row.GetData()?.Name.TrimEnd('\u200B') == name);

    static Quest_SubItem QuestRow() => UnityEngine.Object.FindObjectsOfType<Quest_SubItem>()
        .FirstOrDefault(row => row.GetData()?.MainQuestId == 1 && row.GetData().SubQuestId == 1);

    static T Find<T>() where T : Component => UnityEngine.Object.FindObjectOfType<T>();
    static async UniTask RecordGameRoom(string directory)
    {
        float started = Time.realtimeSinceStartup;
        // The recording starts in the game room; neither login fields nor account data are captured.
        for (int i = 0; i < 120 && Find<UI_GameRoom>(); ++i)
        {
            int elapsed = Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, $"frame-{elapsed:D7}.png"));
            await UniTask.Delay(250, ignoreTimeScale:true);
        }
    }
    static TMP_InputField Field(Component owner, string name) =>
        owner.GetComponentsInChildren<TMP_InputField>(true).Single(field => field.name == name);
    static void SetField(Component owner, string name, string value)
    {
        var field = Field(owner, name);
        field.text = value;
        field.ForceLabelUpdate();
    }

    static Button ActiveButton(Component owner, string name) =>
        owner.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name && button.isActiveAndEnabled && button.IsInteractable());
    static void Click(Component owner, string name)
    {
        var button = ActiveButton(owner, name);
        if (!button) throw new InvalidOperationException("Unavailable button: " + name);
        button.onClick.Invoke();
    }

    static async UniTask Until(Func<bool> predicate, string step, int seconds = 30)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate())
        {
            if (Time.realtimeSinceStartup >= deadline)
                throw new TimeoutException("Timed out at " + step);
            await UniTask.Delay(100, ignoreTimeScale:true);
        }
        Debug.Log("QUEST_QA reached " + step);
    }
}
#endif
