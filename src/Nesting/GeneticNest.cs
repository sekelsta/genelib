using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using Genelib.Extensions;

namespace Genelib {
    public class GeneticNest : BlockEntityHenBox {
        protected bool WasOccupied = false;
        protected long LastOccupier = -1;
        protected double LastUpdateHours = -1;

        public double decayHours;
        public double DecayStartHours = double.MaxValue;

        public bool Permenant => decayHours <= 0;

        public bool Occupied() {
            return occupier != null && occupier.Alive;
        }

        public override void SetOccupier(Entity entity) {
            if (occupier == entity) {
                return;
            }
            occupier = entity;
            if (entity != null) {
                LastOccupier = entity.UniqueID();
            }
            MarkDirty();
        }

        public bool ContainsRot() {
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                if (isRot(inventory[i].Itemstack)) {
                    return true;
                }
            }
            return false;
        }

        private bool isRot(ItemStack stack) {
            AssetLocation code = stack.Collectible?.Code;
            return code != null && code.Domain == "game" && code.Path == "rot";
        }

        public override float DistanceWeighting {
            get {
                int numEggs = CountEggs();
                return numEggs == 0 ? 2 : 3 / (numEggs + 2);
            }
        }

        public bool Full() {
            return CountEggs() >= inventory.Count;
        }

        public override void Initialize(ICoreAPI api) {
            base.Initialize(api);

            decayHours = Block.Attributes?["decayHours"]?.AsDouble(0) ?? 0;

            (container as ConstantPerishRateContainer).PerishRate = Block.Attributes?["perishRate"]?.AsFloat(1) ?? 1;
            if (LastUpdateHours == -1) {
                LastUpdateHours = Api.World.Calendar.TotalHours;
            }

            if (api.Side == EnumAppSide.Server) {
                IsOccupiedClientside = false;
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(SlowTick, 12000);
                SlowTick(0);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);
            tree.SetDouble("lastUpdateHours", LastUpdateHours);
            tree.SetDouble("decayStartHours", DecayStartHours);
            tree.SetBool("wasOccupied", WasOccupied);
            tree.SetLong("lastOccupier", LastOccupier);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving) {
            WasOccupied = tree.GetBool("wasOccupied");
            LastUpdateHours = tree.GetDouble("lastUpdateHours");
            DecayStartHours = tree.GetDouble("decayStartHours");
            LastOccupier = tree.GetLong("lastOccupier", -1);
            base.FromTreeAttributes(tree, worldForResolving);
        }

        protected void SlowTick(float dt) {
            bool anyEggs = false;
            bool anyRot = false;
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                ItemStack stack = inventory[i].Itemstack;
                if (isRot(stack)) {
                    anyRot = true;
                    continue;
                }
                anyEggs = true;
                TreeAttribute chickData = (TreeAttribute) stack.Attributes["chick"];
                if (chickData == null) {
                    continue;
                }
                string chickCode = chickData.GetString("code");
                if (chickCode == null || chickCode == "") {
                    continue;
                }
                if (WasOccupied) {
                    double incubationHoursPrev = stack.Attributes.GetDouble("incubationHoursRemaining", 0.0);
                    double incubationHoursNext = incubationHoursPrev - (Api.World.Calendar.TotalHours - LastUpdateHours);
                    double incubationHoursTotal = stack.Attributes.GetDouble("incubationHoursTotal", 0);
                    double check = 0.1;
                    if (incubationHoursTotal > 0 && 1 - (incubationHoursPrev / incubationHoursTotal) < check 
                                                 && 1 - (incubationHoursNext / incubationHoursTotal) >= check) {
                        EntityProperties spawnType = Api.World.GetEntityType(chickCode);
                        if (spawnType == null) {
                            throw new Exception(Block.Code.ToString() + " attempted to incubate egg containing entity with code " + chickCode.ToString() + ", but no such entity was found.");
                        }
                        GenomeType genomeType = spawnType.GetGenomeType();
                        if (genomeType != null) {
                            Genome childGenome = new Genome(genomeType, chickData);
                            if (childGenome.EmbryonicLethal()) {
                                chickCode = null;
                                chickData.SetString("code", "");
                            }
                        }
                    }

                    if (incubationHoursNext <= 0 && chickCode != null && chickCode != "") {
                        EntityPos pos = new EntityPos().SetPos(Pos);
                        pos.X += 0.5;
                        pos.Z += 0.5;
                        pos.Y += 0.05;
                        Entity chick = GeneticMultiply.SpawnNewborn(Api.World, pos, occupier, chickData.GetInt("generation", 0), chickData);
                        inventory[i].Itemstack = null;
                        if (LastOccupier != -1 && !chick.WatchedAttributes.HasAttribute("fosterId")) {
                            chick.WatchedAttributes.SetLong("fosterId", LastOccupier);
                        }
                    }
                    else {
                        stack.Attributes.SetDouble("incubationHoursRemaining", incubationHoursNext);
                    }

                    inventory.DidModifyItemSlot(inventory[i]);
                }
            }

