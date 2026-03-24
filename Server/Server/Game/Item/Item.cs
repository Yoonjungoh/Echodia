

using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;

public class Item
{
    public virtual bool CanUse(Player player, int slotIndex, bool needSendPacket = false) { return true; }
    public virtual void Use(Player player, int slotIndex) {; }

}