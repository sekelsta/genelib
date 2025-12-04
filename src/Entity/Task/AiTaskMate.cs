using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Genelib {
    public class AiTaskMate : AiTaskGotoEntity {
        protected bool prevAlwaysActiveValue;

        public AiTaskMate(EntityAgent entity, Entity target)  : base(entity, target) {
            allowedExtraDistance = 0.2f;
        }

        public void SetPriority(float value) {
            this.priority = value;
            this.priorityForCancel = value;
        }

        public override void StartExecute() {
            base.StartExecute();
            prevAlwaysActiveValue = entity.AlwaysActive;
            entity.AlwaysActive = true;
            entity.State = EnumEntityState.Active;
        }

        public override bool ContinueExecute(float dt) {
            bool result = base.ContinueExecute(dt);
            // Optimization here: Base function runs logic equivalent to TargetReached() and includes it in result
            if (!result && TargetReached()) {
                GeneticMultiply? multiply = entity.GetBehavior<GeneticMultiply>();
                if (multiply != null) {
                    multiply.MateWith(targetEntity);
                }
            }
            return result;
        }

        public override void FinishExecute(bool cancelled) {
            base.FinishExecute(cancelled);
            entity.AlwaysActive = prevAlwaysActiveValue;
        }
    }
}
