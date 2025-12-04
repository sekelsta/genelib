using ProtoBuf;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SetNoteMessage {
        public required long entityId;
        public required string note;
    }
}
