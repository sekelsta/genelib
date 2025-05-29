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

        // Duplicate private parts from EntityBehaviorMultiply
        protected JsonObject typeAttributes;
        protected long callbackId = 0;
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
            
            if (saturation >= PortionsEatenForMultiply)
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

        public void MateWith(Entity sire) {
            if (spawnEntityCodes == null) PopulateSpawnEntityCodes();

            IsPregnant = true;
            TotalDaysPregnancyStart = entity.World.Calendar.TotalDays;
            Genome sireGenome = sire.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            Genome ourGenome = entity.GetBehavior<EntityBehaviorGenetics>()?.Genome;

            float q = SpawnQuantityMin + (float)entity.World.Rand.NextDouble() * (SpawnQuantityMax - SpawnQuantityMin);
            int litterSize = (int)Math.Floor(q);
            if (entity.World.Rand.NextSingle() > q - litterSize) {
                litterSize += 1;
            }

            TreeArrayAttribute litterData = new TreeArrayAttribute();
            litterData.value = new TreeAttribute[litterSize];
            for (int i = 0; i < litterSize; ++i) {
                AssetLocation offspringCode = spawnEntityCodes[entity.World.Rand.Next(spawnEntityCodes.Length)];
                litterData.value[i] = new TreeAttribute();
                if (ourGenome != null && sireGenome != null) {
                    bool heterogametic = ourGenome.Type.SexDetermination.Heterogametic(entity.IsMale());
                    Genome child = new Genome(ourGenome, sireGenome, heterogametic, entity.World.Rand);
                    child.Mutate(GenelibConfig.MutationRate, entity.World.Rand);
                    TreeAttribute childGeneticsTree = (TreeAttribute) litterData.value[i].GetOrAddTreeAttribute("genetics");
                    if (!child.EmbryonicLethal()) {
                        child.AddToTree(childGeneticsTree);
                    }
                }
                litterData.value[i].SetString("code", offspringCode.ToString());

                litterData.value[i].SetLong("motherId", entity.UniqueID());
                string motherName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (motherName != null && motherName != "") {
                    litterData.value[i].SetString("motherName", motherName);
                }
                litterData.value[i].SetString("motherKey", entity.Code.Domain + ":item-creature-" + entity.Code.Path);

                litterData.value[i].SetLong("fatherId", sire.UniqueID());
                string fatherName = sire.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (fatherName != null && fatherName != "") {
                    litterData.value[i].SetString("fatherName", fatherName);
                }
                litterData.value[i].SetString("fatherKey", sire.Code.Domain + ":item-creature-" + sire.Code.Path);
            }
            Litter = litterData;
            if (litterData.value.Length == 0) {
                SetNotPregnant();
            }

            entity.WatchedAttributes.MarkPathDirty("multiply");
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

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            this.typeAttributes = attributes;

            if (entity.World.Side == EnumAppSide.Server)
            {
                if (!multiplyTree.HasAttribute("totalDaysLastBirth"))
                {
                    TotalDaysLastBirth = -9999;
                }

                callbackId = entity.World.RegisterCallback(CheckMultiply, 3000);
            }
        }
    }
}
