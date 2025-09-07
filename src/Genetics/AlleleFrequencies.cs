using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

using Vintagestory.API.Datastructures;

using Genelib.Extensions;

namespace Genelib {
    public class AlleleFrequencies {
        public GenomeType ForType { get; protected set; }
        public float[]?[]? Autosomal { get; protected set; }
        public float[]?[]? Bitwise { get; protected set; }
        public float[]?[]? XZ { get; protected set; }
        public float[]?[]? YW { get; protected set; }

        public AlleleFrequencies(GenomeType type) {
            ForType = type;
        }

        public AlleleFrequencies(GenomeType type, JsonObject json) : this(type) {
            Autosomal = parseFrequencies(json["autosomal"], type.Autosomal);

            JsonObject jsonXZ = json["xz"];
            XZ = parseFrequencies(jsonXZ.Exists ? jsonXZ : json["sexlinked"], type.XZ);

            YW = parseFrequencies(json["yw"], type.YW);

            JsonObject jsonBitwise = json["bitwise"];
            if (jsonBitwise.Exists) {
                Bitwise = new float[type.Bitwise.GeneCount][];
                foreach (JProperty jp in ((JObject) jsonBitwise.Token).Properties()) {
                    string geneGroup = jp.Name;
                    int groupSize = type.Bitwise.GroupSize(geneGroup);
                    int ordinal = type.Bitwise.GroupOrdinal(geneGroup);
                    if (jp.Value.Type == JTokenType.Array) {
                        float[]? values = jp.Value.ToObject<float[]>();
                        if (values == null) {
                            throw new Exception("Improper format of values for initializing bitwise gene group " + geneGroup + " in genome type " + type.Name);
                        }
                        if (values.Length > groupSize || values.Length < 1) {
                            throw new Exception("Incorrect number of values for initializing bitwise gene group " + geneGroup + " in genome type " + type.Name);
                        }
                        Bitwise[ordinal] = values;
                    }
                    else {
                        float chance = new JsonObject(jp.Value).AsFloat();
                        Bitwise[ordinal] = new float[] { chance };
                    }
                }
            }
        }

        private float[]?[]? parseFrequencies(JsonObject json, NameMapping mappings) {
            if (!json.Exists) {
                return null;
            }

            float[]?[] frequencies = new float[mappings.GeneCount][];
            foreach (JProperty jp in ((JObject) json.Token).Properties()) {
                string geneName = jp.Name;
                int geneID = mappings.GeneID(geneName);
                string? defaultAlleleName = null;
                JObject jsonFrequencies = (JObject) jp.Value;
                List<float> list = new List<float>();
                if (jsonFrequencies.ContainsKey("default")) {
                    object? o = (jsonFrequencies.GetValue("default") as JValue)?.Value;
                    if (o is string) {
                        defaultAlleleName = (string) o;
                    }
                }
                int defaultAlleleID = defaultAlleleName == null ? 0 : mappings.AlleleID(geneID, defaultAlleleName);
                foreach (JProperty jf in jsonFrequencies.Properties()) {
                    string alleleName = jf.Name;
                    if (alleleName == "default" && defaultAlleleName != null) {
                        continue;
                    }
                    int alleleID = mappings.AlleleID(geneID, alleleName);
                    list.EnsureSize(alleleID + 1);
                    list[alleleID] = new JsonObject(jf.Value).AsFloat();
                }
                float sum = 0;
                foreach (float f in list) {
                    sum += f;
                }
                list.EnsureSize(defaultAlleleID + 1);
                list[defaultAlleleID] = Math.Max(0, 1 - sum);
                float s = 1;
                if (sum > 1) {
                    s = 1 / sum;
                }

                frequencies[geneID] = new float[list.Count];
                frequencies[geneID]![0] = s * list[0];
                for (int i = 1; i < frequencies[geneID]!.Length; ++i) {
                    frequencies[geneID]![i] = frequencies[geneID]![i - 1] + s * list[i];
                }
            }
            return frequencies;
        }
    }
}
