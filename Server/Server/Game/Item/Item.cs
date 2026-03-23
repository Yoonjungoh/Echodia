

using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;

public class Item
{
    public virtual bool CanUse(Player player) { return true; }
    public virtual void Use(Player player) {; }

}