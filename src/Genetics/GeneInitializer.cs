using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;

using Genelib.Extensions;

namespace Genelib {
    public class GeneInitializer {
        public readonly string Name;
        public GenomeType ForType;
        private JsonObject? attributes;
        public float[]?[]? AutosomalFrequencies;
        public float[]?[]? BitwiseFrequencies;
        public float[]?[]? SexlinkedFrequencies;
        private ClimateSpawnCondition? climateCondition;
        private float minGeo;
        private float maxGeo = 1;
        private float minFertility;
        private float maxFertility = 1;
        private float maxForestOrShrubs = 1;

        public GeneInitializer(GenomeType type, string name, JsonObject attributes) {
            this.ForType = type;
            Name = name;
            this.attributes = attributes;
            if (attributes.KeyExists("conditions")) {
                JsonObject conditions = attributes["conditions"];
                climateCondition = conditions.AsObject<ClimateSpawnCondition>();
                if (conditions.KeyExists("minGeologicActivity")) {
                    minGeo = conditions["minGeologicActivity"].AsFloat();
                }
                if (conditions.KeyExists("maxGeologicActivity")) {
                    maxGeo = conditions["maxGeologicActivity"].AsFloat();
                }
                if (conditions.KeyExists("minFertility")) {
                    minFertility = conditions["minFertility"].AsFloat();
                }
                if (conditions.KeyExists("maxFertility")) {
                    maxFertility = conditions["maxFertility"].AsFloat();
                }
                if (conditions.KeyExists("maxForestOrShrubs")) {
                    maxForestOrShrubs = conditions["maxForestOrShrubs"].AsFloat();
                }
            }

            AutosomalFrequencies = parseFrequencies(attributes["autosomal"], type.Autosomal);

            JsonObject jsonXZ = attributes["xz"];
            SexlinkedFrequencies = parseFrequencies(jsonXZ.Exists ? jsonXZ : attributes["sexlinked"], type.XZ);

            JsonObject jsonBitwise = attributes["bitwise"];
            if (jsonBitwise.Exists) {
                BitwiseFrequencies = new float[type.Bitwise.GeneCount][];
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
                        BitwiseFrequencies[ordinal] = values;
                    }
                    else {
                        float chance = new JsonObject(jp.Value).AsFloat();
                        BitwiseFrequencies[ordinal] = new float[] { chance };
                    }
                }
            }
        }

        public bool CanSpawnAt(ClimateCondition climate, int y) {
            if (climateCondition == null) {
                return true;
            }
            bool forestOrShrubs = (climateCondition.MinForestOrShrubs <= climate.ForestDensity 
                    || climateCondition.MinForestOrShrubs <= climate.ShrubDensity)
                && (maxForestOrShrubs >= climate.ForestDensity 
                    || maxForestOrShrubs >= climate.ShrubDensity);
            int sealevel = GenelibSystem.ServerAPI!.World.SeaLevel;
            int maxheight = GenelibSystem.ServerAPI!.WorldManager.MapSizeY;
            float highY = (y + 3f) > sealevel ? (y + 3f - sealevel) / (maxheight - sealevel) : (y + 3f) / sealevel;
            float lowY = (y - 3f) > sealevel ? (y - 3f - sealevel) / (maxheight - sealevel) : (y - 3f) / sealevel;
            return forestOrShrubs
                && climateCondition.MinTemp <= climate.WorldGenTemperature
                && climateCondition.MaxTemp >= climate.WorldGenTemperature
                && climateCondition.MinRain <= climate.WorldgenRainfall
                && climateCondition.MaxRain >= climate.WorldgenRainfall
                && climateCondition.MinForest <= climate.ForestDensity 
                && climateCondition.MaxForest >= climate.ForestDensity
                && climateCondition.MinShrubs <= climate.ShrubDensity
                && climateCondition.MaxShrubs >= climate.ShrubDensity
                && climateCondition.MinY <= highY
                && climateCondition.MaxY >= lowY
                && minGeo <= climate.GeologicActivity
                && maxGeo >= climate.GeologicActivity
                && minFertility <= climate.Fertility
                && maxFertility >= climate.Fertility;

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