            if (!Permenant) {
                if ((anyEggs && !anyRot) || DecayStartHours > Api.World.Calendar.TotalHours) {
                    DecayStartHours = Api.World.Calendar.TotalHours;
                }
                else {
                    if (DecayStartHours + decayHours < Api.World.Calendar.TotalHours) {
                        Api.World.BlockAccessor.SetBlock(0, Pos);
                    }
                }
            }

            LastUpdateHours = Api.World.Calendar.TotalHours;
            WasOccupied = Occupied();
            MarkDirty();
        }

        public override bool CanPlayerPlaceItem(ItemStack stack) {
            if (stack?.Collectible.Attributes == null) {
                return false;
            }
            return stack.Collectible.Attributes["nestitem"].AsBool(false);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder info) {
            // Deliberately avoid calling base method
            bool anyEggs = false;
            bool anyRot = false;
            bool anyFertile = false;
            for (int i = 0; i < inventory.Count; ++i) {
                if (inventory[i].Empty) {
                    continue;
                }
                ItemStack stack = inventory[i].Itemstack;
                if (isRot(stack)) {
                    anyRot = true;
                }
                else {
                    anyEggs = true;
                }
                if (stack.Collectible.RequiresTransitionableTicking(Api.World, inventory[i].Itemstack)) {
                    info.Append(BlockEntityShelf.PerishableInfoCompact(Api, inventory[i], 0));
                }
                else {
                    info.AppendLine(inventory[i].GetStackName());
                }
                TreeAttribute chickData = (TreeAttribute) stack.Attributes["chick"];
                if (chickData != null) {
                    string chickCode = chickData.GetString("code");
                    if (chickCode == null || chickCode == "") {
                        info.AppendLine(" • " + Lang.Get("detailedanimals:blockinfo-fertilitylost"));
                    }
                    else {
                        anyFertile = true;
                        double hours = stack.Attributes.GetDouble("incubationHoursRemaining", 0.0);
                        info.AppendLine(" • " + Lang.Get("detailedanimals:infotext-incubationtime", VSExtensions.TranslateTimeFromHours(Api, hours)));
                    }
                }
            }
            if (anyEggs) {
                if (!anyFertile) {
                    info.AppendLine(Lang.Get("No eggs are fertilized"));
                }
                else if (Full() && !anyRot) {
                    if (!IsOccupiedClientside && !WasOccupied) {
                        info.AppendLine(Lang.Get("A broody hen is needed!"));
                    }
                    else if (!WasOccupied) {
                        info.AppendLine(Lang.Get("detailedanimals:blockinfo-nestbox-eggs-warming"));
                    }
                    else if (!IsOccupiedClientside) {
                        info.AppendLine(Lang.Get("detailedanimals:blockinfo-nestbox-eggs-cooling"));
                    }
                    else {
                        info.AppendLine(Lang.Get("detailedanimals:blockinfo-nestbox-eggs-incubating"));
                    }
                }
            }
            if (anyRot) {
                info.AppendLine(Lang.Get("detailedanimals:blockinfo-nest-rotten"));
                return;
            }
        }
    }
}
