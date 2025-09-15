using Genelib.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Genelib {
    public class Genome {
        public GenomeType Type  { get; private set; }
        public readonly int Ploidy = 2;
        // Dimensions are [genes, ploidy]
        public byte[,] Autosomal { get; private set; }
        public byte[,] Anonymous { get; private set; }
        public byte[,] XZ { get; private set; }
        public byte[,]? YW { get; private set; }
        // A compact way to store numerous genes each with only 2 possible alleles
        // Format: Basic layout similar to autosomal genes (gene copies grouped), but compressed down to where each allele is only 1 bit.
        // Across the bytes in the array the order is as you'd expect, starting at index 0 and increasing.
        // Within each byte, the order is right to left - 0th is 0 bitshifts from the right, 1 is one bitshift from the right, etc.
        public byte[] Bitwise { get; private set; }

        // Checks if any allele matches any of the given alleles, for an autosomal gene
        public bool HasAllele(int gene, params byte[] alleles) {
            bool result = false;
            for (int n = 0; n < Ploidy; ++n) {
                foreach (byte a in alleles) {
                    result = result || Autosomal[gene, n] == a;
                }
            }
            return result;
        }

        // Checks if every allele matches at least one of the given alleles, for an autosomal gene
        public bool HasOnlyAlleles(int gene, params byte[] alleles) {
            bool result = true;
            for (int n = 0; n < Ploidy; ++n) {
                bool matches = false;
                foreach (byte a in alleles) {
                    matches = matches || Autosomal[gene, n] == a;
                }
                result = result && matches;
            }
            return result;
        }

        // Checks if any allele matches any of the given alleles, for a gene on the X or Z chromosome
        public bool HasSexlinked(int gene, params byte[] alleles) {
            bool result = false;
            for (int n = 0; n < XZ.GetLength(1); ++n) {
                foreach (byte a in alleles) {
                    result = result || XZ[gene, n] == a;
                }
            }
            return result;
        }

        // Checks if every allele matches at least one of the given alleles, for a gene on the X or Z chromosome
        public bool HasOnlySexlinked(int gene, params byte[] alleles) {
            bool result = false;
            for (int n = 0; n < XZ.GetLength(1); ++n) {
                bool matches = false;
                foreach (byte a in alleles) {
                    matches = matches || XZ[gene, n] == a;
                }
                result = result && matches;
            }
            return result;
        }

        // Checks if any allele matches any of the given alleles, for a gene on the Y or W chromosome
        [MemberNotNull(nameof(YW))]
        public bool HasHeterogametic(int gene, params byte[] alleles) {
            ArgumentNullException.ThrowIfNull(YW);
            bool result = false;
            for (int n = 0; n < YW.GetLength(1); ++n) {
                foreach (byte a in alleles) {
                    result = result || YW[gene, n] == a;
                }
            }
            return result;
        }

        // Checks if every allele matches at least one of the given alleles, for a gene on the Y or W chromosome
        [MemberNotNull(nameof(YW))]
        public bool HasOnlyHeterogametic(int gene, params byte[] alleles) {
            ArgumentNullException.ThrowIfNull(YW);
            bool result = false;
            for (int n = 0; n < YW.GetLength(1); ++n) {
                bool matches = false;
                foreach (byte a in alleles) {
                    matches = matches || YW[gene, n] == a;
                }
                result = result && matches;
            }
            return result;
        }

        public bool IsHomogametic() {
            return XZ.GetLength(1) == Ploidy;
        }

        public bool IsHeterogametic() {
            return YW is not null;
        }

        public int BitwiseSum(Range range) {
            int total = 0;
            for (int g = Ploidy * range.Start.Value; g < Ploidy * range.End.Value; ++g) {
                int b = g % 8;
                total += (Bitwise[g/8] >> b) & 1;
            }
            return total;
        }

        public int BitwiseDominant(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                int v = 0;
                for (int i = 0; i < Ploidy; ++i) {
                    int index = (Ploidy * g + i) / 8;
                    int offset = (Ploidy * g + i) % 8;
                    v |= (Bitwise[index] >> offset) & 1;
                }
                total += v;
            }
            return total;
        }

        public int BitwiseRecessive(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                int v = 1;
                for (int i = 0; i < Ploidy; ++i) {
                    int index = (Ploidy * g + i) / 8;
                    int offset = (Ploidy * g + i) % 8;
                    v &= (Bitwise[index] >> offset) & 1;
                }
                total += v;
            }
            return total;
        }

        public bool HasAllele(string gene, params string[] alleles) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAllele(geneID, alleles.Select(allele => Type.Autosomal.AlleleID(geneID, allele)).ToArray());
        }

        public bool HasOnlyAlleles(string gene, params string[] alleles) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasOnlyAlleles(geneID, alleles.Select(allele => Type.Autosomal.AlleleID(geneID, allele)).ToArray());
        }

        public bool HasSexlinked(string gene, params string[] alleles) {
            int geneID = Type.XZ.GeneID(gene);
            return HasSexlinked(geneID, alleles.Select(allele => Type.XZ.AlleleID(geneID, allele)).ToArray());
        }

        public bool HasOnlySexlinked(string gene, params string[] alleles) {
            int geneID = Type.XZ.GeneID(gene);
            return HasOnlySexlinked(geneID, alleles.Select(allele => Type.XZ.AlleleID(geneID, allele)).ToArray());
        }

        [MemberNotNull(nameof(YW))]
        public bool HasHeterogametic(string gene, params string[] alleles) {
            int geneID = Type.YW.GeneID(gene);
            return HasHeterogametic(geneID, alleles.Select(allele => Type.YW.AlleleID(geneID, allele)).ToArray());
        }

        [MemberNotNull(nameof(YW))]
        public bool HasOnlyHeterogametic(string gene, params string[] alleles) {
            int geneID = Type.YW.GeneID(gene);
            return HasOnlyHeterogametic(geneID, alleles.Select(allele => Type.YW.AlleleID(geneID, allele)).ToArray());
        }

        public void SetAutosomal(string gene, int n, string allele) {
            int geneID = Type.Autosomal.GeneID(gene);
            byte alleleID = Type.Autosomal.AlleleID(geneID, allele);
            Autosomal[geneID, n] = alleleID;
        }

        public void SetHomozygous(string gene, string allele) {
            int geneID = Type.Autosomal.GeneID(gene);
            byte alleleID = Type.Autosomal.AlleleID(geneID, allele);
            for (int n = 0; n < Ploidy; ++n) {
                Autosomal[geneID, n] = alleleID;
            }
        }

        public void SetNotHomozygousFor(string gene, string avoidAllele, AlleleFrequencies frequencies, string fallbackAllele) {
            int geneID = Type.Autosomal.GeneID(gene);
            byte avoidID = Type.Autosomal.AlleleID(geneID, avoidAllele);
            if (HasOnlyAlleles(geneID, avoidID)) {
                float[] f = frequencies.Autosomal[geneID];
                for (int i = 0; i < f.Length; ++i) {
                    if (i == avoidID) {
                        continue;
                    }
                    if (f[i] > 0) {
                        Autosomal[geneID, 0] = (byte) i;
                        break;
                    }
                }
                if (Autosomal[geneID, 0] == avoidID) {
                    Autosomal[geneID, 0] = Type.Autosomal.AlleleID(geneID, fallbackAllele);
                }
            }
        }

        public bool IsEmbryonicLethal() {
            foreach (GeneInterpreter interpreter in Type.Interpreters) {
                if (interpreter.IsEmbryonicLethal(this)) {
                    return true;
                }
            }
            return false;
        }

        protected Genome(GenomeType type, int ploidy) {
            Type = type;
            Ploidy = ploidy;

            Autosomal = new byte[Type.Autosomal.GeneCount, Ploidy];
            Anonymous = new byte[Type.Anonymous.GeneCount, Ploidy];

            Bitwise = new byte[(int)Math.Ceiling(Ploidy * Type.Bitwise.GeneCount / 8.0)];

            // Leaves the sex chromosomes uninitialized
            XZ = null!;
        }

        public Genome(AlleleFrequencies frequencies, bool heterogametic, Random random) : this(frequencies.ForType, 2) {
            for (int gene = 0; gene < Type.Autosomal.GeneCount; ++gene) {
                for (int n = 0; n < Ploidy; ++n) {
                    Autosomal[gene, n] = getRandomAllele(frequencies.Autosomal[gene], random);
                }
            }

            byte[] buffer = new byte[Anonymous.GetLength(0) * Anonymous.GetLength(1)];
            random.NextBytes(buffer);
            Buffer.BlockCopy(buffer, 0, Anonymous, 0, buffer.Length);

            int i = 0;
            for (int group = 0; group < Type.Bitwise.GeneGroupCount; ++group) {
                for (int gene = 0; gene < Type.Bitwise.GroupSizes[group]; ++gene) {
                    float chance = frequencies.Bitwise[group][Math.Min(gene, frequencies.Bitwise[group].Length)];
                    for (int n = 0; n < Ploidy; ++n) {
                        if (random.NextSingle() < chance) {
                            Bitwise[i/8] |= (byte) (1 << (i % 8));
                        }
                        i += 1;
                    }
                }
            }

            if (heterogametic) {
                XZ = new byte[Ploidy/2, Type.XZ.GeneCount];
                YW = new byte[Ploidy/2, Type.YW.GeneCount];

                for (int gene = 0; gene < frequencies.YW.Length; ++gene) {
                    for (int n = 0; n < YW.GetLength(1); ++n) {
                        YW[n, gene] = getRandomAllele(frequencies.YW[gene], random);
                    }
                }
            }
            else {
                XZ = new byte[Ploidy, Type.XZ.GeneCount];
            }

            for (int gene = 0; gene < frequencies.XZ.Length; ++gene) {
                for (int n = 0; n < XZ.GetLength(1); ++n) {
                    XZ[gene, n] = getRandomAllele(frequencies.XZ[gene], random);
                }
            }
        }

        protected byte getRandomAllele(float[] alleles, Random random) {
            if (alleles == null) {
                return 0;
            }
            float f = random.NextSingle();
            byte a = 0;
            for ( ; a < alleles.Length && alleles[a] < f; ++a);
            return a;
        }

        public virtual Genome CreateGamete(bool heterogametic, Random random) {
            if (Ploidy % 2 != 0) {
                throw new InvalidOperationException("Not supported for odd ploidy (n=" + Ploidy + "). Genome type: " + Type.Name);
            }
            Genome gamete = new Genome(Type, Ploidy / 2);

            SplitGenes(gamete.Autosomal, Autosomal, random);
            SplitGenes(gamete.Anonymous, Anonymous, random);

            for (int p = 0; p < gamete.Ploidy; ++p) {
                for (int gene = 0; gene < Type.Bitwise.GeneCount; ++gene) {
                    int n = random.Next(2);
                    int gIndex = gamete.Ploidy * gene + p;
                    int pIndex = Ploidy * gene + 2 * p + n;
                    gamete.Bitwise[gIndex / 8] |= (byte)(((Bitwise[pIndex / 8] >> (pIndex % 8)) & 1) << (gIndex % 8));
                }
            }

            if (this.IsHeterogametic()) {
                if (heterogametic) {
                    gamete.XZ = new byte[Type.XZ.GeneCount, 0];
                    gamete.YW = (byte[,])this.YW!.Clone();
                }
                else {
                    gamete.XZ = (byte[,])this.XZ.Clone();
                }
            }
            else {
                gamete.XZ = new byte[Type.XZ.GeneCount, gamete.Ploidy];
                SplitGenes(gamete.XZ, XZ, random);
            }

            return gamete;
        }

        protected static void SplitGenes(byte[,] gamete, byte[,] parent, Random random) {
            for (int p = 0; p < gamete.GetLength(1); ++p) {
                int n = random.Next(2);
                for (int gene = 0; gene < parent.GetLength(0); ++gene) {
                    gamete[gene, p] = parent[gene, 2 * p + n];
                    // TODO: Genetic linkage
                    n = random.Next(2);
                }
            }
        }

        public virtual Genome Join(Genome other) {
            Genome zygote = new Genome(Type, this.Ploidy + other.Ploidy);
            JoinGenes(zygote.Autosomal, this.Autosomal, other.Autosomal);
            JoinGenes(zygote.Anonymous, this.Anonymous, other.Anonymous);

            for (int gene = 0; gene < Type.Bitwise.GeneCount; ++gene) {
                for (int p = 0; p < this.Bitwise.GetLength(1); ++p) {
                    int zIndex = gene * zygote.Bitwise.GetLength(1) + p;
                    int gIndex = gene * this.Bitwise.GetLength(1) + p;
                    zygote.Bitwise[zIndex/8] |= (byte)(((this.Bitwise[gIndex/8] >> (gIndex % 8)) & 1) << (zIndex % 8));
                }
                for (int p = 0; p < other.Bitwise.GetLength(1); ++p) {
                    int zIndex = gene * zygote.Bitwise.GetLength(1) + p;
                    int gIndex = gene * other.Bitwise.GetLength(1) + this.Bitwise.GetLength(1) + p;
                    zygote.Bitwise[zIndex/8] |= (byte)(((other.Bitwise[gIndex/8] >> (gIndex % 8)) & 1) << (zIndex % 8));
                }
            }

            JoinGenes(zygote.XZ, this.XZ, other.XZ);
            zygote.YW = this.YW ?? other.YW;
            return zygote;
        }

        protected static void JoinGenes(byte[,] joined, byte[,] first, byte[,] second) {
            for (int gene = 0; gene < first.GetLength(0); ++gene) {
                for (int p = 0; p < first.GetLength(1); ++p) {
                    joined[gene, p] = first[gene, p];
                }
                for (int p = 0; p < second.GetLength(1); ++p) {
                    joined[gene, first.GetLength(1) + p] = second[gene, p];
                }
            }
        }

        public static Genome Inherit(Genome mother, Genome father, bool isHeterogametic, Random random) {
            return mother.CreateGamete(isHeterogametic, random).Join(father.CreateGamete(isHeterogametic, random));
        }

        public virtual Genome Mutate(double p, Random random) {
            for (int gene = 0; gene < Type.Autosomal.GeneCount; ++gene) {
                for (int n = 0; n < Ploidy; ++n) {
                    if (random.NextDouble() < p) {
                        Autosomal[gene, n] = (byte) random.Next(Type.Autosomal.AlleleCount(gene));
                    }
                }
            }
            if (Type.Autosomal.TryGetGeneID("KIT", out int KIT)) {
                for (int n = 0; n < Ploidy; ++n) {
                    if (random.NextDouble() < 10 * p) {
                        Autosomal[KIT, n] = (byte) random.Next(Type.Autosomal.AlleleCount(KIT));
                    }
                }
            }
            for (int gene = 0; gene < Ploidy * Type.Anonymous.GeneCount; ++gene) {
                for (int n = 0; n < Ploidy; ++n) {
                    if (random.NextDouble() < p) {
                        Anonymous[gene, n] = (byte)random.Next(256);
                    }
                }
            }
            for (int gene = 0; gene < Ploidy * Type.Bitwise.GeneCount; ++gene) {
                for (int b = 0; b < 8; ++b) {
                    if (random.NextDouble() < p) {
                        Bitwise[gene] ^= (byte)(1 << b);
                    }
                }
            }
            for (int gene = 0; gene < Type.XZ.GeneCount; ++gene) {
                for (int n = 0; n < XZ.GetLength(1); ++n) {
                    if (random.NextDouble() < p) {
                        XZ[gene, 0] = (byte) random.Next(Type.XZ.AlleleCount(gene));
                    }
                }
            }
            if (YW != null) {
                for (int gene = 0; gene < Type.YW.GeneCount; ++gene) {
                    for (int n = 0; n < YW.GetLength(1); ++n) {
                        if (random.NextDouble() < p) {
                            YW[gene, n] = (byte) random.Next(Type.YW.AlleleCount(gene));
                        }
                    }
                }
            }
            return this;
        }

        private byte[] atLeastSize(byte[] given, int size) {
            if (given != null && given.Length >= size) {
                return given;
            }
            byte[] array = new byte[size];
            if (given == null) {
                return array;
            }
            Array.Copy(given, array, given.Length);
            return array;
        }

        protected virtual void UpdateForType() {
            this.Autosomal = atLeastSize(Autosomal, Ploidy * Type.Autosomal.GeneCount);
            this.Anonymous = atLeastSize(Anonymous, Ploidy * Type.Anonymous.GeneCount);
            this.Bitwise = atLeastSize(Bitwise, (int)Math.Ceiling(Ploidy * Type.Bitwise.GeneCount / 8.0));
            this.XZ = atLeastSize(XZ, (YW == null ? (int)Math.Ceiling(Ploidy/2.0) : Ploidy) * type.XZ.GeneCount);
            this.YW = atLeastSize(YW, (int)Math.Ceiling(Ploidy/2.0) * Type.YW.GeneCount);
        }

        public Genome(GenomeType type, TreeAttribute geneticsTree) {
            this.Type = type;

            byte[] autosomal = (geneticsTree.GetAttribute("autosomal") as ByteArrayAttribute)?.value;
            byte[] anonymous = (geneticsTree.GetAttribute("anonymous") as ByteArrayAttribute)?.value;
            byte[] bitwise = (geneticsTree.GetAttribute("bitwise") as ByteArrayAttribute)?.value;
            byte[] primary_xz = (geneticsTree.GetAttribute("primary_xz") as ByteArrayAttribute)?.value;
            byte[] secondary_xz = (geneticsTree.GetAttribute("secondary_xz") as ByteArrayAttribute)?.value;
            byte[] yw = (geneticsTree.GetAttribute("yw") as ByteArrayAttribute)?.value;
            // TODO: Read these properly
            UpdateForType();
        }

        // Caller is responsible for marking the path as dirty if necessary
        public void AddToTree(TreeAttribute geneticsTree) {
            if (Autosomal.Length == 0) {
                geneticsTree.RemoveAttribute("autosomal");
            }
            else {
                geneticsTree.SetAttribute("autosomal", new ByteArrayAttribute(Autosomal));
            }
            if (Anonymous.Length == 0) {
                geneticsTree.RemoveAttribute("anonymous");
            }
            else {
                geneticsTree.SetAttribute("anonymous", new ByteArrayAttribute(Anonymous));
            }
            if (Bitwise.Length == 0) {
                geneticsTree.RemoveAttribute("bitwise");
            }
            else {
                geneticsTree.SetAttribute("bitwise", new ByteArrayAttribute(Bitwise));
            }
            if (XZ.Length == 0) {
                geneticsTree.RemoveAttribute("xz");
            }
            else {
                geneticsTree.SetAttribute("xz", new ByteArrayAttribute(XZ));
            }
            if (YW == null) {
                geneticsTree.RemoveAttribute("yw");
            }
            else {
                geneticsTree.SetAttribute("yw", new ByteArrayAttribute(YW));
            }
        }

        public override string ToString() {
            return "Genome << type:" + Type.Name 
                + ",\n    autosomal=" + Autosomal.ArrayToString() 
                + ",\n    xz=" + XZ.ArrayToString() 
                + ",\n    yw=" + YW?.ArrayToString() 
                + ",\n    anonymous=" + Anonymous.ArrayToString()
                + ",\n    bitwise=" + Bitwise.ArrayToString() + " >>";
        }
    }
}
