using ProtoBuf;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNameMessage {
        public required long entityId;
        public required string name;
    }
}
