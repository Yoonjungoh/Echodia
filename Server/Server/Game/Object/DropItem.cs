using Google.Protobuf.Protocol;

namespace Server.Game
{
    public class DropItem : GameObject
    {
        public int ItemId { get; set; }     // SpecData item ID
        public int Count { get; set; }

        public DropItem()
        {
            ObjectType = GameObjectType.DropItem;
        }
    }
}
