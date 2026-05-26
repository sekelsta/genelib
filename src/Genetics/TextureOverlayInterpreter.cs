using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class TextureOverlayInterpreter : GeneInterpreter {
        public const string Code = "TextureOverlay";
        public string Name => Code;

        public struct GeneCondition {
            public int GeneID;
            // At least one allele must match this
            public byte AlleleID;
            // All other alleles (could be polyploid or haploid) must match this, unless -1
            public int OtherAlleleID = -1;

            public bool Invert = false;

            public GeneCondition() { }

            public bool Matches(byte[,] genes) {
                bool mainFound = false;
                for (int n = 0; n < genes.GetLength(1); ++n) {
                    byte allele = genes[GeneID, n];

                    if (OtherAlleleID != -1 && allele != OtherAlleleID && (mainFound || allele != AlleleID)) {
                        return !Invert;
                    }

                    mainFound = mainFound || allele == AlleleID;
                }

                return mainFound ^ Invert;
            }
        }

        // One variant will be randomly chosen for each individual to be the overlay
        // Null only if the json is ill-formatted, but we'd still like not to crash in that situation
        protected BlendedOverlayTexture[]? textureVariants;

        // Genome must satisfy all these conditions for the overlay to be applied
        protected GeneCondition[] conditions = [];

        // If set, these overlays will only be applied to this specified material
        protected string? material = null;

        /// <summary>
        /// Used for saving which randomly chosen variant to use into entity data
        /// </summary>
        protected string id = "";


        public TextureOverlayInterpreter(JsonObject config, GenomeType genomeType, AssetLocation forCode) {
            material = config["material"].AsString();

            id = config["id"].AsString() ?? "";
            if (id == "") {
                GenelibSystem.API.Logger.Warning("Json error: No id provided for TextureOverlay on genetics behavior of entity " + forCode + ". This affects texture randomization across saving and loading.");
            }

            textureVariants = config["textureVariants"].AsArray<BlendedOverlayTexture>(null, forCode.Domain)!;
            if (textureVariants == null) {
                GenelibSystem.API.Logger.Warning("Json error: No textureVariants provided for TextureOverlay on genetics behavior of " + forCode);
            }
            else if (!Array.TrueForAll(textureVariants, x => x != null)) {
                GenelibSystem.API.Logger.Warning("Json error: A texture overlay of id " + id + " for entity " + forCode + " includes an invalid overlay");
                textureVariants = textureVariants.Where(x => x != null).ToArray();
            }

            JsonObject[]? jsonConditions = config["conditions"].AsArray();
            if (jsonConditions != null) {
                conditions = new GeneCondition[jsonConditions.Length];
                for (int i = 0; i < jsonConditions.Length; ++i) {
                    string? geneName = jsonConditions[i]["gene"].AsString();
                    if (geneName == null) {
                        GenelibSystem.API.Logger.Warning("Json error: Gene condition in texture overlay " + id + " for entity " + forCode + " is missing which gene to check");
                        continue;
                    }
                    conditions[i].GeneID = genomeType.Autosomal.GeneID(geneName);

                    string? hasAlleleName = jsonConditions[i]["hasAllele"].AsString();
                    string? otherAlleleName = jsonConditions[i]["otherAllele"].AsString();
                    string? homozygousAlleleName = jsonConditions[i]["homozygous"].AsString();
                    if (homozygousAlleleName != null) {
                        hasAlleleName = homozygousAlleleName;
                        otherAlleleName = homozygousAlleleName;
                    }

                    if (hasAlleleName != null) {
                        conditions[i].AlleleID = genomeType.Autosomal.AlleleID(conditions[i].GeneID, hasAlleleName);
                    }
                    if (otherAlleleName != null) {
                        conditions[i].OtherAlleleID = genomeType.Autosomal.AlleleID(conditions[i].GeneID, otherAlleleName);
                    }

                    conditions[i].Invert = jsonConditions[i]["invert"].AsBool(false);
                }
            }
        }

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            if (textureVariants == null || textureVariants.Length <= 1) return;

            string key = id + "OverlayIndex";
            if (!genetics.entity.WatchedAttributes.HasAttribute(key)) {
                genetics.entity.WatchedAttributes.SetInt(key, genetics.entity.World.Rand.Next(textureVariants.Length));
            }
        }

        void GeneInterpreter.GatherTextureOverlays(EntityBehaviorGenetics genetics, ref List<BlendedOverlayTexture>? overlays, string material) {
            if (this.material != null && material != this.material) return;

            foreach (var condition in conditions) {
                if (!condition.Matches(genetics.Genome.Autosomal)) return;
            }

            if (textureVariants == null) return;

            int variant = genetics.entity.WatchedAttributes.GetInt(id + "OverlayIndex");
            overlays ??= new();
            overlays.Add(textureVariants[variant % textureVariants.Length]);
        }
    }
}
