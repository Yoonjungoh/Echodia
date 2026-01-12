using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers s_instance; // 유일성이 보장된다
    private static Managers Instance { get { Init(); return s_instance; } } // 유일한 매니저를 갖고온다

    private CurrencyManager _currency = new CurrencyManager();
    private GameRoomManager _gameRoom = new GameRoomManager();
    private GameRoomObjectManager _gameRoomObject = new GameRoomObjectManager();
    private LobbyManager _lobby = new LobbyManager();
    private MapManager _map = new MapManager();
    private NetworkManager _network = new NetworkManager();
    private TimeManager _time = new TimeManager();
    private WaitingRoomObjectManager _waitingRoomObject = new WaitingRoomObjectManager();
    private WaitingRoomManager _waitingRoom = new WaitingRoomManager();
    private PoolManager _pool = new PoolManager();
    private InputManager _input = new InputManager();
    private DataManager _data = new DataManager();
    private ResourceManager _resource = new ResourceManager();
    private SceneManagerEx _scene = new SceneManagerEx();
    private SoundManager _sound = new SoundManager();
    private UIManager _ui = new UIManager();
    private URLManager _url = new URLManager();

    public static CurrencyManager Currency { get { return Instance._currency; } }
    public static GameRoomManager GameRoom { get { return Instance._gameRoom; } }
    public static GameRoomObjectManager GameRoomObject { get { return Instance._gameRoomObject; } }
    public static LobbyManager Lobby { get { return Instance._lobby; } }
    public static MapManager Map { get { return Instance._map; } }
    public static NetworkManager Network { get { return Instance._network; } }
    public static TimeManager Time { get { return Instance._time; } }
    public static WaitingRoomObjectManager WaitingRoomObject { get { return Instance._waitingRoomObject; } }
    public static WaitingRoomManager WaitingRoom { get { return Instance._waitingRoom; } }
    public static PoolManager Pool { get { return Instance._pool; } }
    public static DataManager Data { get { return Instance._data; } }
    public static InputManager Input { get { return Instance._input; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx Scene { get { return Instance._scene; } }
    public static SoundManager Sound { get { return Instance._sound; } }
    public static UIManager UI { get { return Instance._ui; } }
    public static URLManager URL { get { return Instance._url; } }

    void Start()
    {
        Init();
        // 주석 해제하면 s3의 json 정보 가져옴
        //StartCoroutine(CoDataManagerInit());
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

            // 네트워크는 로비에 입장 시 Init
            s_instance._data.Init();
            s_instance._sound.Init();
            s_instance._resource.Init();
            s_instance._waitingRoom.Init();
            s_instance._map.Init();
            s_instance._gameRoomObject.Init();
        }
    }

    public static void Clear()
    {
        Sound.Clear();
        UI.Clear();

        Pool.Clear();

        WaitingRoomObject.Clear();

        GameRoom.Clear();
        GameRoomObject.Clear();
    }

    public IEnumerator CoDataManagerInit()
    {
        // 추가될 json 데이터들 가져오는 코루틴 넣어주기
        //StartCoroutine(Managers.Data.CoDownLoadJsonData());

        yield return null;
    }
}