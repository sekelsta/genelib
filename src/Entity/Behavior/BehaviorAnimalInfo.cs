using Genelib.Extensions;
using Genelib.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
﻿using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace Genelib {
    public class BehaviorAnimalInfo : EntityBehaviorNameTag {
        public const string Code = "genelib.info";

        public string Note {
            get => entity.WatchedAttributes.GetTreeAttribute("nametag").GetString("note", "");
            set => entity.WatchedAttributes.GetTreeAttribute("nametag").SetString("note", value);
        }

        public BehaviorAnimalInfo(Entity entity) : base(entity) {
            if (!entity.WatchedAttributes.HasAttribute("UID")) {
                entity.WatchedAttributes.SetLong("UID", entity.EntityId);
            }
        }

        public override string GetName(ref EnumHandling handling) {
            // Unlike base method, don't set handling to prevent default
            return DisplayName;
        }

        public static bool ToggleAnimalInfoGUI(KeyCombination keyConbination) {
            foreach (GuiDialog dialog in GenelibSystem.ClientAPI!.Gui.OpenedGuis) {
                if (dialog is GuiDialogAnimal && dialog.IsOpened()) {
                    dialog.TryClose();
                    return true;
                }
            }

            EntityPlayer? player = (GenelibSystem.ClientAPI.World as ClientMain)?.EntityPlayer;
            EntitySelection? entitySelection = player?.EntitySelection;
            EntityAgent? agent = entitySelection?.Entity as EntityAgent;
            if (agent == null 
                    || !agent.Alive 
                    || agent.GetBehavior<BehaviorAnimalInfo>() == null 
                    || agent.Pos.SquareDistanceTo(player!.Pos.XYZ) > 20 * 20) {
                return false;
            }
            GuiDialogAnimal animalDialog = new GuiDialogAnimal(GenelibSystem.ClientAPI, agent);
            animalDialog.TryOpen();
            return true;
        }

        public static void OnSetNameMessageServer(IServerPlayer fromPlayer, SetNameMessage message) {
            Entity target = GenelibSystem.ServerAPI!.World.GetEntityById(message.entityId);
            EntityBehaviorNameTag? nametag = target.GetBehavior<EntityBehaviorNameTag>();
            if (nametag == null || target.OwnedByOther(fromPlayer)) {
                return;
            }
            string newName = message.name;
            target.Api.Logger.Audit(fromPlayer.PlayerName + " changed name of " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos
                + " from " + nametag.DisplayName + " to " + newName);
            nametag.SetName(newName);

            // Update stored parent name on all offspring
            long renamedUID = target.UniqueID();
            foreach (Entity entity in GenelibSystem.ServerAPI.World.LoadedEntities.Values) {
                if (entity.WatchedAttributes.GetLong("motherId", -1) == renamedUID) {
                    entity.WatchedAttributes.SetString("motherName", newName);
                }
                if (entity.WatchedAttributes.GetLong("fatherId", -1) == renamedUID) {
                    entity.WatchedAttributes.SetString("fatherName", newName);
                }
                if (entity.WatchedAttributes.GetLong("fosterId", -1) == renamedUID) {
                    entity.WatchedAttributes.SetString("fosterName", newName);
                }
            }
        }

        public static void OnSetNoteMessageServer(IServerPlayer fromPlayer, SetNoteMessage message) {
            Entity target = GenelibSystem.ServerAPI!.World.GetEntityById(message.entityId);
            BehaviorAnimalInfo? info = target.GetBehavior<BehaviorAnimalInfo>();
            if (info == null || target.OwnedByOther(fromPlayer)) {
                return;
            }
            target.Api.Logger.Audit(fromPlayer.PlayerName + " changed note of " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos 
                + " from " + info.Note + " to " + message.note);
            info.Note = message.note;
        }

        public static void OnToggleBreedingMessageServer(IServerPlayer fromPlayer, ToggleBreedingMessage message) {
            Entity target = GenelibSystem.ServerAPI!.World.GetEntityById(message.entityId);
            if (target.OwnedByOther(fromPlayer)) {
                return;
            }
            ITreeAttribute domestication = target.WatchedAttributes.GetTreeAttribute("domesticationstatus");
            if (domestication != null) {
                domestication.SetBool("multiplyAllowed", !message.preventBreeding);
            }
            else {
                target.WatchedAttributes.SetBool("preventBreeding", message.preventBreeding);
            }
            target.Api.Logger.Audit(fromPlayer.PlayerName + " set preventBreeding=" + message.preventBreeding + " for " + target.Code + " ID " + target.EntityId + " at " + target.Pos.XYZ.AsBlockPos);
        }
    }
}
