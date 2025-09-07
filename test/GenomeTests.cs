using Genelib;
using NUnit.Framework;
using System;
using System.Text;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace Genelib.Test {
    [TestFixture]
    public class InheritanceTests
    {
        private const int SEED = 12345;

        [SetUp]
        public void Setup()
        {
            GenomeType.RegisterInterpreter(new PolygeneInterpreter());

            string mammal = "{ genes: { autosomal: [ { extension: [\"wildtype\", \"black\", \"red\"] }, { tyrosinase: [\"wildtype\", \"white\"] }, ], xz: [ { xlinked1: [\"a\", \"b\"] }, { xlinked2: [\"a\", \"b\"] }, ], yw: [ { ylinked1: [\"a\", \"b\"] }, { ylinked2: [\"a\", \"b\"] }, ], anonymous: [ { deleterious: 16 } ], bitwise: [ { coi: 128 } ], }, interpreters: [ \"Polygenes\" ], sexdetermination: \"xy\", initializers: { defaultinitializer: {}, secondsies: {autosomal: { extension: { default: \"black\" }, tyrosinase: { default: \"white\" }, }, xz: { xlinked1: { default: \"b\" }, xlinked2: { default: \"b\" }, }, yw: { ylinked1: { default: \"b\" }, ylinked2: { default: \"b\" }, },} }}";
            GenomeType.Load(new Asset(Encoding.ASCII.GetBytes(mammal), "mammal", null));

            string bird = "{ genes: { autosomal: [ { extension: [\"wildtype\", \"black\", \"red\"] }, { tyrosinase: [\"wildtype\", \"white\"] }, ], xz: [ { xlinked1: [\"a\", \"b\"] }, { xlinked2: [\"a\", \"b\"] }, ], yw: [ { ylinked1: [\"a\", \"b\"] }, { ylinked2: [\"a\", \"b\"] }, ], }, interpreters: [ \"Polygenes\" ], sexdetermination: \"zw\", initializers: { defaultinitializer: {} }}";
            GenomeType.Load(new Asset(Encoding.ASCII.GetBytes(bird), "bird", null));  

            // Note the probabilities in bitwise initializers are listed per gene in the group, with the final one being used for all remaining genes
            string bitwise = "{ genes: { anonymous: [ { deleterious: 16 } ], bitwise: [ { coi: 128 }, { strength: 8 }, { stamina: 4 }, { energy: 64 } ], }, interpreters: [ \"Polygenes\" ], sexdetermination: \"xy\", initializers: { defaultinitializer: {}, allzeros: { bitwise: { coi: [0], strength: [0], stamina: [0], energy: [0] } }, allones: { bitwise: { coi: [1], strength: [1], stamina: [1], energy: [1] } }, mixed: { bitwise: { strength: [0.0, 1.0, 0.2, 0.3, 0.4, 1.0], stamina: [0, 0.2, 0.8, 1], energy: [0, 0.8] } } }}";   
            GenomeType.Load(new Asset(Encoding.ASCII.GetBytes(bitwise), "bitwise", null));         
        }

        [Test]
        public void Genome_GenerateFemaleMammal()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Genome female = new Genome(mammal.Initializer("defaultinitializer").Frequencies, false, new Random(SEED));

            Assert.AreEqual(false, female.IsHeterogametic());
            Assert.NotNull(female.XZ, "Female mammal should have X chromosomes");
            Assert.AreEqual(2, female.XZ.GetLength(1), "Female mammal should have secondary X chromosome");
            Assert.Null(female.YW, "Female mammal should not have Y chromosome");

            Assert.AreEqual(0, female.Type.Bitwise.TryGetRange("thisgenedoesnotexist").Start.Value);
            Assert.AreEqual(0, female.Type.Bitwise.TryGetRange("thisgenedoesnotexist").End.Value);
        }

        [Test]
        public void Genome_GenerateMaleMammal()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Genome male = new Genome(mammal.Initializer("defaultinitializer").Frequencies, true, new Random(SEED));

            Assert.AreEqual(true, male.IsHeterogametic());
            Assert.NotNull(male.XZ, "Male mammal should have X chromosome");
            Assert.AreEqual(1, male.XZ.GetLength(1), "Male mammal should not have secondary X chromosome");
            Assert.NotNull(male.YW, "Male mammal should have Y chromosome");
        }

        [Test]
        public void Genome_GenerateFemaleBird()
        {
            GenomeType bird = GenomeType.Get("bird");
            Genome female = new Genome(bird.Initializer("defaultinitializer").Frequencies, true, new Random(SEED));

            Assert.AreEqual(true, female.IsHeterogametic());
            Assert.NotNull(female.XZ, "Female bird should have Z chromosome");
            Assert.AreEqual(1, female.XZ.GetLength(1), "Female bird should not have secondary Z chromosome");
            Assert.NotNull(female.YW, "Female bird should have W chromosome");
        }

        [Test]
        public void Genome_GenerateMaleBird()
        {
            GenomeType bird = GenomeType.Get("bird");
            Genome male = new Genome(bird.Initializer("defaultinitializer").Frequencies, false, new Random(SEED));

            Assert.AreEqual(false, male.IsHeterogametic());
            Assert.NotNull(male.XZ, "Male bird should have primary Z chromosome");
            Assert.AreEqual(2, male.XZ.GetLength(1), "Male bird should have secondary Z chromosome");
            Assert.Null(male.YW, "Male bird should not have W chromosome");
        }

        [Test]
        public void Genome_Inherit()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Random random = new Random(SEED);
            Genome mother = new Genome(mammal.Initializer("defaultinitializer").Frequencies, false, random);
            Genome father = new Genome(mammal.Initializer("secondsies").Frequencies, true, random);

            Genome daughter = Genome.Inherit(mother, father, false, random);
            Genome son = Genome.Inherit(mother, father, true, random);

            Assert.AreEqual(0, daughter.Autosomal[0, 0]);
            Assert.AreEqual(1, daughter.Autosomal[0, 1]);
            Assert.AreEqual(0, daughter.Autosomal[1, 0]);
            Assert.AreEqual(1, daughter.Autosomal[1, 1]);

            Assert.AreEqual(0, daughter.XZ[0, 0]);
            Assert.AreEqual(1, daughter.XZ[0, 1]);
            Assert.AreEqual(0, daughter.XZ[1, 0]);
            Assert.AreEqual(1, daughter.XZ[1, 1]);

            Assert.AreEqual(0, son.Autosomal[0, 0]);
            Assert.AreEqual(1, son.Autosomal[0, 1]);
            Assert.AreEqual(0, son.Autosomal[1, 0]);
            Assert.AreEqual(1, son.Autosomal[1, 1]);

            Assert.AreEqual(0, son.XZ[0, 0]);
            Assert.AreEqual(0, son.XZ[1, 0]);
            Assert.NotNull(son.YW);
            Assert.AreEqual(1, son.YW![0, 0]);
            Assert.AreEqual(1, son.YW![1, 0]);

            Assert.True(son.Anonymous[0, 0] == mother.Anonymous[0, 0] || son.Anonymous[0, 0] == mother.Anonymous[0, 1]);
        }

        [Test]
        public void Genome_Mutate()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Random random = new Random(SEED);
            Genome genome = new Genome(mammal.Initializer("defaultinitializer").Frequencies, false, random);
            genome.Mutate(0.05, random);
        }

        [Test]
        public void Genome_GenerateWithBitwiseInitializers()
        {
            GenomeType bitwise = GenomeType.Get("bitwise");
            Assert.AreEqual(4, bitwise.Bitwise.GroupNames.Length);
            Assert.AreEqual("coi", bitwise.Bitwise.GroupNames[0]);
            Assert.AreEqual("strength", bitwise.Bitwise.GroupNames[1]);

            // Just check that we can initialize with an empty initializer without exceptions
            Genome genome = new Genome(bitwise.Initializer("defaultinitializer").Frequencies, false, new Random(SEED));

            Genome zeros = new Genome(bitwise.Initializer("allzeros").Frequencies, false, new Random(SEED));
            // All zeros - no bits set
            Assert.AreEqual(0, zeros.BitwiseSum("coi"));
            Assert.AreEqual(0, zeros.BitwiseSum("strength"));
            Assert.AreEqual(0, zeros.BitwiseSum("stamina"));
            Assert.AreEqual(0, zeros.BitwiseSum("energy"));

            Assert.AreEqual(0, zeros.BitwiseDominant("coi"));
            Assert.AreEqual(0, zeros.BitwiseDominant("strength"));

            Assert.AreEqual(0, zeros.BitwiseRecessive("coi"));
            Assert.AreEqual(0, zeros.BitwiseRecessive("strength"));
            Assert.AreEqual(0, zeros.BitwiseRecessive("stamina"));
            Assert.AreEqual(0, zeros.BitwiseRecessive("energy"));

            Assert.AreEqual(128, zeros.BitwiseHomozygotes("coi"));
            Assert.AreEqual(8, zeros.BitwiseHomozygotes("strength"));


            Genome ones = new Genome(bitwise.Initializer("allones").Frequencies, false, new Random(SEED));
            // All ones - all bits set
            Assert.AreEqual(256, ones.BitwiseSum("coi")); // 128 * 2 (diploid)
            Assert.AreEqual(16, ones.BitwiseSum("strength")); // 8 * 2
            Assert.AreEqual(8, ones.BitwiseSum("stamina")); // 4 * 2
            Assert.AreEqual(128, ones.BitwiseSum("energy")); // 64 * 2

            Assert.AreEqual(128, ones.BitwiseDominant("coi"));
            Assert.AreEqual(8, ones.BitwiseDominant("strength"));
            Assert.AreEqual(4, ones.BitwiseDominant("stamina"));

            Assert.AreEqual(128, ones.BitwiseRecessive("coi"));
            Assert.AreEqual(8, ones.BitwiseRecessive("strength"));
            Assert.AreEqual(4, ones.BitwiseRecessive("stamina"));
            Assert.AreEqual(64, ones.BitwiseRecessive("energy"));

            Assert.AreEqual(128, ones.BitwiseHomozygotes("coi"));
            Assert.AreEqual(8, ones.BitwiseHomozygotes("strength"));
            Assert.AreEqual(4, ones.BitwiseHomozygotes("stamina"));


            Genome mixed = new Genome(bitwise.Initializer("mixed").Frequencies, false, new Random(SEED));

            int mixedSum = mixed.BitwiseSum("coi");
            int mixedDominant = mixed.BitwiseDominant("coi");
            int mixedRecessive = mixed.BitwiseRecessive("coi");
            int mixedHomozygotes = mixed.BitwiseHomozygotes("coi");

            // FUZZ TESTS - The following have a small chance of failing for certain seeds. Just make sure there's at least one seed they pass with.
            // Counting 1 bits as dominant should give a larger result than counting them as recessive
            Assert.Greater(mixedDominant, mixedRecessive);
            // The sum counts each dominant once and each recessive a second time
            Assert.AreEqual(mixedSum, mixedDominant + mixedRecessive);
            // Every recessive is a homozygote, but not every homozygote is a recessive
            Assert.Greater(mixedHomozygotes, mixedRecessive);
        }
    }
}
