using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class BlockGeneticNest : Block {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as GeneticNest;
            if (blockEntity != null) {
                return blockEntity.OnInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
