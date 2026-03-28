using Google.Protobuf.Protocol;

namespace Server.Game
{
    public class DropItem : GameObject, IPoolable
    {
        public int ItemId { get; set; }     // SpecData item ID
        public int Count { get; set; }

        public DropItem()
        {
            ObjectType = GameObjectType.DropItem;
        }

        public void Reset()
        {
            ItemId = 0;
            Count = 0;
            GameRoom = null;
        }
    }
}
