using ProtoBuf;

namespace NightVisionToggle
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NightVisionTogglePacket
    {
        public bool Enabled;
    }
}
