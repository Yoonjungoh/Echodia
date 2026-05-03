using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers s_instance; // 유일성이 보장된다
    private static Managers Instance { get { Init(); return s_instance; } } // 유일한 매니저를 갖고온다

    private ColorManager _color = new ColorManager();
    private ConfigManager _config = new ConfigManager();
    private CooldownManager _cooldown = new CooldownManager();
    private CurrencyManager _currency = new CurrencyManager();
    private EquipmentManager _equipment = new EquipmentManager();
    private InventoryManager _inventory = new InventoryManager();
    private ImageManager _image = new ImageManager();
    private GameRoomManager _gameRoom = new GameRoomManager();
    private GameRoomObjectManager _gameRoomObject = new GameRoomObjectManager();
    private MapManager _map = new MapManager();
    private NetworkManager _network = new NetworkManager();
    private PartyManager _party = new PartyManager();
    private QuestManager _quest = new QuestManager();
    private RedDotManager _redDot = new RedDotManager();
    private RewardManager _reward = new RewardManager();
    private TimeManager _time = new TimeManager();
    private PoolManager _pool = new PoolManager();
    private InputManager _input = new InputManager();
    private DataManager _data = new DataManager();
    private ResourceManager _resource = new ResourceManager();
    private SceneManagerEx _scene = new SceneManagerEx();
    private SoundManager _sound = new SoundManager();
    private SpecDataManager _specData = new SpecDataManager();
    private UIManager _ui = new UIManager();
    private URLManager _url = new URLManager();

    public static ColorManager Color { get { return Instance._color; } }
    public static ConfigManager Config { get { return Instance._config; } }
    public static CooldownManager Cooldown { get { return Instance._cooldown; } }
    public static CurrencyManager Currency { get { return Instance._currency; } }
    public static EquipmentManager Equipment { get { return Instance._equipment; } }
    public static InventoryManager Inventory { get { return Instance._inventory; } }
    public static ImageManager Image { get { return Instance._image; } }
    public static GameRoomManager GameRoom { get { return Instance._gameRoom; } }
    public static GameRoomObjectManager GameRoomObject { get { return Instance._gameRoomObject; } }
    public static MapManager Map { get { return Instance._map; } }
    public static NetworkManager Network { get { return Instance._network; } }
    public static PartyManager Party { get { return Instance._party; } }
    public static QuestManager Quest { get { return Instance._quest; } }
    public static RedDotManager RedDot { get { return Instance._redDot; } }
    public static RewardManager Reward { get { return Instance._reward; } }
    public static TimeManager Time { get { return Instance._time; } }
    public static PoolManager Pool { get { return Instance._pool; } }
    public static DataManager Data { get { return Instance._data; } }
    public static InputManager Input { get { return Instance._input; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx Scene { get { return Instance._scene; } }
    public static SoundManager Sound { get { return Instance._sound; } }
    public static SpecDataManager SpecData { get { return Instance._specData; } }
    public static UIManager UI { get { return Instance._ui; } }
    public static URLManager URL { get { return Instance._url; } }

    void Start()
    {
        Init();
        
        CoInit().Forget();
    }

    void Update()
    {
        _network.OnUpdate();
        _input.OnUpdate();
    }

    static void Init()
    {
        if (s_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }

            DontDestroyOnLoad(go);
            s_instance = go.GetComponent<Managers>();
            InitAfterCoInit();
        }
    }

    public async UniTask CoInit()
    {
        await SpecData.DownloadDataSheetAsync();
        await Config.DownloadConfigAsync();
        SpecData.Init();    // 자동 생성 안되는 SpecData의 데이터 로딩 (보통 가공해서 쓰는 값들이 많음)
        OnAllDataReady();
    }


    public static bool IsDataReady { get; private set; }

    private static void InitAfterCoInit()
    {
        // 네트워크는 로비에 입장 시 Init
        s_instance._data.Init();
        s_instance._sound.Init();
        s_instance._resource.Init();
        // s_instance._map.Init(1);
        s_instance._gameRoomObject.Init();
        s_instance._image.Init();
        s_instance._quest.Init();
        s_instance._equipment.Init();
    }

    private void OnAllDataReady()
    {
        IsDataReady = true;
        InitAfterCoInit();
        Debug.Log("[Managers] 모든 데이터 준비 완료. 게임 시작 가능.");

        // 최초에만 적용 (이후에는 LoadScene에서 CurrentScene 자동으로 바뀜)
        // UI는 각 Scene의 SceneStarter에서 주로 호출
        // 최초만 예외
        // Managers.UI.ShowSceneUI<UI_Splash>();
        // Managers.Scene.CurrentScene = Define.Scene.Splash;
    }

    public static void Clear()
    {
        Sound.Clear();
        UI.Clear();

        Pool.Clear();

        GameRoom.Clear();
        GameRoomObject.Clear();
    }

    public IEnumerator CoDataManagerInit()
    {
        yield return null;
    }
}