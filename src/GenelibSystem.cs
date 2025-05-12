using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace Genelib
{
    public class GenelibSystem : ModSystem
    {
        public static readonly string modid = "genelib";
        public static AssetCategory genetics = null;

        internal static ICoreServerAPI ServerAPI { get; private set; }
        internal static ICoreClientAPI ClientAPI { get; private set; }
        internal static ICoreAPI API => (ICoreAPI)ServerAPI ?? (ICoreAPI)ClientAPI;

        private static Harmony harmony = new Harmony("sekelsta.genelib");

        // Called during intial mod loading, called before any mod receives the call to Start()
        public override void StartPre(ICoreAPI api) {
            genetics = new AssetCategory(nameof(genetics), true, EnumAppSide.Server);
        }

        // Called on server and client
        public override void Start(ICoreAPI api) {
            harmony.Patch(
                typeof(ServerMain).GetMethod("SendServerAssets", BindingFlags.Instance | BindingFlags.Public),
                postfix: new HarmonyMethod(typeof(GenelibSystem).GetMethod("SendServerAssets_Postfix", BindingFlags.Static | BindingFlags.Public)) 
            );

            api.RegisterBlockClass("Genelib.BlockNest", typeof(BlockGeneticNest));
            api.RegisterBlockEntityClass("Genelib.Nest", typeof(GeneticNest));

            api.RegisterEntityBehaviorClass(EntityBehaviorGenetics.Code, typeof(EntityBehaviorGenetics));
            api.RegisterEntityBehaviorClass(GeneticMultiply.Code, typeof(GeneticMultiply));
            api.RegisterEntityBehaviorClass(GeneticGrow.Code, typeof(GeneticGrow));

            GenomeType.RegisterInterpreter(new PolygeneInterpreter());

            GenelibConfig.Load(api);
        }

        public override void AssetsLoaded(ICoreAPI api) {
            LoadAssetType(api, genetics.Code, (asset) => GenomeType.Load(asset), "genome types");
        }

        public void LoadAssetType(ICoreAPI api, string category, Action<IAsset> onLoaded, string typeName) {
            List<IAsset> assets = api.Assets.GetManyInCategory(category, "");
            foreach (IAsset asset in assets) {
                try {
                    onLoaded(asset);
                }
                catch (Exception e) {
                    api.Logger.Error("Error loading asset " + asset.Location.ToString() + ". " + e.Message + "\n" + e.StackTrace);
                }
            }
            api.Logger.Event(assets.Count + " " + typeName + " loaded");
        }

        public override void StartServerSide(ICoreServerAPI api) {
            ServerAPI = api;
            api.Network.RegisterChannel("genelib")
                .RegisterMessageType<GenomeTypesMessage>();
        }

        public override void StartClientSide(ICoreClientAPI api) {
            ClientAPI = api;
            api.Network.RegisterChannel("genelib")
                .RegisterMessageType<GenomeTypesMessage>().SetMessageHandler<GenomeTypesMessage>(GenomeType.OnAssetsRecievedClient);

            api.Event.LevelFinalize += WaitForGenomeAssets;
        }

        public static void SendServerAssets_Postfix(ServerMain __instance, IServerPlayer player) {
            if (player?.ConnectionState == null) {
                return;
            }

            if (__instance.Clients.TryGetValue(player.ClientId, out var connectedClient) && connectedClient.IsSinglePlayerClient) {
                return;
            }
            GenomeTypesMessage message = new GenomeTypesMessage(GenomeType.loaded);

            ServerAPI.Network.GetChannel("genelib").SendPacket<GenomeTypesMessage>(message, player);
        }

        public static void WaitForGenomeAssets() {
            if (ClientAPI.IsSinglePlayer) {
                GenomeType.assetsReceived = true;
            }
            for (int tries = 0; tries < 10; ++tries) {
                for (int wait = 0; wait < 500; ++wait) {
                    if (GenomeType.assetsReceived) {
                        break;
                    }
                    Thread.Sleep(20);
                }
                if (!GenomeType.assetsReceived) {
                    ClientAPI.Logger.Warning("Genelib: Waiting on genome type assets took more than 10 seconds!");
                }
            }
            if (!GenomeType.assetsReceived) {
                throw new Exception("Connection failed: Genome type assets arrival timed out");
            }
        }
    }
}
