using Genelib.Network;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Linq;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Genelib {
    [ProtoContract]
    public class GenomeType {
        internal static bool assetsReceived = false;
        internal static volatile Dictionary<AssetLocation, GenomeType> loaded = new Dictionary<AssetLocation, GenomeType>();
        private static Dictionary<string, GeneInterpreter> interpreterMap = new Dictionary<string, GeneInterpreter>();

        public static void RegisterInterpreter(GeneInterpreter interpreter) {
            interpreterMap[interpreter.Name] = interpreter;
        }

        [ProtoMember(1)]
        public NameMapping Autosomal { get; protected set; }
        [ProtoMember(2)]
        public NameMapping XZ { get; protected set; }
        [ProtoMember(3)]
        public NameMapping YW { get; protected set; }
        [ProtoMember(4)]
        public NameGroupMapping Anonymous { get; protected set; }
        [ProtoMember(5)]
        public NameGroupMapping Bitwise { get; protected set; }

        // Not serialized, so make sure not to try accessing from client
        private Dictionary<string, GeneInitializer> initializers = new Dictionary<string, GeneInitializer>();
        public GeneInterpreter[] Interpreters { get; protected set; }

        [ProtoMember(4)]
        public SexDetermination SexDetermination { get; protected set; } = SexDetermination.XY;

        private AlleleFrequencies? defaultFrequencies;
        public AlleleFrequencies DefaultFrequencies {
            get {
                if (defaultFrequencies == null) {
                    defaultFrequencies = new AlleleFrequencies(this);
                }
                return defaultFrequencies;
                }
            private set {
                defaultFrequencies = value;
            }
        }

        [ProtoMember(5)]
        public string Name { get; private set; }

        [ProtoMember(6)]
        public string[] InterpreterNames {
            get => Interpreters.Select(x => x.Name).ToArray();
            set => Interpreters = value.Select(x => interpreterMap[x]).ToArray();
        }

        public GenomeType() {
            Name = "uninitialized";
            Autosomal = new NameMapping();
            XZ = new NameMapping();
            YW = new NameMapping();
            Anonymous = new NameGroupMapping();
            Bitwise = new NameGroupMapping();
            Interpreters = new GeneInterpreter[0];
        }

        private GenomeType(string name, JsonObject attributes) {
            this.Name = name;
            JsonObject genes = attributes["genes"];
            Autosomal = parse(genes, "autosomal");
            XZ = parse(genes, "xz");
            YW = parse(genes, "yw");
            Anonymous = parseGrouped(genes, "anonymous");
            Bitwise = parseGrouped(genes, "bitwise");

            if (attributes.KeyExists("initializers")) {
                JsonObject initialization = attributes["initializers"];
                foreach (JProperty jp in ((JObject) initialization.Token).Properties()) {
                    initializers[jp.Name] = new GeneInitializer(this, jp.Name, new JsonObject(jp.Value));
                }
            }
            if (attributes.KeyExists("sexdetermination")) {
                SexDetermination = SexDeterminationExtensions.Parse(attributes["sexdetermination"].AsString());
            }
            string[] interpreterNames = attributes["interpreters"].AsArray<string>();
            Interpreters = new GeneInterpreter[interpreterNames.Length];
            for (int i = 0; i < interpreterNames.Length; ++i) {
                Interpreters[i] = interpreterMap[interpreterNames[i]];
            }
        }

        private NameMapping parse(JsonObject json, string key) {
            if (!json.KeyExists(key)) {
                return new NameMapping();
            }
            JsonObject[] genes = json[key].AsArray();
            string[] geneArray = new string[genes.Length];
            string[][] alleleArrays = new string[genes.Length][];
            for (int gene = 0; gene < genes.Length; ++gene) {
                JProperty jp = ((JObject) genes[gene].Token).Properties().First();
                geneArray[gene] = jp.Name;
                alleleArrays[gene] = new JsonObject(jp.Value).AsArray<string>();
            }
            return new NameMapping(geneArray, alleleArrays);
        }

        private NameGroupMapping parseGrouped(JsonObject json, string key) {
            if (!json.KeyExists(key)) {
                return new NameGroupMapping();
            }
            JsonObject[] genes = json[key].AsArray();
            Tuple<string, int>[] groups = new Tuple<string, int>[genes.Length];
            for (int gene = 0; gene < genes.Length; ++gene) {
                JProperty jp = ((JObject) genes[gene].Token).Properties().First();
                string name = jp.Name;
                int count = new JsonObject(jp.Value).AsInt();
                if (count < 0) {
                    throw new Exception(name + ": Can't have a negative number of genes!");
                }
                groups[gene] = new Tuple<string, int>(name, count);
            }
            return new NameGroupMapping(groups);
        }

        public static void Load(IAsset asset) {
            AssetLocation key = asset.Location.CopyWithPathPrefixAndAppendixOnce("genetics/", ".json");
            if (!loaded.ContainsKey(key)) {
                loaded[key] = new GenomeType(key.CopyWithPath(key.PathOmittingPrefixAndSuffix("genetics/", ".json")).ToString(), JsonObject.FromJson(asset.ToText()));
            }
        }

        public static GenomeType Get(AssetLocation location) {
            AssetLocation key = location.CopyWithPathPrefixAndAppendixOnce("genetics/", ".json");
            return loaded[key];
        }

        public GeneInitializer Initializer(string name) {
            return initializers[name];
        }

        public GeneInitializer? ChooseInitializer(string[]? initializerNames, ClimateCondition climate, int y, Random random) {
            List<GeneInitializer> valid = new List<GeneInitializer>();
            // If no list provided, default to all being valid
            if (initializerNames == null) {
                foreach (KeyValuePair<string, GeneInitializer> entry in initializers) {
                    addIfValid(valid, climate, y, entry.Value);
                }
            }
            else {
                for (int i = 0; i < initializerNames.Length; ++i) {
                    addIfValid(valid, climate, y, initializers[initializerNames[i]]);
                }
            }
            if (valid.Count == 0) {
                return null;
            }
            return valid[random.Next(valid.Count)];
        }

        private void addIfValid(List<GeneInitializer> valid, ClimateCondition climate, int y, GeneInitializer ini) {
            if (ini.CanSpawnAt(climate, y)) {
                valid.Add(ini);
            }
        }

        internal static void OnAssetsRecievedClient(GenomeTypesMessage message) {
            assetsReceived = true;
            if (message == null) throw new Exception("Null GenomeTypesMessage received, this could mean an error on the server");
            if (message.AssetLocations == null || message.GenomeTypes == null) return; // This happens if 0 genome types are loaded

            for (int i = 0; i < message.AssetLocations.Length; ++i) {
                loaded[message.AssetLocations[i]] = message.GenomeTypes[i];
            }
        }
    }
}
