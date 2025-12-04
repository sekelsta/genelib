using Genelib;
using ProtoBuf;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Vintagestory.API.Common;

namespace Genelib.Network {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class GenomeTypesMessage {
        public required string[] AssetLocations;
        public required GenomeType[] GenomeTypes;

        public GenomeTypesMessage() {}

        [SetsRequiredMembers]
        public GenomeTypesMessage(Dictionary<AssetLocation, GenomeType> types) {
            AssetLocations = new string[types.Count];
            GenomeTypes = new GenomeType[types.Count];

            int i = 0;
            foreach (var item in types) {
                AssetLocations[i] = item.Key;
                GenomeTypes[i] = item.Value;
                i++;
            }
        }
    }
}
