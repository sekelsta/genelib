using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class GeneticMultiply : EntityBehaviorMultiply {
        public const string Code = "genelib.multiply";

        // Wait this long (as a fraction of the total pregnancy length) before discarding non-viable embryos
        protected double ViabilityCheckDelay = 0.125;
        // Fraction of embryos considered non-viable, randomly determined (a second check after gene viability)
        protected double MiscarriageChance = 0;
        // After a miscarriage (either random or genetic), wait this long before being ready to mate again
        protected double MiscarriageCooldown = 2;
        // Pregnancy length will vary by plus or minus this fraction of the total length
        protected double PregnancyLengthVariation = 0.04;
        // Fraction of pregnancy length after which the expectant mother has a very large tummy and needs to move carefully
        protected double LatePregnancy = 0.67;
        // Chance each slowtick that the animal, if all preconditions are met, looks for a mate
        public double TryGetPregnantChance = 0.06;
        // Chance that when animals mate, the female actually does get pregnant
        public double MatingSuccessChance = 0.8;
        // Saturation consumed on unsuccessful mating (that does not result in the female becoming pregnant)
        public double MatingFoodCost = 1;
        // Priority of the AI task that entities use to mate (both male and female)
        public float MateTaskPriority = 1.5f;

        // Duplicate private field from EntityBehaviorMultiply
        protected AssetLocation[] SpawnEntityCodes;

        public bool pregnancyDaysSpecified = false;
        public double PregnancyDays;
        public string RequiresNearbyEntityCode;
        public float RequiresNearbyEntityRange;

        protected AssetLocation[] SireCodes;

        // Make litter size a bell curve instead of a uniform
        protected double litterAddChance = 0.5;

        // Seasonal breeding
        protected double LactationDays = 0;
        protected double EstrousCycleDays;
        protected double DaysInHeat;
        protected bool InducedOvulation = false;
        protected bool SeasonalBreeding = false;
        protected double BreedingSeasonPeak;
        protected double BreedingSeasonBefore;
        protected double BreedingSeasonAfter;

        protected TreeArrayAttribute Litter {
            get => multiplyTree["litter"] as TreeArrayAttribute;
            set { 
                multiplyTree["litter"] = value;
                entity.WatchedAttributes.MarkPathDirty("multiply");
            }
        }

        protected double TotalDaysPregnancyEnd {
            get => multiplyTree.GetDouble("totalDaysPregnancyEnd");
            set {
                multiplyTree.SetDouble("totalDaysPregnancyEnd", value);
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
        protected override void CheckMultiply(float dt)
        {
            // Deliberately skip call to base.CheckMultiply
            FieldInfo callbackField = typeof(EntityBehaviorMultiply).GetField("callbackId", BindingFlags.Instance | BindingFlags.NonPublic);

            if (!entity.Alive) {
                callbackField.SetValue(this, 0);
                return;
            }

            callbackField.SetValue(this, entity.World.RegisterCallback(CheckMultiply, 3000));

            if (entity.World.Calendar == null) return;

            double daysNow = entity.World.Calendar.TotalDays;

            if (!IsPregnant) {
                TryGetPregnant();
                return;
            }

            if (InEarlyPregnancy) {
                if (entity.World.Calendar.TotalDays > TotalDaysPregnancyStart + PregnancyDays * ViabilityCheckDelay) {
                    EntityBehaviorGenetics gb = entity.GetBehavior<EntityBehaviorGenetics>();
                    if (gb != null) {
                        List<TreeAttribute> surviving = new List<TreeAttribute>();
                        foreach (TreeAttribute childTree in Litter.value) {
                            if (childTree.GetBool("viable", true) == false) continue;
                            Genome childGenome = new Genome(gb.Genome.Type, childTree);
                            if (!childGenome.EmbryonicLethal()) {
                                surviving.Add(childTree);
                            }
                        }
                        if (surviving.Count == 0) {
                            SetNotPregnant();
                        }
                        else {
                            Litter.value = surviving.ToArray();
                            entity.WatchedAttributes.MarkPathDirty("multiply");
                        }
                    }
                    InEarlyPregnancy = false;
                }
                return;
            }
            
            if (daysNow > TotalDaysPregnancyEnd) {
                GiveBirth(0);
            }

            entity.World.FrameProfiler.Mark("multiply");
        }

        protected override bool TryGetPregnant() {
            if (entity.World.Rand.NextDouble() > TryGetPregnantChance) return false;

            if (!EntityCanMate(this.entity)) {
                return false;
            }

            double totalDays = entity.World.Calendar.TotalDays;

            if (TotalDaysCooldownUntil + DaysInHeat < totalDays) TotalDaysCooldownUntil += EstrousCycleDays;

            if (TotalDaysCooldownUntil > totalDays) return false;

            if (!IsBreedingSeason()) {
                TotalDaysCooldownUntil += entity.World.Calendar.DaysPerMonth;
                return false;
            }

            Entity sire = GetRequiredEntityNearby();
            if (sire == null && SireCodes.Length > 0) return false;
            // If no required nearby entity code, then self-fertilize
            sire ??= entity;

            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi == null) {
                MateWith(sire);
                return true;
            }
            if (taskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate) {
                // Already trying
                return false;
            }

            EntityBehaviorTaskAI sireTaskAi = sire.GetBehavior<EntityBehaviorTaskAI>();
            if (sireTaskAi == null) {
                MateWith(sire);
                return true;
            }
            if (!(sireTaskAi.TaskManager.ActiveTasksBySlot[0] is AiTaskMate)) {
                AiTaskMate sireMateTask = new AiTaskMate((EntityAgent)sire, entity);
                sireMateTask.SetPriority(MateTaskPriority);
                sireTaskAi.TaskManager.ExecuteTask(sireMateTask, 0);
            }

            AiTaskMate mateTask = new AiTaskMate((EntityAgent)entity, sire);
            mateTask.SetPriority(MateTaskPriority);
            taskAi.TaskManager.ExecuteTask(mateTask, 0);

            return false;
        }

        public virtual bool EntityHasEatenEnoughToMate(Entity e) {
            return !e.WatchedAttributes.GetBool("doesEat") || GetSaturation() >= PortionsEatenForMultiply;
        }

        public virtual bool EntityCanMate(Entity entity) {
            if (!entity.Alive) {
                return false;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return false;
            }
            if (!entity.MatingAllowed()) {
                return false;
            }
            return EntityHasEatenEnoughToMate(entity);
        }

        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        protected override Entity GetRequiredEntityNearby() {
            if (SireCodes.Length == 0) {
                return null;
            }

            Entity[] entities = entity.World.GetEntitiesAround(entity.Pos.XYZ, RequiresNearbyEntityRange, RequiresNearbyEntityRange,
                (e) => {
                    bool matches = false;
                    foreach (AssetLocation sire in SireCodes) {
                        if (e.WildCardMatch(sire)) {
                            matches = true;
                            break;
                        }
                    }
                    if (!matches || !EntityCanMate(e)) {
                        return false;
                    }
                    return true;
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
            int litterSize = (int)SpawnQuantityMin;
            for (int i = 0; i < (SpawnQuantityMax - SpawnQuantityMin); ++i) {
                if (entity.World.Rand.NextDouble() < litterAddChance) {
                    litterSize += 1;
                }
            }
            return litterSize;
        }

        public bool IsBreedingSeason(double season) {
            if (!SeasonalBreeding || BreedingSeasonBefore + BreedingSeasonAfter >= 1) {
                return true;
            }
            if (season > BreedingSeasonPeak) {
                season -= 1;
            }
            double timeUntilPeak = BreedingSeasonPeak - season;
            return timeUntilPeak < BreedingSeasonBefore || 1 - timeUntilPeak < BreedingSeasonAfter;
        }

        public bool IsBreedingSeason() {
            return IsBreedingSeason(entity.World.Calendar.GetSeasonRel(entity.Pos.AsBlockPos));
        }

        public virtual void MateWith(Entity sire) {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            float saturation = tree?.GetFloat("saturation", 0) ?? 0;
            if (entity.World.Rand.NextDouble() > MatingSuccessChance) {
                tree?.SetFloat("saturation", saturation - (float)MatingFoodCost);
                return;
            }
            tree?.SetFloat("saturation", saturation - PortionsEatenForMultiply);

            if (sire != null)
            {
                ITreeAttribute maletree = sire.WatchedAttributes.GetTreeAttribute("hunger");
                if (maletree != null)
                {
                    saturation = maletree.GetFloat("saturation", 0);
                    maletree.SetFloat("saturation", Math.Max(0, saturation - (float)MatingFoodCost));
                }
            }

            Genome sireGenome = sire.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            Genome ourGenome = entity.GetBehavior<EntityBehaviorGenetics>()?.Genome;
            int litterSize = ChooseLitterSize();

            List<TreeAttribute> litter = new List<TreeAttribute>();
            List<string> genesDebug = new List<string>();
            for (int i = 0; i < litterSize; ++i) {
                TreeAttribute offspring = new TreeAttribute();
                if (entity.World.Rand.NextSingle() < MiscarriageChance) {
                    if (ViabilityCheckDelay == 0) continue;
                    offspring.SetBool("viable", false); // Predetermined, to avoid save-scumming
                }
                AssetLocation offspringCode = SpawnEntityCodes[entity.World.Rand.Next(SpawnEntityCodes.Length)];
                if (ourGenome != null && sireGenome != null) {
                    bool heterogametic = ourGenome.Type.SexDetermination.Heterogametic(entity.IsMale());
                    Genome child = new Genome(ourGenome, sireGenome, heterogametic, entity.World.Rand);
                    child.Mutate(GenelibConfig.MutationRate, entity.World.Rand);
                    TreeAttribute childGeneticsTree = (TreeAttribute) offspring.GetOrAddTreeAttribute("genetics");
                    if (ViabilityCheckDelay == 0 && child.EmbryonicLethal()) {
                        continue;
                    }
                    child.AddToTree(childGeneticsTree);
                    genesDebug.Add(child.anonymous.ArrayToString());
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
            string offspringGenes = genesDebug.Count == 0 ? "" : "\n    pgenes=" + String.Join("\n    pgenes=", genesDebug);

            if (litter.Count == 0) {
                if (litterSize > 0 && MiscarriageCooldown > 0) {
                    TotalDaysCooldownUntil = entity.World.Calendar.TotalDays 
                        + MiscarriageCooldown * (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                }
                return;
            }

            if (ViabilityCheckDelay > 0) {
                InEarlyPregnancy = true;
            }

            IsPregnant = true;
            TotalDaysPregnancyStart = entity.World.Calendar.TotalDays;
            double rate = 1 + 0.08 * (entity.World.Rand.NextDouble() - 0.5);
            TotalDaysPregnancyEnd = TotalDaysPregnancyStart + rate * PregnancyDays;
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
            TotalDaysLastBirth = entity.World.Calendar.TotalDays;
            TotalDaysCooldownUntil = entity.World.Calendar.TotalDays + MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin);
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
            return false;
        }

        public virtual ItemStack LayEgg() {
            // Not implemented
            return null;
        }

        // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
        public override void GetInfoText(StringBuilder infotext) {
            if (!entity.Alive) {
                return;
            }

            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");

            if (IsPregnant) {
                if (!pregnancyDaysSpecified) {
                    infotext.AppendLine(Lang.Get("Is pregnant"));
                    return;
                }
                int passed = (int)Math.Round(entity.World.Calendar.TotalDays - TotalDaysPregnancyStart);
                int expected = (int)Math.Round(PregnancyDays);
                infotext.AppendLine(Lang.Get("genelib:infotext-multiply-pregnancy", passed, expected));
                if (InEarlyPregnancy) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-multiply-earlypregnancy"));
                }
                else if (entity.World.Calendar.TotalDays > TotalDaysPregnancyStart + PregnancyDays * LatePregnancy) {
                    infotext.AppendLine(Lang.Get("genelib:infotext-multiply-latepregnancy"));
                }
                return;
            }
            if (entity.WatchedAttributes.GetBool("neutered", false)) {
                return;
            }

            float animalWeight = entity.WatchedAttributes.GetFloat("animalWeight", 1);
            GetReadinessInfoText(infotext, animalWeight);
        }

        protected virtual void GetReadinessInfoText(StringBuilder infotext, double animalWeight) {
            if (!entity.MatingAllowed()) {
                return;
            }

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree != null && PortionsEatenForMultiply > 0)
            {
                float saturation = tree.GetFloat("saturation", 0);
                infotext.AppendLine(Lang.Get("Portions eaten: {0}", (int)saturation));
            }

            double daysLeft = TotalDaysCooldownUntil - entity.World.Calendar.TotalDays;
            IGameCalendar calendar = entity.World.Calendar;
            double season = (calendar.GetSeasonRel(entity.Pos.AsBlockPos) + daysLeft / calendar.DaysPerMonth / 12) % 1;
            if (!IsBreedingSeason(season)) {
                // TODO: Remove this and put the info in the handbook instead
                double breedingStart = (BreedingSeasonPeak - BreedingSeasonBefore + 1) % 1;
                if (breedingStart < 0.5) {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-longday"));
                }
                else {
                    infotext.AppendLine(Lang.Get("detailedanimals:infotext-reproduce-shortday"));
                }
                return;
            }

            if (daysLeft <= 0) {
                infotext.AppendLine(Lang.Get("game:Ready to mate"));
            }
            else if (daysLeft <= 4) {
                infotext.AppendLine(Lang.Get("genelib:infotext-multiply-waitdays" + Math.Ceiling(daysLeft).ToString()));
            }
            else {
                infotext.AppendLine(Lang.Get("game:Several days left before ready to mate"));
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            if (attributes.KeyExists("pregnancyMonths")) {
                PregnancyDays = attributes["pregnancyMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
                pregnancyDaysSpecified = true;
            }
            else {
                PregnancyDays = attributes["pregnancyDays"].AsDouble(3f);
                pregnancyDaysSpecified = attributes.KeyExists("pregnancyDays");
            }

            RequiresNearbyEntityRange = attributes["requiresNearbyEntityRange"].AsFloat(16);

            RequiresNearbyEntityCode = null;
            string[] sireStrings;
            if (attributes.KeyExists("sireCodes")) {
                sireStrings = attributes["sireCodes"].AsArray<string>();
            }
            else {
                sireStrings = new string[] { attributes["requiresNearbyEntityCode"].AsString("") };
            }
            SireCodes = sireStrings.Select(x => AssetLocation.Create(x, entity.Code.Domain)).ToArray();

            // Based on a copy-paste from EntityBehaviorMultiply of VSEssentialsMod
            JsonObject sec = attributes["spawnEntityCodes"];   // Optional fancier syntax in version 1.19+
            if (!sec.Exists)
            {
                sec = attributes["spawnEntityCode"];    // The simple property as it was pre-1.19 - can still be used, suitable for the majority of cases
                if (sec.Exists) SpawnEntityCodes = new AssetLocation[] { new AssetLocation(sec.AsString("")) };
                return;
            }
            if (sec.IsArray())
            {
                SpawnEntityProperties[] codes = sec.AsArray<SpawnEntityProperties>();
                SpawnEntityCodes = new AssetLocation[codes.Length];
                for (int i = 0; i < codes.Length; i++) SpawnEntityCodes[i] = new AssetLocation(codes[i].Code ?? "");
            }
            else
            {
                SpawnEntityCodes = new AssetLocation[] { new AssetLocation(sec.AsString("")) };
            }

            if (attributes.KeyExists("litterAddChance")) {
                litterAddChance = attributes["litterAddChance"].AsDouble();
            }
            if (attributes.KeyExists("mateTaskPriority")) {
                MateTaskPriority = attributes["mateTaskPriority"].AsFloat();
            }

            if (attributes.KeyExists("multiplyCooldownMonthsMin")) {
                MultiplyCooldownDaysMin = attributes["multiplyCooldownMonthsMin"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            if (attributes.KeyExists("multiplyCooldownMonthsMax")) {
                MultiplyCooldownDaysMax = attributes["multiplyCooldownMonthsMax"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }

            // Seasonal breeding
            InducedOvulation = attributes["inducedOvulation"].AsBool(false);
            if (InducedOvulation) {
                EstrousCycleDays = MultiplyCooldownDaysMax;
                DaysInHeat = EstrousCycleDays;
            }
            else {
                if (attributes.KeyExists("estrousCycleMonths")) {
                    EstrousCycleDays = attributes["estrousCycleMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
                }
                else if (attributes.KeyExists("estrousCycleDays")) {
                    EstrousCycleDays = attributes["estrousCycleDays"].AsDouble();
                }
                else {
                    EstrousCycleDays = entity.World.Calendar.DaysPerMonth;
                }

                if (attributes.KeyExists("daysInHeat")) {
                    DaysInHeat = attributes["daysInHeat"].AsDouble();
                    DaysInHeat *= Math.Clamp(entity.World.Calendar.DaysPerMonth, 3, 9) / 9;
                }
                else {
                    DaysInHeat = 2;
                }
            }
            if (attributes.KeyExists("breedingPeakMonth")) {
                SeasonalBreeding = true;
                BreedingSeasonPeak = attributes["breedingPeakMonth"].AsDouble() / 12;
                BreedingSeasonBefore = attributes["breedingMonthsBefore"].AsDouble() / 12;
                BreedingSeasonAfter = attributes["breedingMonthsAfter"].AsDouble() / 12;
            }

            if (attributes.KeyExists("lactationMonths")) {
                LactationDays = attributes["lactationMonths"].AsDouble() * entity.World.Calendar.DaysPerMonth;
            }
            else if (attributes.KeyExists("lactationDays")) {
                LactationDays = attributes["lactationDays"].AsDouble();
            }

            if (IsPregnant) {
                if (Litter == null) {
                    IsPregnant = false;
                }
                else {
                    double length = TotalDaysPregnancyEnd - TotalDaysPregnancyStart;
                    if (length < 0.8 * PregnancyDays || length > 1.2 * PregnancyDays) {
                        double rate = 1 + PregnancyLengthVariation * 2 * (entity.World.Rand.NextDouble() - 0.5); // Random from 0.96 to 1.04
                        TotalDaysPregnancyEnd = TotalDaysPregnancyStart + rate * PregnancyDays;
                    }
                }
            }
        }
    }
}
