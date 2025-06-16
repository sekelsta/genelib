using Genelib.Extensions;
using Genelib.Network;
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
    public class JsonTweakingSystem : ModSystem
    {
        public override double ExecuteOrder() => 0.25;

        private void addToArray(ref JsonObject[] array, JsonObject toAdd) {
            JsonObject[] oldArray = array;
            array = new JsonObject[oldArray.Length + 1];
            Array.Copy(oldArray, array, oldArray.Length);
            array[array.Length - 1] = toAdd;
        }

        public override void AssetsLoaded(ICoreAPI api) {
            if (api.Side != EnumAppSide.Server) return;

            if (!GenelibSystem.AutoadjustAnimalBehaviors) return;
            EntityTagArray animalTag = api.TagRegistry.EntityTagsToTagArray("animal");
            JsonObject infoBehavior = JsonObject.FromJson("{code: \"" + BehaviorAnimalInfo.Code + "\", showtagonlywhentargeted: true}");
            foreach (EntityProperties entityType in api.World.EntityTypes) {
                if (!entityType.Tags.ContainsAll(animalTag)) continue;

                bool serverInfoBehavior = false;
                foreach (JsonObject jsonObject in entityType.Server.BehaviorsAsJsonObj) {
                    string code = jsonObject["code"].AsString();
                    if (code == BehaviorAnimalInfo.Code) {
                        serverInfoBehavior = true;
                        break;
                    }
                }
                if (!serverInfoBehavior) {
                    addToArray(ref entityType.Server.BehaviorsAsJsonObj, infoBehavior);
                }

                bool clientInfoBehavior = false;
                foreach (JsonObject jsonObject in entityType.Client.BehaviorsAsJsonObj) {
                    string code = jsonObject["code"].AsString();
                    if (code == BehaviorAnimalInfo.Code) {
                        clientInfoBehavior = true;
                        break;
                    }
                }
                if (!clientInfoBehavior) {
                    addToArray(ref entityType.Client.BehaviorsAsJsonObj, infoBehavior);
                }
            }
        }
    }
}
