using System;

using Genelib.Network;
using Genelib.Extensions;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace Genelib {
    public class GuiDialogAnimal : GuiDialog {
        public EntityAgent Animal;
        protected int currentTab = 0;
        public int Width = 300;

        public delegate void AddToContents(GuiDialogAnimal gui, ref int y);
        public static AddToContents AddToStatusContents;
        public static AddToContents AddPreInfoContents;

        public GuiDialogAnimal(ICoreClientAPI capi, EntityAgent animal) : base(capi) {
            this.Animal = animal;
            ComposeGui();
        }

        protected void ComposeGui() {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-tab-status"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-tab-info"), DataInt = 1 },
            };
            ElementBounds tabBounds = ElementBounds.Fixed(0, -24, Width, 25);
            CairoFont tabFont = CairoFont.WhiteDetailText();

            string animalName = Animal.GetBehavior<EntityBehaviorNameTag>().DisplayName;
            SingleComposer = capi.Gui.CreateCompo("animaldialog-" + Animal.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("", () => { TryClose(); } )
                .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont, tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                .BeginChildElements(bgBounds);

            if (Animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                SingleComposer.AddStaticText(animalName, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, -14, 200, 22));
            }
            else {
                SingleComposer.AddTextInput(ElementBounds.Fixed(0, -14, 200, 22), OnNameSet, null, "animalName");
                SingleComposer.GetTextInput("animalName").SetValue(animalName);
            }

            SingleComposer.GetHorizontalTabs("tabs").activeElement = currentTab;
            if (currentTab == 0) {
                AddStatusContents();
            }
            else if (currentTab == 1) {
                AddInfoContents();
            }
            SingleComposer.EndChildElements().Compose();
        }

        private void OnNameSet(string name) {
            SetNameMessage message = new SetNameMessage() { entityId = Animal.EntityId, name = name };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<SetNameMessage>(message);
        }

        private void OnNoteSet(string note) {
            SetNoteMessage message = new SetNoteMessage() { entityId = Animal.EntityId, note = note };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<SetNoteMessage>(message);
        }

        private void OnPreventBreedingSet(bool value) {
            ToggleBreedingMessage message = new ToggleBreedingMessage() { entityId = Animal.EntityId, preventBreeding = value };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<ToggleBreedingMessage>(message);
        }

        protected void AddStatusContents() {
            int y = 25;
            if (!Animal.WatchedAttributes.GetBool("neutered", false)) {
                if (Animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                    if (!Animal.MatingAllowed()) {
                        SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-breedingprevented"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 25));
                        y += 25;
                    }
                    else {
                        // TODO: Less hacky fix for crashing if the composer has no contents.
                        SingleComposer.AddStaticText(" ", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 25));
                    }
                }
                else {
                    SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-preventbreeding"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
                    SingleComposer.AddSwitch(OnPreventBreedingSet, ElementBounds.Fixed(Width - 25, y - 5, 25, 25), "preventbreeding");
                    SingleComposer.GetSwitch("preventbreeding").SetValue(!Animal.MatingAllowed());
                    y += 25;
                }
            }

            if (AddToStatusContents != null) {
                AddToStatusContents(this, ref y);
            }
        }

        protected void AddInfoContents() {
            CairoFont infoFont = CairoFont.WhiteDetailText();
            int y = 20;

            if (AddPreInfoContents != null) {
                AddPreInfoContents(this, ref y);
            }

            string motherString = getParentName("mother");
            string fatherString = getParentName("father");
            SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-father", fatherString), infoFont, ElementBounds.Fixed(0, y, Width, 25));
            y += 25;
            SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-mother", motherString), infoFont, ElementBounds.Fixed(0, y, Width, 25));
            y += 25;
            if (Animal.WatchedAttributes.HasAttribute("fosterId")) {
                long fosterId = Animal.WatchedAttributes.GetLong("fosterId");
                if (fosterId != Animal.WatchedAttributes.GetLong("motherId", -1)) {
                    string fosterString = getParentName("foster"); // TO_OPTIMIZE: skip getting foster ID again
                    SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-foster", fosterString), infoFont, ElementBounds.Fixed(0, y, Width, 25));
                    y += 25;
                }
            }

            ITreeAttribute geneticsTree = Animal.WatchedAttributes.GetTreeAttribute("genetics");
            if (geneticsTree != null && geneticsTree.HasAttribute("coi")) {
                float coi = geneticsTree.GetFloat("coi");
                if (coi >= 0.0395) {
                    string coiText = Lang.Get("genelib:gui-animalinfo-inbreedingcoefficient", Math.Round(100 * coi));
                    SingleComposer.AddStaticText(coiText, infoFont, ElementBounds.Fixed(0, y, Width, 25));
                    string desc = Lang.Get("genelib:gui-animalinfo-inbreedingcoefficient-desc");
                    SingleComposer.AddAutoSizeHoverText(desc, CairoFont.WhiteDetailText(), 350, ElementBounds.Fixed(0, y, Width, 25), "hoverCOI");
                    y += 25;
                }
            }

            SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-note"), infoFont, ElementBounds.Fixed(0, y, Width, 25));
            y += 25;
            string note = Animal.GetBehavior<BehaviorAnimalInfo>().Note;
            if (Animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                SingleComposer.AddStaticText(note, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width - 20, 22));
            }
            else {
                SingleComposer.AddTextInput(ElementBounds.Fixed(0, y, Width - 20, 22), OnNoteSet, CairoFont.WhiteDetailText(), "note");
                SingleComposer.GetTextInput("note").SetValue(note);
            }
            y += 25;
        }

        // TO_OPTIMIZE: Consider calculating once on dialog open and caching the result, instead of recalculating every tick
        private string getParentName(string parent) {
            if (Animal.WatchedAttributes.HasAttribute(parent+"Id")) {
                if (Animal.WatchedAttributes.HasAttribute(parent+"Name")) {
                    return Animal.WatchedAttributes.GetString(parent+"Name");
                }
                if (Animal.WatchedAttributes.HasAttribute(parent+"Key")) {
                    return Lang.Get(Animal.WatchedAttributes.GetString(parent+"Key"));
                }
                if (parent == "foster") {
                    return Lang.Get("genelib:gui-animalinfo-unknownmother");
                }
                return Lang.Get("genelib:gui-animalinfo-unknown" + parent);
            }
            else if (parent == "mother" && Animal.WatchedAttributes.HasAttribute("fatherId")) {
                // For a time (until detailedanimals 0.3.2?) there was a bug where only fathers not mothers were being recorded
                return Lang.Get("genelib:gui-animalinfo-unknownmother");
            }
            else {
                return Lang.Get("genelib:gui-animalinfo-foundation");
            }
        }

        protected void OnTabClicked(int tab) {
            currentTab = tab;
            ComposeGui();
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
