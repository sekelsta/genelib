using Genelib.Extensions;
using Genelib.Network;
using HarmonyLib;
using Newtonsoft.Json.Linq;
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
        public static bool AutoadjustAnimalBehaviors = false;

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
            harmony.Patch(
                typeof(EntityBehaviorGrow).GetMethod("BecomeAdult", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(GenelibSystem).GetMethod("BecomeAdult_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(EntityBehaviorGrow).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public),
                postfix: new HarmonyMethod(typeof(GenelibSystem).GetMethod("Grow_Initialize_Postfix", BindingFlags.Static | BindingFlags.Public)) 
            );
            harmony.Patch(
                typeof(EntitySidedProperties).GetConstructor(BindingFlags.Instance | BindingFlags.Public, new[] { typeof(JsonObject[]), typeof(Dictionary<string, JsonObject>)}),
                prefix: new HarmonyMethod(typeof(GenelibSystem).GetMethod("EntitySidedProperties_Ctor_Prefix", BindingFlags.Static | BindingFlags.Public)) 
            );

            api.RegisterBlockEntityClass("Genelib.Nest", typeof(GeneticNest));

            api.RegisterItemClass("ItemGeneticEgg", typeof(ItemGeneticEgg));

            AiTaskRegistry.Register<AiTaskMate>("genelib.mate");

            api.RegisterEntityBehaviorClass(EntityBehaviorGenetics.Code, typeof(EntityBehaviorGenetics));
            api.RegisterEntityBehaviorClass(GeneticMultiply.Code, typeof(GeneticMultiply));
            api.RegisterEntityBehaviorClass(BehaviorAnimalInfo.Code, typeof(BehaviorAnimalInfo));

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
                .RegisterMessageType<SetNameMessage>().SetMessageHandler<SetNameMessage>(BehaviorAnimalInfo.OnSetNameMessageServer)
                .RegisterMessageType<SetNoteMessage>().SetMessageHandler<SetNoteMessage>(BehaviorAnimalInfo.OnSetNoteMessageServer)
                .RegisterMessageType<ToggleBreedingMessage>().SetMessageHandler<ToggleBreedingMessage>(BehaviorAnimalInfo.OnToggleBreedingMessageServer)
                .RegisterMessageType<GenomeTypesMessage>();
        }

        public override void StartClientSide(ICoreClientAPI api) {
            ClientAPI = api;
            api.Network.RegisterChannel("genelib")
                .RegisterMessageType<SetNameMessage>()
                .RegisterMessageType<SetNoteMessage>()
                .RegisterMessageType<ToggleBreedingMessage>()
                .RegisterMessageType<GenomeTypesMessage>().SetMessageHandler<GenomeTypesMessage>(GenomeType.OnAssetsRecievedClient);

            api.Event.LevelFinalize += WaitForGenomeAssets;

            api.Input.RegisterHotKey("genelib.info", Lang.Get("genelib:gui-hotkey-animalinfo"), GlKeys.N, type: HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("genelib.info", BehaviorAnimalInfo.ToggleAnimalInfoGUI);
        }

        public static bool BecomeAdult_Prefix(EntityBehaviorGrow __instance, Entity adult, bool keepTextureIndex) {
            Entity entity = __instance.entity;
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

            return true;
        }

        public static void Grow_Initialize_Postfix(EntityBehaviorGrow __instance, EntityProperties properties, JsonObject typeAttributes) {
            IGameCalendar calendar = __instance.entity.World.Calendar;
            if (typeAttributes.KeyExists("monthsToGrow")) {
                __instance.HoursToGrow = typeAttributes["monthsToGrow"].AsFloat() * calendar.DaysPerMonth * calendar.HoursPerDay;
            }
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

        public static bool EntitySidedProperties_Ctor_Prefix(EntitySidedProperties __instance, ref JsonObject[] behaviors, ref Dictionary<string, JsonObject> commonConfigs) {
            if (!AutoadjustAnimalBehaviors && (commonConfigs == null || !commonConfigs.ContainsKey(GeneticMultiply.Code))) {
                return true;
            }
            int multiplyIndex = -1;
            for (int i = 0; i < behaviors.Length; ++i) {
                string code = behaviors[i]["code"].AsString();
                if (code == "multiply") {
                    multiplyIndex = i;
                }
            }

            if (multiplyIndex != -1) {
                JObject multiplyJson = (JObject)(behaviors[multiplyIndex].Token);
                multiplyJson.Property("code").Value = new JValue(GeneticMultiply.Code);
            }

            // Might have to also merge multiply commonconfig with genelib.multiply's for future mod compat

            if (commonConfigs != null) {
                commonConfigs.Remove("multiply", out JsonObject multiplyConfig);
                if (multiplyConfig != null) {
                    commonConfigs.Add(GeneticMultiply.Code, multiplyConfig);
                }
            }

            return true;
        }
    }
}
