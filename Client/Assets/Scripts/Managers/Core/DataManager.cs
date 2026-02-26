using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using UnityEngine; // 서버/클라 모두 Newtonsoft.Json 사용 가능

public class DataManager
{
    public int DefaultServerId { get; set; } = 1; // 서버 선택창에 들어오면 기본으로 선택되는 서버 ID

    public void Init()
    {
        string path = Path.Combine(Application.dataPath, "../../Common/SpecData/LevelData.json");
        LoadLevelData(path);
    }

    public LevelDataSheet LevelDataSheet { get; private set; }

    public void LoadLevelData(string path)
    {
        string json = File.ReadAllText(path);
        LevelDataSheet = JsonConvert.DeserializeObject<LevelDataSheet>(json);
    }

    public int GetMaxExpForLevelUp(int level)
    {
        var data = LevelDataSheet.Levels.Find(x => x.Level == level);
        return data != null ? data.MaxExp : 0;
    }
}


public class LevelData
{
    public int Level { get; set; }
    public int MaxExp { get; set; }
}

public class LevelDataSheet
{
    public List<LevelData> Levels { get; set; }
}