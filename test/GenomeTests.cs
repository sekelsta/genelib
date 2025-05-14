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

            string mammal = "{ genes: { autosomal: [ { extension: [\"wildtype\", \"black\", \"red\"] }, { tyrosinase: [\"wildtype\", \"white\"] }, ], xz: [ { xlinked1: [\"a\", \"b\"] }, { xlinked2: [\"a\", \"b\"] }, ], yw: [ { ylinked1: [\"a\", \"b\"] }, { ylinked2: [\"a\", \"b\"] }, ], }, interpreters: [ \"Polygenes\" ], sexdetermination: \"xy\", initializers: { defaultinitializer: {}, secondsies: {autosomal: { extension: { default: \"black\" }, tyrosinase: { default: \"white\" }, }, xz: { xlinked1: { default: \"b\" }, xlinked2: { default: \"b\" }, }, yw: { ylinked1: { default: \"b\" }, ylinked2: { default: \"b\" }, },} }}";
            GenomeType.Load(new Asset(Encoding.ASCII.GetBytes(mammal), "mammal", null));

            string bird = "{ genes: { autosomal: [ { extension: [\"wildtype\", \"black\", \"red\"] }, { tyrosinase: [\"wildtype\", \"white\"] }, ], xz: [ { xlinked1: [\"a\", \"b\"] }, { xlinked2: [\"a\", \"b\"] }, ], yw: [ { ylinked1: [\"a\", \"b\"] }, { ylinked2: [\"a\", \"b\"] }, ], }, interpreters: [ \"Polygenes\" ], sexdetermination: \"zw\", initializers: { defaultinitializer: {} }}";
            GenomeType.Load(new Asset(Encoding.ASCII.GetBytes(bird), "bird", null));            
        }

        [Test]
        public void Genome_GenerateFemaleMammal()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Genome female = new Genome(mammal.Initializer("defaultinitializer").Frequencies, false, new Random(SEED));

            Assert.AreEqual(false, female.Heterogametic());
            Assert.NotNull(female.primary_xz, "Female mammal should have primary X chromosome");
            Assert.NotNull(female.secondary_xz, "Female mammal should have secondary X chromosome");
            Assert.Null(female.yw, "Female mammal should not have Y chromosome");
        }

        [Test]
        public void Genome_GenerateMaleMammal()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Genome male = new Genome(mammal.Initializer("defaultinitializer").Frequencies, true, new Random(SEED));

            Assert.AreEqual(true, male.Heterogametic());
            Assert.NotNull(male.primary_xz, "Male mammal should have X chromosome");
            Assert.Null(male.secondary_xz, "Male mammal should not have secondary X chromosome");
            Assert.NotNull(male.yw, "Male mammal should have Y chromosome");
        }

        [Test]
        public void Genome_GenerateFemaleBird()
        {
            GenomeType bird = GenomeType.Get("bird");
            Genome female = new Genome(bird.Initializer("defaultinitializer").Frequencies, true, new Random(SEED));

            Assert.AreEqual(true, female.Heterogametic());
            Assert.NotNull(female.primary_xz, "Female bird should have Z chromosome");
            Assert.Null(female.secondary_xz, "Female bird should not have secondary Z chromosome");
            Assert.NotNull(female.yw, "Female bird should have W chromosome");
        }

        [Test]
        public void Genome_GenerateMaleBird()
        {
            GenomeType bird = GenomeType.Get("bird");
            Genome male = new Genome(bird.Initializer("defaultinitializer").Frequencies, false, new Random(SEED));

            Assert.AreEqual(false, male.Heterogametic());
            Assert.NotNull(male.primary_xz, "Male bird should have primary Z chromosome");
            Assert.NotNull(male.secondary_xz, "Male bird should have secondary Z chromosome");
            Assert.Null(male.yw, "Male bird should not have W chromosome");
        }

        [Test]
        public void Genome_Inherit()
        {
            GenomeType mammal = GenomeType.Get("mammal");
            Random random = new Random(SEED);
            Genome mother = new Genome(mammal.Initializer("defaultinitializer").Frequencies, false, random);
            Genome father = new Genome(mammal.Initializer("secondsies").Frequencies, true, random);

            Genome daughter = new Genome(mother, father, false, random);
            Genome son = new Genome(mother, father, true, random);

            Assert.AreEqual(0, daughter.Autosomal(0, 0));
            Assert.AreEqual(1, daughter.Autosomal(0, 1));
            Assert.AreEqual(0, daughter.Autosomal(1, 0));
            Assert.AreEqual(1, daughter.Autosomal(1, 1));

            Assert.AreEqual(0, daughter.XZ(0, 0));
            Assert.AreEqual(1, daughter.XZ(0, 1));
            Assert.AreEqual(0, daughter.XZ(1, 0));
            Assert.AreEqual(1, daughter.XZ(1, 1));

            Assert.AreEqual(0, son.Autosomal(0, 0));
            Assert.AreEqual(1, son.Autosomal(0, 1));
            Assert.AreEqual(0, son.Autosomal(1, 0));
            Assert.AreEqual(1, son.Autosomal(1, 1));

            Assert.AreEqual(0, son.XZ(0, 0));
            Assert.AreEqual(0, son.XZ(1, 0));
            Assert.AreEqual(1, son.YW(0));
            Assert.AreEqual(1, son.YW(1));

            Assert.True(son.Anonymous(0, 0) == mother.Anonymous(0, 0) || son.Anonymous(0, 0) == mother.Anonymous(0, 1));
        }
    }
}
