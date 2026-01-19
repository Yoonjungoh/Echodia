using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomObjectManager
{
    public int PlayerId { get; set; }    // 내 캐릭터를 생성했을 때 부여 받은 고유 Id (오브젝트 Id는 CreatureController의 Id고 이 값은 매번 바뀜 )

    public MyPlayerController MyPlayer { get; set; }    

    private Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();

    public Dictionary<int, GameObject> Objects { get { return _objects; } set { _objects = value; } }

    private HashSet<string> _damagedObjectName = new HashSet<string>()
    {
        "Player",
        "Monster"
    };

    private HashSet<string> _IgnoredLayerByProjectileName = new HashSet<string>()
    {
        "Ground",
    };

    private HashSet<int> _damagedObjects = new HashSet<int>();

    private HashSet<int> _IgnoredLayerByProjectiles = new HashSet<int>();

    private Dictionary<int, int> _projectileOwners = new Dictionary<int, int>();    // key = 투사체, value = 주인

    public GameObjectType GetObjectTypeById(int id)
    {
        int type = (id >> 24) & 0x7F;
        return (GameObjectType)type;
    }

    public void Add(ObjectState objectState, bool isMyPlayer = false)
    {
        if (MyPlayer != null && MyPlayer.Id == objectState.ObjectId)
            return;

        if (_objects.ContainsKey(objectState.ObjectId))
            return;

        GameObjectType objectType = GetObjectTypeById(objectState.ObjectId);
        Vector3 position = new Vector3(objectState.Position.X, objectState.Position.Y, objectState.Position.Z);
        Quaternion rotation = new Quaternion(objectState.Rotation.X, objectState.Rotation.Y, objectState.Rotation.Z, objectState.Rotation.W);

        if (objectType == GameObjectType.Player && isMyPlayer)
        {
            GameObject player = Managers.Resource.Instantiate($"Creatures/Players/MyPlayers/MyPlayer_1", position, rotation);
            MyPlayer = player.GetComponent<MyPlayerController>();

            MyPlayer.ObjectState = objectState;
            MyPlayer.GameObjectType = objectType;

            _objects.Add(objectState.ObjectId, MyPlayer.gameObject);
            Debug.Log($"소환: {MyPlayer.Id}, isMyPlayer: {isMyPlayer}");

            Camera.main.GetComponent<CameraController>().Init();   // 캐릭터 소환 후 카메라 부착
        }
        else if (objectType == GameObjectType.Player && isMyPlayer == false)
        {
            GameObject player = Managers.Resource.Instantiate($"Creatures/Players/OtherPlayers/OtherPlayer_1", position, rotation);
            OtherPlayerController otherPlayer = player.GetComponent<OtherPlayerController>();

            otherPlayer.ObjectState = objectState;
            otherPlayer.GameObjectType = objectType;

            otherPlayer.SetServerState(
                objectState.Position,
                objectState.Rotation,
                objectState.Velocity,
                objectState.ServerReceivedTime
            );
            _objects.Add(objectState.ObjectId, otherPlayer.gameObject);
        }
        else if (objectType == GameObjectType.Monster)
        {
            GameObject go = Managers.Resource.Instantiate($"Creatures/Monsters/{objectState.MonsterType}", position, rotation);
            MonsterController monster = go.GetComponent<MonsterController>();

            monster.ObjectState = objectState;
            monster.GameObjectType = objectType;

            monster.SetServerState(
                objectState.Position,
                objectState.Rotation,
                objectState.Velocity,
                objectState.ServerReceivedTime
            );
            _objects.Add(objectState.ObjectId, monster.gameObject);
        }
        else if (objectType == GameObjectType.Projectile)
        {
            GameObject go = Managers.Resource.Instantiate($"Creatures/Projectiles/{objectState.ProjectileType}", position, rotation);
            ProjectileController projectile = go.GetComponent<ProjectileController>();
            
            projectile.ObjectState = objectState;
            projectile.GameObjectType = objectType;

            projectile.SetServerState(
                objectState.Position,
                objectState.Rotation,
                objectState.Velocity,
                objectState.ServerReceivedTime
            );

            _objects.Add(objectState.ObjectId, projectile.gameObject);
            _projectileOwners.Add(objectState.ObjectId, objectState.OwnerId);
        }
    }

    // 게임룸에서 사라질 때 반드시 호출하는 함수 (이것만 호출하면 됨)
    public void Remove(int id, bool isDead)
    {
        if (MyPlayer != null && MyPlayer.Id == id)
            return;

        if (_objects.ContainsKey(id) == false)
            return;

        GameObject go = FindById(id);
        if (go == null)
            return;

        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Managers.Resource.Destroy(go);
            return;
        }
        _objects.Remove(id);
        GameObjectType gameObjectType = GetObjectTypeById(id);
        if (gameObjectType == GameObjectType.Projectile)
        {
            _projectileOwners.Remove(id);
        }

        if (isDead == false)
        {
            Managers.Resource.Destroy(go);
        }
    }

    public GameObject FindById(int id)
    {
        GameObject go = null;
        _objects.TryGetValue(id, out go);
        return go;
    }

    public GameObject Find(Func<GameObject, bool> condition)
    {
        foreach (GameObject obj in _objects.Values)
        {
            if (condition.Invoke(obj))
                return obj;
        }

        return null;
    }

    public bool IsDamageable(int layer)
    {
        return _damagedObjects.Contains(layer);
    }

    public bool IsLayerIgnoredByProjectile(int layer)
    {
        return _IgnoredLayerByProjectiles.Contains(layer);
    }

    public void HandleChangeCreatureState(int objectId, CreatureState creatureState)
    {
        _objects.TryGetValue(objectId, out GameObject go);
        if (go == null)
        {
            Debug.Log($"{objectId}가 null 입니다.");
            return;
        }

        CreatureController creatureController = go.GetComponent<CreatureController>();
        if (creatureController == null)
        {
            Debug.Log($"{objectId}가 CreatureController가 아닙니다.");
            return;
        }

        creatureController.ObjectState.CreatureState = creatureState;
    }

    public int GetProjectileOwnerId(int projectileId)
    {
        if (_projectileOwners.TryGetValue(projectileId, out int ownerId))
        {
            return ownerId;
        }

        return -1;  // 없다는 의미
    }

    public void Init()
    {
        foreach (string objectName in _damagedObjectName)
        {
            _damagedObjects.Add(LayerMask.NameToLayer(objectName));
        }
        foreach (string objectName in _IgnoredLayerByProjectileName)
        {
            _IgnoredLayerByProjectiles.Add(LayerMask.NameToLayer(objectName));
        }
    }

    public void Clear()
    {
        foreach (GameObject obj in _objects.Values)
        {
            if (obj != null)
                Managers.Resource.Destroy(obj);
        }
        _objects.Clear();
    }
}