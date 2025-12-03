using System;
using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    [ProtoContract]
    // TODO: Save and load these values using serverapi.WorldManager.SaveGame.StoreData<T>("key", object)
    public class AnimalSaveData {
        public EntityAgent? Animal;
        // Starts the same as the entity ID, then kept constant over things that technically change out the entity, like growing up, getting caught in a trap, or being tamed
        [ProtoMember(1)]
        public long UID;
        // Entity type code
        [ProtoMember(2)]
        public string Code = null!;
        // Custom name set by the player TODO: Include support for entity nametags mod
        [ProtoMember(3)]
        public string? Name;
        // Family tree
        [ProtoMember(4)]
        public long MotherUID;
        [ProtoMember(5)]
        public long FatherUID;
        [ProtoMember(6)]
        public long FosterUID;
        [ProtoMember(7)]
        public long[]? OffspringUIDs;
        // Date of birth
        [ProtoMember(8)]
        public double BirthTotalHours;
        // Position when last recorded
        [ProtoMember(9)]
        public BlockPos LastSeenPosition = null!;
        // Custom data
        [ProtoMember(10)]
        public TreeAttribute? Attributes;
    }
}
