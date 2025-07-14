using Genelib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Genelib {
    public class EntityBehaviorGenetics : EntityBehavior {
        public const string Code = "genelib.genetics";

        protected GenomeType GenomeType { get; set; } = null!;
        private Genome genome = null!;
        public Genome Genome {
            get => genome;
            set {
                genome = value;
                GenomeModified();
            }
        }
        protected string[]? initializers;
        protected GeneInitializer defaultInitializer = null!;

        public EntityBehaviorGenetics(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes) {
            GenomeType = GenomeType.Get(
                AssetLocation.Create(attributes["genomeType"].AsString(), entity.Code.Domain)
            );

            string? definit = attributes["defaultInitializer"].AsString();
            defaultInitializer = definit == null ? GenomeType.DefaultInitializer : GenomeType.Initializer(definit);

            initializers = attributes["initializers"].AsArray<string>()?.Select(x => x ?? throw new ArgumentNullException("entry in initializers list for entity with code " + entity.Code)).ToArray();

            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetTreeAttribute("genetics");
            if (geneticsTree != null) {
                Genome = new Genome(GenomeType, geneticsTree);
            }
        }

        public override void AfterInitialized(bool onRuntimeSpawn) {
            if (entity.World.Side == EnumAppSide.Server) {
                if (Genome == null) {
                    Random random = entity.World.Rand;
                    bool heterogametic = GenomeType.SexDetermination.Heterogametic(entity.IsMale());
                    BlockPos blockPos = entity.Pos.AsBlockPos;
                    ClimateCondition climate = entity.Api.World.BlockAccessor.GetClimateAt(blockPos);
                    GeneInitializer initializer = GenomeType.ChooseInitializer(initializers, climate, blockPos.Y, random) ?? defaultInitializer;
                    Genome = new Genome(initializer, heterogametic, random);
                    Genome.Mutate(GenelibConfig.MutationRate, random);
                    if (!onRuntimeSpawn) {
                        // Note the API does not provide a good way to distinguish between loading from save or spawning 
                        // at worldgen. So assume we are 
                        // creating genetics for a previously existing entity. Set genes to match phenotype.
                        foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                            interpreter.MatchPhenotype(this);
                        }
                    }
                    foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                        interpreter.FinalizeSpawn(Genome, initializer, random);
                    }
                }
            }
            foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                interpreter.Interpret(this);
            }
        }

        public void GenomeModified() {
            if (entity.World.Side == EnumAppSide.Client) {
                return;
            }
            TreeAttribute geneticsTree = (TreeAttribute) entity.WatchedAttributes.GetOrAddTreeAttribute("genetics");
            genome.AddToTree(geneticsTree);
            entity.WatchedAttributes.MarkPathDirty("genetics");
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements) {
            ICoreClientAPI? clientAPI = entity.Api as ICoreClientAPI;

            if (clientAPI == null) return;

            List<(AssetLocation, Shape)>? overlays = null;
            foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                interpreter.PrepareShape(this, ref overlays, shapePathForLogging, ref willDeleteElements);
            }

            if (overlays == null || overlays.Count == 0) return;

            if (!shapeIsCloned) {
                entityShape = entityShape.Clone();
                shapeIsCloned = true;
            }

            string? prefix = null;
            foreach ((AssetLocation, Shape) overlay in overlays) {
                AssetLocation shapePath = overlay.Item1;
                Shape? partShape = overlay.Item2 ?? Shape.TryGet(entity.Api, shapePath);

                if (partShape == null) {
                    entity.Api.Logger.Warning("Entity shape part {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Shape part will be invisible.", shapePath, entity.Properties.Code, shapePath);
                    continue;
                }

                partShape.SubclassForStepParenting(prefix);

                var textures = entity.Properties.Client.Textures;
                entityShape.StepParentShape(partShape, shapePath.ToShortString(), shapePathForLogging, entity.Api.Logger, (texcode, loc) =>
                {
                    if (prefix == null && textures.ContainsKey(texcode)) return;

                    var cmpt = textures[prefix + "-" + texcode] = new CompositeTexture(loc);
                    cmpt.Bake(entity.Api.Assets);
                    clientAPI.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                    cmpt.Baked.TextureSubId = textureSubid;
                });
            }
        }

        public override ITexPositionSource? GetTextureSource(ref EnumHandling handling) {
            ITexPositionSource? source = null;
            foreach (GeneInterpreter interpreter in Genome.Type.Interpreters) {
                source = interpreter.GetTextureSource(this, ref handling);
                if (handling == EnumHandling.PreventSubsequent) {
                    return source;
                }
            }
            return source;
        }

        public override string PropertyName() => Code;
    }
}
