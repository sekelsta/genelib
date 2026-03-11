using System;
using Genelib.Extensions;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class PolygeneInterpreter : GeneInterpreter {
        public string Name => "Polygenes";

        void GeneInterpreter.FinalizeSpawn(Genome genome, GeneInitializer initializer, Random random) {
            genome.Anonymous.TryGetAttribute("deleterious", out IAttribute attribute);
            byte[,]? deleterious = (attribute as ByteArray2DAttribute)?.value;
            if (deleterious == null) return;

            for (int gene = 0; gene < deleterious.GetLength(0); ++gene) {
                bool homozygous = true;
                for (int n = 0; n < genome.Ploidy; ++n) {
                    if (random.NextSingle() < GenelibConfig.Instance.InbreedingResistance) {
                        deleterious[gene, n] = 0;
                    }
                    homozygous = homozygous && deleterious[gene, n] == deleterious[gene, 0];
                }
                if (homozygous) deleterious[gene, 0] = 0;
            }
        }

        private int countVitalityHomozygotes(Genome genome) {
            genome.Anonymous.TryGetAttribute("deleterious", out IAttribute attribute);
            byte[,]? deleterious = (attribute as ByteArray2DAttribute)?.value;
            if (deleterious == null) return 0;

            int duplicates = 0;
            for (int gene = 0; gene < deleterious.GetLength(0); ++gene) {
                bool homozygous = true;
                for (int n = 0; n < genome.Ploidy; ++n) {
                    homozygous = homozygous && deleterious[gene, n] == deleterious[gene, 0];
                }
                if (homozygous) duplicates += 1;
            }
            return duplicates;
        }

        bool GeneInterpreter.IsEmbryonicLethal(Genome genome) {
            return countVitalityHomozygotes(genome) >= 4;
        }

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;

            int numGenes = genome.Type.Bitwise.TryGetValue("coi");
            if (numGenes > 0) {
                int repeats = genome.BitwiseHomozygotes("coi");
                float coi = repeats / (float)numGenes;
                entity.WatchedAttributes.GetOrAddTreeAttribute("genetics").SetFloat("coi", coi);
            }
        }
    }
}
