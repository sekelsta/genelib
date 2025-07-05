using System;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class GeneticMultiply : EntityBehaviorMultiply {
        public const string Code = "genelib.multiply";
        protected double viabilityCheckDelay = 0;
        protected double miscarriageChance = 0;

        // Duplicate private parts from EntityBehaviorMultiply
        protected JsonObject typeAttributes;
        protected AssetLocation[] spawnEntityCodes;

        public float PregnancyDays
        {
            get { return typeAttributes["pregnancyDays"].AsFloat(3f); }
        }

        public string RequiresNearbyEntityCode
        {
            get { return typeAttributes["requiresNearbyEntityCode"].AsString(""); }
        }

        public float RequiresNearbyEntityRange
        {
            get { return typeAttributes["requiresNearbyEntityRange"].AsFloat(5); }
        }

        protected TreeArrayAttribute Litter {
            get => multiplyTree["litter"] as TreeArrayAttribute;
            set { 
                multiplyTree["litter"] = value;
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
        }

        public bool InEarlyPregnancy {
            get => multiplyTree.GetBool("earlyPregnancy", true);
            set {
                multiplyTree.SetBool("earlyPregnancy", value);
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
        }

        public GeneticMultiply(Entity entity) : base(entity) { }

        public void SetNotPregnant() {
            IsPregnant = false;
            multiplyTree.RemoveAttribute("litter");
            entity.WatchedAttributes.MarkPathDirty("multiply");
        }


        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        protected override bool TryGetPregnant() {
            if (entity.World.Rand.NextDouble() > 0.06) return false;
            if (TotalDaysCooldownUntil > entity.World.Calendar.TotalDays) return false;

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) return false;

            float saturation = tree.GetFloat("saturation", 0);
            
            if (saturation >= PortionsEatenForMultiply && entity.MatingAllowed())
            {
                Entity maleentity = null;
                if (RequiresNearbyEntityCode != null && (maleentity = GetRequiredEntityNearby()) == null) return false;

                if (entity.World.Rand.NextDouble() < 0.2)
                {
                    tree.SetFloat("saturation", saturation - 1);
                    return false;
                }

                tree.SetFloat("saturation", saturation - PortionsEatenForMultiply);

                if (maleentity != null)
                {
                    ITreeAttribute maletree = maleentity.WatchedAttributes.GetTreeAttribute("hunger");
                    if (maletree != null)
                    {
                        saturation = maletree.GetFloat("saturation", 0);
                        maletree.SetFloat("saturation", Math.Max(0, saturation - 1));
                    }
                }

                // If no required nearby entity code, then self-fertilize
                MateWith(maleentity ?? entity);

                return true;
            }

            return false;
        }

        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        protected override Entity GetRequiredEntityNearby() {
            if (RequiresNearbyEntityCode == null) {
                return null;
            }

            AssetLocation sire = new AssetLocation(RequiresNearbyEntityCode);

            Entity[] entities = entity.World.GetEntitiesAround(entity.Pos.XYZ, RequiresNearbyEntityRange, RequiresNearbyEntityRange,
                (e) => {
                    if (e.WildCardMatch(sire) && e.MatingAllowed()) {
                        if (!e.WatchedAttributes.GetBool("doesEat") || (e.WatchedAttributes["hunger"] as ITreeAttribute)?.GetFloat("saturation") >= 1) {
                            return true;
                        }
                    }
                    return false;
                }
            );
            if (entities == null || entities.Length == 0) {
                return null;
            }
            if (entity.World.Rand.NextSingle() < 0.1f) {
                return entities[entity.World.Rand.Next(entities.Length)];
            }
            return ChooseAvoidingCloseRelatives(entity, entities);
        }

        public static Entity ChooseAvoidingCloseRelatives(Entity entity, Entity[] entities) {
            if (entities == null || entities.Length == 0) {
                return null;
            }
            Entity best = entities[0];
            bool closeRelative = entity.IsCloseRelative(best);
            float distance = entity.Pos.SquareDistanceTo(best.Pos);
            for (int i = 1; i < entities.Length; ++i) {
                if (closeRelative && !entity.IsCloseRelative(entities[i])) {
                    best = entities[i];
                    closeRelative = false;
                    continue;
                }
                float currentDistance = entity.Pos.SquareDistanceTo(entities[i].Pos);
                if (distance > currentDistance) {
                    best = entities[i];
                    distance = currentDistance;
                }
            }
            return best;
        }

        public virtual int ChooseLitterSize() {
            float q = SpawnQuantityMin + (float)entity.World.Rand.NextDouble() * (SpawnQuantityMax - SpawnQuantityMin);
            int litterSize = (int)Math.Floor(q);
            if (entity.World.Rand.NextSingle() < q - litterSize) {
                litterSize += 1;
            }
            return litterSize;
        }

        public virtual bool MateWith(Entity sire) {
            if (spawnEntityCodes == null) PopulateSpawnEntityCodes();

            Genome sireGenome = sire.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            Genome ourGenome = entity.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            int litterSize = ChooseLitterSize();

            List<TreeAttribute> litter = new List<TreeAttribute>();
            for (int i = 0; i < litterSize; ++i) {
                if (viabilityCheckDelay == 0 && entity.World.Rand.NextSingle() < miscarriageChance) {
                    continue;
                }
                AssetLocation offspringCode = spawnEntityCodes[entity.World.Rand.Next(spawnEntityCodes.Length)];
                TreeAttribute offspring = new TreeAttribute();
                if (ourGenome != null && sireGenome != null) {
                    bool heterogametic = ourGenome.Type.SexDetermination.Heterogametic(entity.IsMale());
                    Genome child = new Genome(ourGenome, sireGenome, heterogametic, entity.World.Rand);
                    child.Mutate(GenelibConfig.MutationRate, entity.World.Rand);
                    TreeAttribute childGeneticsTree = offspring.GetOrAddTreeAttribute("genetics");
                    if (viabilityCheckDelay == 0 && child.EmbryonicLethal()) {
                        continue;
                    }
                    child.AddToTree(childGeneticsTree);
                }
                offspring.SetString("code", offspringCode.ToString());

                offspring.SetLong("motherId", entity.UniqueID());
                string motherName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (motherName != null && motherName != "") {
                    offspring.SetString("motherName", motherName);
                }
                offspring.SetString("motherKey", entity.Code.Domain + ":item-creature-" + entity.Code.Path);

                offspring.SetLong("fatherId", sire.UniqueID());
                string fatherName = sire.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (fatherName != null && fatherName != "") {
                    offspring.SetString("fatherName", fatherName);
                }
                offspring.SetString("fatherKey", sire.Code.Domain + ":item-creature-" + sire.Code.Path);
                litter.Add(offspring);
            }

            if (litter.Count == 0) {
                return false;
            }

            TreeArrayAttribute litterData = new TreeArrayAttribute();
            litterData.value = litter.ToArray();
            Litter = litterData;
        }

        protected override void GiveBirth(float q) {
            int nextGeneration = entity.WatchedAttributes.GetInt("generation", 0) + 1;
            TreeAttribute[] litterData = Litter?.value;
            foreach (TreeAttribute childData in litterData) {
                Entity spawn = SpawnNewborn(entity.World, entity.Pos, entity, nextGeneration, childData);
            }
            SetNotPregnant();
        }

        public static Entity SpawnNewborn(IWorldAccessor world, EntityPos pos, Entity foster, int nextGeneration, TreeAttribute childData) {
            AssetLocation spawnCode = new AssetLocation(childData.GetString("code"));
            EntityProperties spawnType = world.GetEntityType(spawnCode);
            if (spawnType == null) {
                throw new ArgumentException(foster?.Code.ToString() + " attempted to hatch or give birth to entity with code " 
                    + spawnCode.ToString() + ", but no such entity was found.");
            }
            Entity spawn = world.ClassRegistry.CreateEntity(spawnType);
            spawn.ServerPos.SetFrom(pos);
            spawn.ServerPos.Yaw = world.Rand.NextSingle() * GameMath.TWOPI;
            Random random = world.Rand;
            spawn.ServerPos.Motion.X += (random.NextDouble() - 0.5f) / 20f;
            spawn.ServerPos.Motion.Z += (random.NextDouble() - 0.5f) / 20f;
            spawn.Pos.SetFrom(spawn.ServerPos);
            spawn.Attributes.SetString("origin", "reproduction");
            spawn.WatchedAttributes.SetInt("generation", nextGeneration);
            // Alternately, call childData.RemoveAttribute("code"), then copy over all remaining attributes
            spawn.WatchedAttributes.SetLong("fatherId", childData.GetLong("fatherId"));
            spawn.WatchedAttributes.CopyIfPresent("fatherName", childData);
            spawn.WatchedAttributes.CopyIfPresent("fatherKey", childData);
            spawn.WatchedAttributes.SetLong("motherId", childData.GetLong("motherId"));
            spawn.WatchedAttributes.CopyIfPresent("motherName", childData);
            spawn.WatchedAttributes.CopyIfPresent("motherKey", childData);
            spawn.SetFoster(foster);
            spawn.WatchedAttributes.CopyIfPresent("genetics", childData);
            spawn.WatchedAttributes.SetDouble("birthTotalDays", world.Calendar.TotalDays);

            world.SpawnEntity(spawn);
            return spawn;
        }

        public virtual bool CanLayEgg() {
            // Not implemented
            return false;
        }

        public virtual ItemStack LayEgg() {
            // Not implemented
            return null;
        }

        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        protected override void PopulateSpawnEntityCodes()
        {
            base.PopulateSpawnEntityCodes();
            JsonObject sec = typeAttributes["spawnEntityCodes"];   // Optional fancier syntax in version 1.19+
            if (!sec.Exists)
            {
                sec = typeAttributes["spawnEntityCode"];    // The simple property as it was pre-1.19 - can still be used, suitable for the majority of cases
                if (sec.Exists) spawnEntityCodes = new AssetLocation[] { new AssetLocation(sec.AsString("")) };
                return;
            }
            if (sec.IsArray())
            {
                SpawnEntityProperties[] codes = sec.AsArray<SpawnEntityProperties>();
                spawnEntityCodes = new AssetLocation[codes.Length];
                for (int i = 0; i < codes.Length; i++) spawnEntityCodes[i] = new AssetLocation(codes[i].Code ?? "");
            }
            else
            {
                spawnEntityCodes = new AssetLocation[] { new AssetLocation(sec.AsString("")) };
            }
        }

        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            this.typeAttributes = attributes;
        }
    }
}
