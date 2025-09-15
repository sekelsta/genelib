using System;
using Genelib.Extensions;
using Vintagestory.API.Common.Entities;

namespace Genelib {
    public class PolygeneInterpreter : GeneInterpreter {
        public string Name => "Polygenes";

        void GeneInterpreter.FinalizeSpawn(Genome genome, AlleleFrequencies frequencies, Random random) {
            Range range = genome.Type.Anonymous.TryGetRange("deleterious");

            for (int gene = range.Start.Value; gene < range.End.Value; ++gene) {
                bool homozygous = true;
                for (int n = 0; n < genome.Ploidy; ++n) {
                    if (random.NextSingle() < GenelibConfig.Instance.InbreedingResistance) {
                        genome.Anonymous[gene, n] = 0;
                    }
                    homozygous = homozygous && genome.Anonymous[gene, n] == genome.Anonymous[gene, 0];
                }
                if (homozygous) genome.Anonymous[gene, 0] = 0;
            }
        }

        private int countVitalityHomozygotes(Genome genome) {
            Range range = genome.Type.Anonymous.TryGetRange("deleterious");

            int duplicates = 0;
            for (int i = range.Start.Value; i < range.End.Value; ++i) {
                bool homozygous = true;
                for (int n = 0; n < genome.Ploidy; ++n) {
                    homozygous = homozygous && genome.Anonymous[gene, n] == genome.Anonymous[gene, 0];
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

            Range range = genome.Type.Bitwise.TryGetRange("coi");
            int numGenes = range.End.Value - range.Start.Value;
            if (numGenes > 0) {
                int repeats = 0;
                for (int i = 0; i < numGenes; ++i) {
                    if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1]) { // TODO: Update to use the bitwise genes
                        repeats += 1;
                    }
                }
                float coi = repeats / (float)numGenes;
                entity.WatchedAttributes.GetOrAddTreeAttribute("genetics").SetFloat("coi", coi);
            }
        }
    }
}
