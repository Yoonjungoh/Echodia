using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WaitingRoomObjectManager
{
    public int UserId { get; set; }  // 현재 UserId랑 Player의 Id랑 같이 쓰는 중
    public MyPlayerController MyPlayer { get; set; }
    private Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();
    public Dictionary<int, GameObject> Objects { get { return _objects; } set { _objects = value; } }

    public GameObjectType GetObjectTypeById(int id)
    {
        int type = (id >> 24) & 0x7F;
        return (GameObjectType)type;
    }

    public void Add(ObjectState objectState, bool isMyPlayer = false)
    {
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
    }

    public void Remove(int id)
    {
        GameObject go = FindById(id);
        if (go == null)
            return;

        _objects.Remove(id);
        Managers.Resource.Destroy(go);
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