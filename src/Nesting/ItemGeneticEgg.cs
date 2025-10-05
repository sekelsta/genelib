using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Genelib.Extensions;

#nullable enable

namespace Genelib {
    public class ItemGeneticEgg : Item {
        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority) {
            ArgumentNullException.ThrowIfNull(sinkStack);
            ArgumentNullException.ThrowIfNull(sourceStack);

            string? sourceChickCode = sourceStack.Attributes?.GetTreeAttribute("chick")?.GetString("code");
            string? sinkChickCode = sinkStack.Attributes?.GetTreeAttribute("chick")?.GetString("code");
            if (priority == EnumMergePriority.AutoMerge 
                    && ((sourceChickCode != null && sourceChickCode != "") || (sinkChickCode != null && sinkChickCode != ""))) {
                return base.GetMergableQuantity(sinkStack, sourceStack, priority);
            }

            string[] ignored = new string[GlobalConstants.IgnoredStackAttributes.Length + 3];
            ignored[0] = "chick";
            ignored[1] = "incubationHoursRemaining";
            ignored[2] = "incubationHoursTotal";
            Array.Copy(GlobalConstants.IgnoredStackAttributes, 0, ignored, 3, GlobalConstants.IgnoredStackAttributes.Length);
            if (Equals(sinkStack, sourceStack, ignored) && sinkStack.StackSize < MaxStackSize) {
                return Math.Min(MaxStackSize - sinkStack.StackSize, sourceStack.StackSize);
            }

            return 0;
        }

        // Exact duplicate of ItemEgg.TryMergeStacks. Not inheriting, because I call Item.GetMergableQuantity directly, above
        public override void TryMergeStacks(ItemStackMergeOperation op) {
            IAttribute? sourceChick = op.SourceSlot.Itemstack?.Attributes?["chick"];
            IAttribute? sinkChick = op.SinkSlot.Itemstack?.Attributes?["chick"];
            bool chickDataMatches = (sourceChick == null && sinkChick == null) || (sourceChick != null && sourceChick.Equals(sinkChick));
            base.TryMergeStacks(op);
            if (op.MovedQuantity > 0 && !chickDataMatches) {
                op.SinkSlot.Itemstack?.Attributes?.RemoveAttribute("chick");
                op.SinkSlot.Itemstack?.Attributes?.RemoveAttribute("incubationHoursRemaining");
                op.SinkSlot.Itemstack?.Attributes?.RemoveAttribute("incubationHoursTotal");
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ITreeAttribute? chickData = inSlot.Itemstack?.Attributes?.GetTreeAttribute("chick");
            string? chickCode = chickData?.GetString("code");
            if (chickCode == null || chickCode == "") {
                return;
            }

            double hours = inSlot!.Itemstack!.Attributes.GetDouble("incubationHoursRemaining", 0.0);
            dsc.AppendLine(Lang.Get("genelib:egg-fertile", Lang.Get("genelib:infotext-incubationtime", VSExtensions.TranslateTimeFromHours(world.Api, hours))));
        }
    }
}
