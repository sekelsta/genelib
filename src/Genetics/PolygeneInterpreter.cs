using System;
using Genelib.Extensions;
using Vintagestory.API.Common.Entities;

#nullable enable

namespace Genelib {
    public class PolygeneInterpreter : GeneInterpreter {
        public string Name => "Polygenes";

        void GeneInterpreter.Finalize(Genome genome, AlleleFrequencies frequencies, Random random) {
            Range range = genome.Type.Anonymous.TryGetRange("deleterious");

            for (int i = 2 * range.Start.Value; i < 2 * range.End.Value; ++i) {
                if (random.NextSingle() < GenelibConfig.Instance.InbreedingResistance) {
                    genome.anonymous[i] = 0;
                }
            }
            for (int i = range.Start.Value; i < range.End.Value; ++i) {
                if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1]) {
                    genome.anonymous[2 * i] = 0;
                }
            }
        }

        private int countVitalityHomozygotes(Genome genome) {
            Range range = genome.Type.Anonymous.TryGetRange("deleterious");

            int duplicates = 0;
            for (int i = range.Start.Value; i < range.End.Value; ++i) {
                if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1] && genome.anonymous[2 * i] != 0) {
                    duplicates += 1;
                }
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
                    if (genome.anonymous[2 * i] == genome.anonymous[2 * i + 1]) {
                        repeats += 1;
                    }
                }
                float coi = repeats / (float)numGenes;
                entity.WatchedAttributes.GetOrAddTreeAttribute("genetics").SetFloat("coi", coi);
            }
        }
    }
}
