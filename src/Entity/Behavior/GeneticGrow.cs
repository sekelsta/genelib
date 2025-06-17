using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

using Genelib.Extensions;

// Copied from VSEssentialsMod EntityBehaviorGrow
namespace Genelib {
    public class GeneticGrow : EntityBehaviorGrow
    {
        public const string Code = "genelib.grow";

        protected override void BecomeAdult(Entity adult, bool keepTextureIndex)
        {
            // Detailed Animals compat
            adult.WatchedAttributes.CopyIfPresent("hunger", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fedByPlayer", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("bodyCondition", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("ownedby", entity.WatchedAttributes);

            adult.WatchedAttributes.CopyIfPresent("nametag", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("genetics", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("motherKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fatherKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterId", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterName", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("fosterKey", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("preventBreeding", entity.WatchedAttributes);
            adult.WatchedAttributes.CopyIfPresent("neutered", entity.WatchedAttributes);

            adult.WatchedAttributes.SetLong("UID", entity.UniqueID());

            // PetAI compat
            adult.WatchedAttributes.CopyIfPresent("domesticationstatus", entity.WatchedAttributes);

            base.BecomeAdult();
        }


        public override string PropertyName()
        {
            return Code;
        }
    }
}
