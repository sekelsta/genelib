Modifies [Vintage Story](https://www.vintagestory.at/).

The basic idea:
* You write a json file describing what genes and alleles exist, and how likely an animal is to have them
* Genelib handles the backend parts: Initializing and storing gene data for entities, and children inheriting genes from parents
* Genelib provides a few simple gene-based effects
* You will need to write code for more complicated effects like coat color genetics

### Genetics features
Supports Mendelian genetics, sex-linked genes for both mammals (XY) and birds (ZW), and haplodiploidy such as in bees. Genetic linkage is planned but not in yet. Each gene can have up to 256 alleles.

Not planned: Lateral gene transfer, chromosome rearrangement, polyploidy.

### GenomeType
The GenomeType is where you provide data in json format. You need one GenomeType per hybridizable group - so for example horses and donkeys should share the same GenomeType so that they can hybridize. This is usually going to end up meaning one GenomeType per genus, rather than species or family. Animals will not be able to hybridize unless they have the same GenomeType.

This should be a json file with 3 parts:
* "genes": A list of all possible genes and alleles for all combined species using this genome type
* "interpreters": A list of C# classes that figure out what the different gene variants actually mean. "Polygenes" provides the basic built-in functionality. If you write a custom gene interpreter, this is where you link it in.
* "initializers": Set up the probability that an animal starts with specific gene variants when spawned. You can have multiple different initializers, and each one should correspond to a population (as in the biology concept) such as a subspecies, landrace, or breed. You can also add environmental conditions, for example to make a white coat color more common in artic areas.

GenomeType files go in assets/\[modid]/genetics/\[mygenometype].json

Notes on setting up the gene list:

Though you should provide the genes and alleles as nice readable strings, for performance reasons Genelib will internally store them as numbers based on their ordering. So, DO NOT reorder your genes and alleles, or it will break existing save files. Renaming them is ok though.

Avoid naming any alleles "default", because the gene initializer uses that as a keyword.

When the mod is first installed, or when new genes are added, existing entities are assumed to have the _first_ allele listed. If it doesn't have any more specific name, I recommend calling the first allele "wildtype" if it is the wildtype, or otherwise calling it "standard".

You cannot have more than 256 alleles for a single gene.

### Gene Interpreters
A gene interpreter is a C# class that calculates a phenotype based on a genotype. In other words, it looks at what genes an individual animal has and figures out what that animal should look like, how much health or other stats it should have, what it drops when killed, its temperament, or anything else that you care to have genes affect.

To make a gene interpreter, you should write a class implementing the GeneInterpreter interface. See its source code for info on implementing the methods. You will then need to register your gene interpreter class by calling GenomeType.RegisterInterpreter(codename, instance). The codename is the same string you'll use to refer to it from the GenomeType json file.

One interpreter is built-in, the Polygenes interpreter. This provides basic effects, currently just having fertility decrease for inbred animals. It also calculates a probabilistic COI (coefficient of inbreeding), but doesn't do anything with it except write to the entity's WatchedAttributes.

### Entity Behaviors and AI tasks
To get genetics to work right, you will have to add a few specific EntityBehaviors to your entity. By code, they are:

"genelib.genetics": For having genes, what a shocker. Specify "genomeType" here. Required serverside. Optionally can be included clientside, if you have a gene interpreter that should run on the client.

(see note on autoadjustment) "genelib.multiply" replacing "multiply": Allows the entity to create offspring who inherit genes from both parents. Again the main affects are serverside, that's where the data goes, and clientside you just need it to show the player the info text.

(see note on autoadjustment) "genelib.info", optional: Allows the player to open an info GUI for the animal by looking at it and pressing 'N'. This allows them to name the animal and view its parentage. Also provides a "Prevent breeding" checkbox which only works if that species's females use the "genelib.multiply" behavior instead of vanilla "multiply". This inherits from the nametag behavior, so you'll probably want to use it as `{ code: "genelib.info", showtagonlywhentargeted: true }`.

This library does not provide support for genetics on egg-laying species. Those features are still provided by [Detailed Animals](https://github.com/sekelsta/detailedanimals) instead.

Aside from EntityBehaviors, if you have any sex-linked genes you should also add male:true/false to the entity's attributes to specify, for example, that roosters are male and hens are female. For convenience, if you leave this out it will take a guess based on the entity code + variant groups - if the whole thing contains the string "-female" it will be treated as female, otherwise as male.

Note on autoadjustment:
Code mods can ask this library to automatically replace multiply with `genelib.multiply` and add `genelib.info` for all animals, that is, entities with the animal tag. (Note this targets `multiply` specifically and not the `multiplybase` used by birds - so birds still need manual setup.)
To trigger the automatic replacement, set `GenelibSystem.AutoadjustAnimalBehaviors` to `true`. This requires either a compile-time dependency on the mod (easier) or reflection (more annoying).

From older versions: "genelib.grow" is deprecated as of version 2.0 for VS 1.21. Gene passing from baby to adult is handled via Harmony patching the vanilla grow.

### Seasonal animal breeding
Entities using `genelib.multiply` can configure it to have more realistic timing of the breeding if the `seasonalbreeding` is installed. Supported options include:
 - `breedingPeakMonth`, `breedingMonthsBefore`, and `breedingMonthsAfter` - These are what control the season during which the animal breeds. Enter these as numbers, e.g. 0.0 = January 1st, 1.0 = Febuary 1st, 1.5 = Febuary 15th, ..., 11.9 = December 28th. Those example dates are just guesstimates.
 - `pregnancyMonths` which scales with year length and overrides `pregnancyDays` if both are present
 - `multiplyCoolDownMonthsMin` and `multiplyCooldownMonthsMax`, which do likewise
 - `lactationMonths` or `lactationDays` might get other uses in the future, but is currently only used by Detailed Animals for newborn animals to decide which adults they can nurse from
 - `litterAddChance` which adjusts the probabilities of different sized litters between the max and the min
 - `mateTaskPriority` - this should be set appropriately depending on the entity's aitasks behavior
 - `inducedOvulation` true or false, defaults to false
 - `estrousCycleMonths` or `estrousCycleDays`, which make the animal stop being receptive to mating for a time
 - `daysInHeat`, how many days out of the total estrous cycle the animal will be ready to mate
As of version 2.1.4, these options will only scale with year length if the `seasonalbreeding` mod is installed. Otherwise, animals will use months at a fixed rate of 5 days per month. Feel free to let me know if you are trying to do mod compatibility and this detail gets in your way - I'm sure there's a better way it could be set up. Or, if I'm unavailable or you're just shy, it can also be changed from code (by modifying static fields in GenelibConfig).

