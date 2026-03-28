public class DataManager
{
    public int DefaultServerId { get; set; } = 1; // 서버 선택창에 들어오면 기본으로 선택되는 서버 ID

    public void Init() { }

    public int GetMaxExpForLevelUp(int level)
    {
        return (int)Managers.SpecData.GetExpRequiredForLevel(level);
    }
}
