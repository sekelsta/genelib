using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Genelib.Extensions {
    public static class EntityExtensions {
        public static long UniqueID(this Entity entity) {
            return entity.WatchedAttributes.GetLong("UID", entity.EntityId);
        }

        public static string GetDisplayName(this Entity entity) {
            string? name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (name == null || name == "") {
                return entity.GetName();
            }
            return name;
        }

        public static void SetFoster(this Entity entity, Entity foster) {
            if (foster == null) {
                // No clear reason why we'd want to remove it so just do nothing
                return;
            }

            long fosterId = foster.UniqueID();
            entity.WatchedAttributes.SetLong("fosterId", fosterId);
            long motherId = entity.WatchedAttributes.GetLong("motherId", -1);
            string prefix = "foster";
            if (motherId == fosterId) {
                prefix = "mother";
                entity.WatchedAttributes.RemoveAttribute("fosterName");
                entity.WatchedAttributes.RemoveAttribute("fosterKey");
            }
            string? motherName = foster.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (motherName != null && motherName != "") {
                entity.WatchedAttributes.SetString(prefix+"Name", motherName);
            }
            entity.WatchedAttributes.SetString(prefix+"Key", foster.Code.Domain + ":item-creature-" + foster.Code.Path);
        }

        public static bool IsMale(this Entity entity) {
            // Sometimes property attributes are null, so need to check
            if (entity.Properties.Attributes == null) {
                return false;
            }
            if (!entity.Properties.Attributes.KeyExists("male")) {
                JObject jo = (JObject) entity.Properties.Attributes.Token;
                jo.Add("male", !entity.Code.Path.Contains("-female"));
            }
            return entity.Properties.Attributes["male"].AsBool();
        }

        public static bool MatingAllowed(this Entity entity) {
            if (entity.WatchedAttributes.HasAttribute("domesticationstatus")) {
                if (!entity.WatchedAttributes.GetTreeAttribute("domesticationstatus").GetBool("multiplyAllowed", true)) {
                    return false;
                }
            }
            else if (entity.WatchedAttributes.GetBool("preventBreeding", false)) {
                return false;
            }
            return true;
        }

        public static bool IsCloseRelative(this Entity entity, Entity other) {
            long ourID = entity.UniqueID();
            long theirID = other.UniqueID();
            long ourMother = entity.WatchedAttributes.GetLong("motherId", -1);
            long ourFather = entity.WatchedAttributes.GetLong("fatherId", -1);
            long ourFoster = entity.WatchedAttributes.GetLong("fosterId", -1);
            long theirMother = other.WatchedAttributes.GetLong("motherId", -1);
            long theirFather = other.WatchedAttributes.GetLong("fatherId", -1);
            long theirFoster = other.WatchedAttributes.GetLong("fosterId", -1);

            bool isParent = ourID == theirMother || ourID == theirFather || ourID == theirFoster;
            bool isChild = theirID == ourMother || theirID == ourFather || theirID == ourFoster;
            // Skip sibling check for adoption
            bool sharesMother = ourMother != -1 && (ourMother == theirMother || ourMother == theirFather);
            bool sharesFather = ourFather != -1 && (ourFather == theirFather || ourFather == theirMother);
            return isParent || isChild || sharesMother || sharesFather;
        }

        public static bool OwnedBy(this Entity entity, IPlayer player) {
            return player != null && entity.WatchedAttributes.GetTreeAttribute("ownedby")?.GetString("uid") == player.PlayerUID;
        }

        public static bool OwnedByOther(this Entity entity, IPlayer? player) {
            string? ownerUID = entity.WatchedAttributes.GetTreeAttribute("ownedby")?.GetString("uid");
            return ownerUID != null && ownerUID != player?.PlayerUID;
        }
    }
}
