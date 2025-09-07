using Genelib.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace Genelib {
    public class Genome {
        public GenomeType Type  { get; protected set; }
        public readonly int Ploidy = 2;
        // Dimensions are [genes, ploidy]
        public byte[,] Autosomal { get; protected set; }
        public byte[,] Anonymous { get; protected set; }
        public byte[,] XZ { get; protected set; }
        public byte[,]? YW { get; protected set; }
        // A compact way to store numerous genes each with only 2 possible alleles
        // Format: Basic layout similar to autosomal genes (gene copies grouped), but compressed down to where each allele is only 1 bit.
        // Across the bytes in the array the order is as you'd expect, starting at index 0 and increasing.
        // Within each byte, the order is right to left - 0th is 0 bitshifts from the right, 1 is one bitshift from the right, etc.
        // So for a typical n=2 creature, the first byte has 2 bits for the two alleles of the first gene, same for the second, third, and fourth, and then the fifth is on the second byte.
        public byte[] Bitwise { get; protected set; }

        public int GetBitwiseAllele(int gene, int n) {
            int i = gene * Ploidy + n;
            int b = i % 8;
            return (Bitwise[i/8] >> b) & 1;
        }

        public void SetBitwiseAllele(int gene, int n, int val) {
            int i = Ploidy * gene + n;
            int b = i % 8;
            byte v = Bitwise[i/8];
            v = (byte)(v & ~(1 << b)); // Set to 0
            v = (byte)(v | (val << b)); // Now set from 0 to val
            Bitwise[i/8] = v;
        }

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

        // Returns true if all copies of the gene match the passed allele
        public bool IsHomozygous(int gene, byte allele) {
            return HasOnlyAlleles(gene, allele);
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
            bool result = true;
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
            bool result = true;
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

        [MemberNotNullWhen(true, nameof(YW))]
        public bool IsHeterogametic() {
            return YW is not null;
        }

        public int BitwiseSum(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                for (int n = 0; n < Ploidy; ++n) {
                    total += GetBitwiseAllele(g, n);
                }
            }
            return total;
        }

        // Returns the number of genes in the given range where ANY allele is 1
        public int BitwiseDominant(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                int v = 0;
                for (int n = 0; n < Ploidy; ++n) {
                    v |= GetBitwiseAllele(g, n);
                }
                total += v;
            }
            return total;
        }

        // Returns the number of genes in the given range where EVERY allele is 1
        public int BitwiseRecessive(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                int v = 1;
                for (int n = 0; n < Ploidy; ++n) {
                    v &= GetBitwiseAllele(g, n);
                }
                total += v;
            }
            return total;
        }

        public int BitwiseHomozygotes(Range range) {
            int total = 0;
            for (int g = range.Start.Value; g < range.End.Value; ++g) {
                bool match = true;
                int a = GetBitwiseAllele(g, 0);
                for (int n = 1; n < Ploidy; ++n) {
                    match = match && GetBitwiseAllele(g, n) == a;
                }
                if (match) total += 1;
            }
            return total;
        }

        public int BitwiseSum(string geneGroup) {
            return BitwiseSum(Type.Bitwise.TryGetRange(geneGroup));
        }

        public int BitwiseDominant(string geneGroup) {
            return BitwiseDominant(Type.Bitwise.TryGetRange(geneGroup));
        }

        public int BitwiseRecessive(string geneGroup) {
            return BitwiseRecessive(Type.Bitwise.TryGetRange(geneGroup));
        }

        public int BitwiseHomozygotes(string geneGroup) {
            return BitwiseHomozygotes(Type.Bitwise.TryGetRange(geneGroup));
        }

        public bool HasAllele(string gene, params string[] alleles) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasAllele(geneID, alleles.Select(allele => Type.Autosomal.AlleleID(geneID, allele)).ToArray());
        }

        public bool IsHomozygous(string gene, string allele) {
            int geneID = Type.Autosomal.GeneID(gene);
            return HasOnlyAlleles(geneID, Type.Autosomal.AlleleID(geneID, allele));
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
                float[]? f = frequencies.Autosomal?[geneID];
                if (f == null) {
                    if (avoidID != 0) Autosomal[geneID, 0] = (byte) 0;
                    else Autosomal[geneID, 0] = (byte) 1;
                    return;
                }
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
                    Autosomal[gene, n] = getRandomAllele(frequencies.Autosomal?[gene], random);
                }
            }

            byte[] buffer = new byte[Anonymous.GetLength(0) * Anonymous.GetLength(1)];
            random.NextBytes(buffer);
            Buffer.BlockCopy(buffer, 0, Anonymous, 0, buffer.Length);

            int i = 0;
            for (int group = 0; group < Type.Bitwise.GeneGroupCount; ++group) {
                float[]? chances = frequencies.Bitwise?[group];
                for (int gene = 0; gene < Type.Bitwise.GroupSizes[group]; ++gene) {
                    float chance = chances?[Math.Min(gene, chances.Length - 1)] ?? 0.5f;
                    for (int n = 0; n < Ploidy; ++n) {
                        if (random.NextSingle() < chance) {
                            Bitwise[i/8] |= (byte) (1 << (i % 8));
                        }
                        i += 1;
                    }
                }
            }

            if (heterogametic) {
                XZ = new byte[Type.XZ.GeneCount, Ploidy/2];
                YW = new byte[Type.YW.GeneCount, Ploidy/2];

                for (int gene = 0; gene < Type.YW.GeneCount; ++gene) {
                    for (int n = 0; n < YW.GetLength(1); ++n) {
                        YW[gene, n] = getRandomAllele(frequencies.YW?[gene], random);
                    }
                }
            }
            else {
                XZ = new byte[Type.XZ.GeneCount, Ploidy];
            }

            for (int gene = 0; gene < Type.XZ.GeneCount; ++gene) {
                for (int n = 0; n < XZ.GetLength(1); ++n) {
                    XZ[gene, n] = getRandomAllele(frequencies.XZ?[gene], random);
                }
            }
        }

        protected byte getRandomAllele(float[]? alleles, Random random) {
            if (alleles == null) {
                return 0;
            }
            float f = random.NextSingle();
            byte a = 0;
            for ( ; a < alleles.Length && alleles[a] < f; ++a);
            return a;
        }

        // In the unlikely circumstance that anybody wants to make a mod supporting polyploidy,
        // AND wants chromosome pairing to be properly random, you'll want to override or harmony patch this function
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
                    gamete.SetBitwiseAllele(gene, p, GetBitwiseAllele(gene, 2 * p + n));
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
                for (int p = 0; p < this.Ploidy; ++p) {
                    zygote.SetBitwiseAllele(gene, p, GetBitwiseAllele(gene, p));
                }
                for (int p = 0; p < other.Ploidy; ++p) {
                    zygote.SetBitwiseAllele(gene, p + this.Ploidy, other.GetBitwiseAllele(gene, p));
                }
            }

            zygote.XZ = new byte[Type.XZ.GeneCount, this.XZ.GetLength(1) + other.XZ.GetLength(1)];
            JoinGenes(zygote.XZ, this.XZ, other.XZ);
            zygote.YW = this.YW ?? other.YW;
            return zygote;
        }

        protected static void JoinGenes(byte[,] joined, byte[,] first, byte[,] second) {
            ArgumentNullException.ThrowIfNull(joined);
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);
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
            for (int gene = 0; gene < Type.Anonymous.GeneCount; ++gene) {
                for (int n = 0; n < Ploidy; ++n) {
                    if (random.NextDouble() < p) {
                        Anonymous[gene, n] = (byte)random.Next(256);
                    }
                }
            }
            for (int i = 0; i < Bitwise.Length; ++i) {
                for (int b = 0; b < 8; ++b) {
                    if (random.NextDouble() < p) {
                        Bitwise[i] ^= (byte)(1 << b);
                    }
                }
            }
            for (int gene = 0; gene < Type.XZ.GeneCount; ++gene) {
                for (int n = 0; n < XZ.GetLength(1); ++n) {
                    if (random.NextDouble() < p) {
                        XZ[gene, n] = (byte) random.Next(Type.XZ.AlleleCount(gene));
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

        private byte[] atLeastSize(byte[]? given, int count) {
            if (given != null && given.Length >= count) {
                return given;
            }
            byte[] array = new byte[count];
            if (given == null) {
                return array;
            }
            Array.Copy(given, array, given.Length);
            return array;
        }

        private byte[,] atLeastSize(byte[,]? given, int count) {
            if (given != null && given.GetLength(0) >= count) {
                return given;
            }
            byte[,] array = new byte[count, given?.GetLength(1) ?? Ploidy];
            if (given == null) {
                return array;
            }
            Array.Copy(given, array, given.Length);
            return array;
        }

        protected virtual void UpdateForType() {
            bool moveCoiGenes = Autosomal.Length == 2 * 48 && Bitwise == null && Ploidy == 2; // TODO: Remove this after people have had a chance to update, but before it starts causing problems
            this.Autosomal = atLeastSize(Autosomal, Type.Autosomal.GeneCount);
            this.Anonymous = atLeastSize(Anonymous, Type.Anonymous.GeneCount);
            this.Bitwise = atLeastSize(Bitwise, (int)Math.Ceiling(Ploidy * Type.Bitwise.GeneCount / 8.0));
            this.XZ = atLeastSize(XZ, Type.XZ.GeneCount);
            this.YW = YW != null ? atLeastSize(YW, Type.YW.GeneCount) : null; // Keep null if it started null
            if (moveCoiGenes) {
                byte[,] oldAnonymous = Anonymous;
                Anonymous = new Byte[Type.Anonymous.GeneCount, 2];
                // Bitwise will have already been set to the new size
                // Old anonymous gene format: 32 diversity genes (COI) followed by 16 vitality genes (deleterious)
                Range deleterious = Type.Anonymous.TryGetRange("deleterious");
                for (int i = deleterious.Start.Value; i < deleterious.End.Value; ++i) {
                    Anonymous[i,0] = oldAnonymous[i - deleterious.Start.Value + 32, 0];
                    Anonymous[i,1] = oldAnonymous[i - deleterious.Start.Value + 32, 1];
                }
                Range coi = Type.Bitwise.TryGetRange("coi");
                for (int i = coi.Start.Value; i < coi.End.Value; ++i) {
                    SetBitwiseAllele(i, 0, oldAnonymous[i - coi.Start.Value, 0]);
                    SetBitwiseAllele(i, 1, oldAnonymous[i - coi.Start.Value, 1]);
                }
            }
        }

        public Genome(GenomeType type, TreeAttribute geneticsTree) {
            this.Type = type;

            // Note these values could temporarily be null, until UpdateForType is called
            Autosomal = (geneticsTree.GetAttribute("autosomal") as ByteArray2DAttribute)?.value!;
            Anonymous = (geneticsTree.GetAttribute("anonymous") as ByteArray2DAttribute)?.value!;
            Bitwise = (geneticsTree.GetAttribute("bitwise") as ByteArrayAttribute)?.value!;
            XZ = (geneticsTree.GetAttribute("xz") as ByteArray2DAttribute)?.value!;

            Ploidy = (Autosomal ?? Anonymous ?? XZ)?.GetLength(1) ?? 2;

            // Update from previous save format where primary and secondary XZ chromosomes were separate
            byte[]? primary_xz = (geneticsTree.GetAttribute("primary_xz") as ByteArrayAttribute)?.value;
            byte[]? secondary_xz = (geneticsTree.GetAttribute("secondary_xz") as ByteArrayAttribute)?.value;
            if (XZ == null && primary_xz != null) {
                if (secondary_xz == null) {
                    XZ = new byte[primary_xz.Length, 1];
                    Buffer.BlockCopy(primary_xz, 0, XZ, 0, primary_xz.Length);
                }
                else {
                    XZ = new byte[primary_xz.Length, 2];
                    for (int i = 0; i < primary_xz.Length; ++i) {
                        XZ[i, 0] = primary_xz[i];
                        XZ[i, 1] = secondary_xz[i];
                    }
                }
            }

            YW = (geneticsTree.GetAttribute("yw") as ByteArray2DAttribute)?.value;
            XZ ??= new byte[0, YW == null ? Ploidy : Ploidy/2]; // Need to set correct ploidy for it here, then length gets handled by UpdateForType
            UpdateForType();
            // Just to make the compiler happy about nullability. Should never happen unless someone overrides UpdateForType to do something dumb
            ArgumentNullException.ThrowIfNull(Autosomal);
            ArgumentNullException.ThrowIfNull(Anonymous);
        }

        // Caller is responsible for marking the path as dirty if necessary
        public void AddToTree(TreeAttribute geneticsTree) {
            if (Autosomal.Length == 0) {
                geneticsTree.RemoveAttribute("autosomal");
            }
            else {
                geneticsTree.SetAttribute("autosomal", new ByteArray2DAttribute(Autosomal));
            }
            if (Anonymous.Length == 0) {
                geneticsTree.RemoveAttribute("anonymous");
            }
            else {
                geneticsTree.SetAttribute("anonymous", new ByteArray2DAttribute(Anonymous));
            }
            if (Bitwise.Length == 0) {
                geneticsTree.RemoveAttribute("bitwise");
            }
            else {
                geneticsTree.SetAttribute("bitwise", new ByteArrayAttribute(Bitwise));
            }
            // Set XZ regardless of whetehr the length is 0 or not
            geneticsTree.SetAttribute("xz", new ByteArray2DAttribute(XZ));
            if (YW == null) {
                geneticsTree.RemoveAttribute("yw");
            }
            else {
                geneticsTree.SetAttribute("yw", new ByteArray2DAttribute(YW));
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
