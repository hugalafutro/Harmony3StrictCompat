# Harmony 3 Strict Binding Compat (local)

Local stop-gap fixes for a large RimWorld 1.7 mod list (Harmony 3, UnityStubs). Every fix is temporary: remove it
once the mod it covers is fixed upstream.

## Fixes

Each C# fix fingerprints the foreign method it touches (FNV-1a of the IL). When that mod updates, the log shows a
red `[Harmony3StrictCompat] ... changed` error so the fix can be re-checked. XML fixes use PatchOperationTest steps
that fail loudly the same way.

| Mod | Problem | Fix |
|---|---|---|
| Auto-Cast Specialist Commands + Det's Xenotypes - Stoneborn | Personal tweak, not a fix: Auto-Cast only knows the command abilities by name, so the Stoneborn Stonecall (self-only +30% mining and construction speed) is never auto-cast | On a mine job or when finishing a construction frame, hands `DV_Stonecall` to Auto-Cast's own toil injector, gated by its "Auto-Cast Mining Command" setting; right-click on the Stonecall gizmo toggles that setting and Auto-Cast's check icon shows it, as on its own commands |
| Basic Traits | "TraitHediffApplier: Processing pawn" debug spam | Log calls removed, logic kept |
| Better Workbench Management | Its bill options (output filter, count away, restrict workbench) overlap the Ideology "Use global style" dropdown at the bottom of the bill dialog | Lifts BWM's block 50 px whenever Ideology is active and the recipe makes a thing (vanilla classic-mode dropdown and Style Change Anytime's dropdown both sit there) |
| Better Workbench Management | Personal tweak, not a fix: the previous/next bill arrows overlap the top of the ingredient filter by a few pixels | Draws them 7 px higher |
| Biomes! Caverns | `BMT_ResourceBlueCrystal` "is equipment but has no verbs or tools" config error | XML: strips its equipment nodes (`1.7/Patches/BiomesCaverns_BlueCrystal.xml`) |
| Biomes! Caverns (Caveworld Flora) | Wild fungus colonies never regrow: `Mycelium.NotifyPlantRemoved` is never called, so a colony that once filled up stays "full" with no mushrooms | Each wild mushroom removed lowers its colony's count (floored at 0, colony kept); on load every count is reset to the live mushrooms |
| Biomes! Caverns (Caveworld Flora) | Wild fungi mouseover reads `Brightbells)`, growth missing | Re-inserts ` (NN% growth` in `FruitingBody.LabelMouseover` |
| Busywork + LTO Colony Groups | With marker position "Upper" the job icon never shows on Colony Groups portraits: Busywork draws it only from a postfix on the vanilla colonist bar drawer, which LTO replaces | Runs Busywork's own portrait postfix from LTO's `DrawColonist` (icon in the portrait's top-left corner, as on the vanilla bar) |
| Common Sense | "Getting random element from empty collection" warnings, repeated on every stack split: "add meal ingredients" looks for a recipe for food that no recipe makes and never caches the miss | Skips that lookup for food no ingredient recipe produces |
| Common Sense | Eating job ends with an error (`CleanFilthToil` null-ref) when the filth a pawn cleans before eating vanishes first | Moves on to the next filth / the meal when the target is gone |
| Designator Shapes + Better Architect Menu | Flood fill (e.g. a floor) runs through doors and fills the next room: Shapes only treats things in the architect category named `Structure` as barriers, and BAM moves `DoorBase` to `Ferny_Doors` | Shapes' private `FloodFill.IsStructure` also answers true for doors (`Building_Door` subclasses and anything in `Ferny_Doors`); only Shapes' flood fill calls it |
| Eternal Pawns | Personal tweak, not a fix: the memory and notes gizmo disappears while a pawn is your prisoner, because `IsPawnSavable` rejects prisoners | Adds the same gizmo for colony prisoners; the veteran pool is unchanged. Use its Pin button or the note is cleared at the next cleanup unless the pawn is a veteran |
| Eternal Pawns | Recycles faction leaders and warcasket pawns as ordinary visitors/raiders (kind rewritten, casket lost); ignores `MustBeCapableOfViolence` | Keeps leaders, leader kinds and warcasket wearers out of the veteran draw; honours the violence flag |
| Eternal Pawns | An issued veteran stays a free world pawn, so vanilla redresses it again in the same group: duplicate pawns in a trade caravan, the trader turned into a guard kind, Giddy-Up gives one rider two mounts and the caravan never arrives (starves) | Issued veterans leave WorldPawns like vanilla-redressed pawns |
| Flourish - Everyday Animations | The telescope pose uses one fixed world offset and facing (`JobAnimationWorker_Anchored`), set up for the telescope's normal art, but vanilla draws that art mirrored when the telescope is rotated West, so there the pawn stands on the wrong side of the eyepiece | When the target is drawn mirrored (`Graphic.WestFlipped`/`EastFlipped` for its rotation), the worker reads the anchor with x negated and East/West facing swapped; the shared def is never written (render threads) |
| Flourish - Everyday Animations | Visual tweak: in the telescope pose the pawn's body covers the telescope (buildings always draw below pawns), so the hands hold nothing visible | While the anchored pose runs, the telescope is drawn a second time just above the pawn and below Flourish's hands (mesh only, no second shadow) |
| Giddy-Up 2 | Dismounted caravan animals park where the rider stepped off, so the caravan never arrives or leaves | Keeps lord animals' travel/follow focus across `Dismount` |
| Hauler's Dream + Compositable Loadouts | CL's 1.7 `Item` has no `Def` (items hold several alternative defs), so HD logs "loadout keep API ... did not fully resolve" and stops shielding loadout items from "adopt all surplus": possible unload / re-fetch loop | Turns HD's shield back on and counts every loadout item whose `Defs` include the def (HD's own startup warning still prints once) |
| Hauler's Dream + Kanban Stockpile Continue | HD ignores Kanban's max stack size: its bulk pick-up counts each slot at the full stack limit and its unload drops the whole carried stack, so vending machines capped at 25 held 248 cheese; Kanban's "remove excess" hauls the surplus out, HD hauls it back (churn, HD "net-zero" warnings) | While HD measures storage space, slots of capped storage count at the cap; HD's unload onto a capped cell drops only what the cell may still take, the rest goes to the next cell |
| Hospitality | Arrival re-gear destroys visitors' warcaskets | Skips the re-gear for casket wearers and re-locks their casket pieces |
| Hospitality | Arriving guests pocket their headgear, warcasket helmet included | Skips headgear pocketing for casket wearers |
| Hospitality | Guests with a claimed bed sleep on the floor after a save is loaded in the same session: `CompUtility.CompGuest` caches comps in a `Dictionary<Pawn, CompGuest>` that is never cleared, and 1.7's `Pawn.Equals` compares IDs, so reloaded pawns get the previous game's comp (stale bed and owner) | Clears that cache after `MemoryUtility.ClearAllMapsAndWorld` (every load / return to menu), logging how many entries it dropped |
| Hospitality | "threw exception in WorkGiver CookFillHopper" null-ref for guests helping out: Hospitality answers `InAllowedArea` from the guest area while the guest's own allowed areas stay empty, so vanilla's fail reason reads `.Label` on a null area for food outside the guest area | Transpiler on `HaulAIUtility.PawnCanAutomaticallyHaul` makes that label read null-safe |
| Hospitality: Casino | Casino only charges from its own drivers (poker, VFE roulette, slots), so tables that LocalTweaks gives its vending comp (tongo, billiards) would show the pricing gizmo but never earn | Charges once per job, on the first joy tick at any building with Casino's comp whose driver is not Casino's own (Casino's `InsertCoin`: other factions pay, colonists play free) |
| Hospitality: Casino | The pricing setting (free / low / default / high stake) only shows as the pricing gizmo's label, not in the inspect pane | Inspect pane gets a "Pricing:" line with the setting and the silver charged per game |
| Hospitality: Casino | Its slot-machine job giver sits above lord duties for every friendly visitor: the bestower left the bestowing ceremony to gamble, guards were pulled off shuttle boarding (shuttle left pawns behind or stayed with pawns inside), leaving trade caravans ran back across the map | Only casino visitors and trade caravans waiting at their chill spot get the slot-machine job |
| Hospitality: Vending machines | Personal tweak, not a fix: the powered vending machine should work as a fridge | Draws 100 W instead of 10 (`1.7/Patches/VendingMachine_Fridge.xml`); while powered, items in it get fridge temperature (ambient - 100, floored at -10 C, same as [sbz] Fridge), so food freezes |
| Hunt For Me | Released (wild) animals keep their hunting training and still run HFM's hunt job giver, which in supervised mode picks prey from the player's Hunt designations without excluding the hunter: a released hydra marked for hunting bit itself until downed | HFM's hunt job giver only runs for player-faction animals (vanilla never puts a Hunt designation on those) |
| Loading Progress | Thread race in its startup profiler (`ModInfoList.GetModInfoFor`): a mod's defs can fail to load ("same key has already been added"); on 2026-11-03 it dropped Anomaly, the game crashed and reset ModsConfig.xml | Serializes the call with a lock |
| Loadout Compositing + Better Workbench Management | BWM 1.7 Waylith build of 2026-11-03 is a Debug build; LC's transpiler no longer matches ("Error in transpiler ... off map"), so per-tag bills lose the "count pawns away / on caravan" option | Swaps BWM's `repeatMode == TargetCount` test for LC's own `ShouldCountAway` |
| Mod Settings Framework (VEF) | Slider settings only change the first def their xpath matches: ReBuild: Doors and Corners' grate speed slider fixed the metal grates but left the sterile grate tile at pathCost 10 (57% walk speed) | After the slider writes the first match, copies the value to every other match |
| More Phytokin Xenotypes | With Anomaly + Odyssey + Starjack, `Modules/Odyssey` shadows six `Modules/Anomaly` def files with the same relative path (last load folder wins), so carrionkin / harbingerkin defs never load and the mod crashes on them | Byte copies of the six files under other names in LocalTweaks `MorePhytokinXenotypes/` (must load after Core for `ParentName`), loaded only with all four mods; this mod compares them against the upstream files at startup, the XML patch there errors once any of those defs exists twice (upstream fixed, delete the copies). Same method as workshop fix 3830670122 |
| MSS Tweaks and Fun | Haunt portraits saved upside down on 1.7/Vulkan | Flips the rows before the PNG is encoded |
| MSS Tweaks and Fun | VFE Pirates Buckoneer culture patch targets `VFEP_Junkers/VFEP_Mercenaries` | Exact xpath remap to the `VFEP_Mercenaries` faction |
| Prisoner Realism + Cleaning Area Relaunched | Prisoners stand still in an endless Clean job loop when their room has filth inside Home but outside the Clean area: Prisoner Realism picks it, the Cleaning Area Relaunched driver skips it, the job ends at once and is handed out again | Prisoner cleaning only picks filth inside the Clean area |
| ReGrowth plant patches | Duplicate wild plant records abort the world-gen Landmarks / Mutators steps | Dedupes `BiomeDef.wildPlants` |
| RimPacts | A government in exile that leaves or is dismissed deletes the fallen faction's leader without a successor: "Faction leader for X is null" every session | A new leader is picked right away (and after load for any faction that should have one but doesn't), as vanilla does when a leader dies |
| Rimsenal Faction Pack - Spacer | Second style comp on the mech commander helmet, Dresser, EndTable and FloodLight (duplicate load IDs) | Those conditionals skipped, for that mod only |
| Sensible Bed Ownership | `OwnedBed` rescans every colonist bed on every call; UI and mods call it 200k-520k times a minute (Circinus: 2.3-5.1% of frame, single calls up to 79 ms with 150 beds) | Remembers SBO's answer per pawn while nothing it depends on changed (assign/unassign/claim methods, SBO's bed list, preferred bed, map, bed still owned; max 250 ticks); every 64th hit is re-checked against SBO and a mismatch is logged. Works from the parallel render threads too (Capture Expansion asks it for every pawn lying in a bed: 95 sleeping animals halved FPS), and SBO's bed list scan runs under a lock (it threw "Collection was modified" in pawn rendering) |
| Sensible Bed Ownership | Reservation prefix null-refs on just-despawned beds | Skipped for beds with no map |
| Show Mod Updates + UI Rethemed - Main Bar | Visual tweak: the "N updated mods" button in the themed Menu has no icon (UIR only knows the vanilla options) | Matches Show Mod Updates' label in the active language and gives it UIR's `arrow-clockwise` icon |
| Simple Leadership | Faction leaders join raids whose points can't cover them | Leaders only join when the raid points allow it |
| Skilled Refueling | `GetRefuelDuration` caches one iterator field from whichever refuel job runs first, so the other job type (normal or `atomicFueling`, e.g. Rimsenal - Security Pack) throws `ArgumentException`; atomic refuels also ignore refuel speed | Prefix refreshes the cache for the current job; atomic refuels take 240 ticks divided by the pawn's refuel speed |
| Smarter Visitors + Hospitality | Its prefix on `Trigger_TicksPassed.ActivateOn` runs for every ticks trigger of a visitor lord, not only Hospitality's stay timer: also vanilla's 1200-tick `Trigger_TicksPassedWithoutHarm` that sends a group back to travelling after a fight. With "Visitors will not leave if needing medical rest" on, a hurt visitor pushes that deadline out 2500 ticks every time, so a group hurt while leaving stands at its defend point eating and sleeping forever | The prefix only handles `Trigger_TicksPassedAndOkayToLeave`; every other ticks trigger runs vanilla |
| Smart Medicine - Continued | Personal tweak, not a fix: the Stock Up picker only lists drugs, medicine and the vanilla hemogen pack, so VRE Sanguophage's animal, corpse and sanguophage hemogen packs and carried meals (plain Foods) cannot be stocked without the dev-only "Stock ANYTHING" list | Postfix on the picker's filter lambda (`Dialog_StockUp.<DoWindowContents>b__10_0`) also admits `VRE_HemogenPack_Animal`, `VRE_HemogenPack_Corpse`, `VRE_HemogenPack_Sanguophage`, `MealSurvivalPack` and `MealNutrientPackaged` (Rimsenal Spacer) |
| Smart Medicine - Continued | Stocked food-type items such as hemogen packs (preferability `DesperateOnly`) are dropped by the idle `JobGiver_DropUnusedInventory` in the Home area once the pawn has not used raw inventory food for 150000 ticks, then fetched again: its keep check only covers the drug loop | Prefix on `JobGiver_DropUnusedInventory.Drop` keeps anything the pawn is stocking up on |
| Stem's VWE Industrial Expansion, Fortification Industrial - Citadel | "No Verse.SoundDef named ..." load warnings and silent weapons: `Interact_Pistol` (VFE Settlers only), `VWEE_Shot_SawnOffRepeater` and `VFE_Shot_InfernoSpewer` (defined nowhere) | XML: pistols use `Interact_Autopistol`, the sawn-off repeater `Shot_BoltActionRifle`, the flame bunker `InfernoCannon_Fire` (`1.7/Patches/MissingSoundDefs.xml`) |
| Too Many Mods | 22 lowercase `Defs/thingDef[` xpaths (Rimsenal items) | Case fixed |
| Triumphant Research | Blank research-complete window with a per-frame index error | Shows the project's full unlock list |
| UI Rethemed - Colonist Card | Personal tweak, not a fix: time to bleed out only shows at the bottom of the Health tab | Prefix on `CardInfo.Activity` puts vanilla's "Bleeding: 295%/d (death in 8 hours)" in front of the activity row (e.g. "Downed") when death is under a day away; none for Deathless pawns |
| UI Rethemed - Colonist Card + TD Enhancement Pack | Personal tweak, not a fix: TD's skill gain arrows only draw on the vanilla skill rows in the Bio tab | Postfix on `Glance.DrawSkill` runs TD's own `LabelLearning` over the right end of each card skill bar, so its up/down arrow settings and fade still apply |
| UI Rethemed - Inspector Tabs | Hover card rows with a value have a fixed height, so a long label (Vanilla Social Interactions Expanded's opinion thoughts are full sentences) overlaps its own value and the row below | A row that does not fit on one line wraps its label beside the value and grows; other rows keep UIR's drawing |
| UI Rethemed - Inspector Tabs | Gear tab vacuum resistance row flickers every rebuild between "Vacuum resist... 9..." and the full label and value, and its bar restarts its fill animation: the gauge measures its text widths in whatever font the last drawer left (its other gauges force Small first) but draws in Small | Forces Small around `GearTab.RefreshVacuumGauge`, restoring the previous font after; personal tweak on top: the row is relabelled "Vacuum res." so the bar keeps some width |
| UI Rethemed - Inspector Tabs + Full Armor - Hands and Feet | Gloves and boots never appear in the doll's Hands/Feet cells and hover as "Headgear": Full Armor moves every gloves/boots def to the Overhead layer (its `moveGlovesHeadgearLayer`), and UIR classifies by layer before body part, so they land in Extra | `GearTab.VanillaSlotFor`: an Overhead/EyeCover item covering hands or feet and no head, torso or leg group goes to the Hands/Feet cell |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the Gear tab overview orders its cards loadout, mass, stock-up, comfy temperature, combat | Transpiler on `GearTab.DrawStatsPanel` moves the combat card in front of the temperature card and the stock-up cards to the end: mass, combat, temperature, stock-up |
| UI Rethemed - Inspector Tabs (+ Packs Are Not Belts) | Personal tweak, not a fix: empty doll slot captions "Mechanitor", "Exoskeleton" and "Ammunition" (Tiny font) are too wide for the cell and wrap onto a second line | `GearTab.SlotLabel` returns "Mech.", "Exo." and "Ammo" for those layers |
| UI Rethemed - Main Bar (+ Heat Map) | Personal tweak, not a fix: the temperature readout only shows a hover card, and its face swallows every click, so it can no longer toggle the temperature overlay the way Heat Map's outdoor thermometer did | Left- or right-click on the temperature readout (text or tile) toggles the vanilla temperature overlay (`showTemperatureOverlay`, the flag Heat Map recolours and its key binding flips), with the checkbox on/off sounds |
| UI Rethemed - Notifications | Visual tweak: the game's transient top-left messages stay plain text outside UIR's notification column (its own `MessagesDoGUI` patch only shifts them sideways) | While a game is loaded and the column is bottom-right, draws each live message as a UIR `StatusRow` (the alert-row widget, UIR's own kind colours/icons) stacked above the alerts; left-click jumps / opens the quest, right-click dismisses with UIR's card click sound, hover highlights and shows UIR's own hover card beside the column at once, as its alerts do (header naming the message type, rule, full text) in the row's colour; messages live 30 s instead of 13 and a hovered one restarts its timer |
| Unburned + VEF | Drafted pawns holding reach melee weapons (halberd, pike, Mono lance) only auto-attack adjacent enemies: Unburned's prefix copies `JobDriver_Wait.CheckForAutoAttack` and skips the original, so VEF's reach transpiler on the original never runs | Transpiler on Unburned's prefix applies VEF's own `IsVanillaMeleeAttack` test in its copy |
| Useful Marks | Its two mark widgets in the rename dialog (label-in-world checkbox, colour square) sit at a fixed offset from the dialog bottom: beside the Title row when renaming a colonist, but on top of the last-name "Suggested..." button in the newborn naming dialog, which has no Title row | The widgets are only drawn when the dialog's bottom row is the Title row |
| Vanilla | Pawns deep-saved in quests are built before the world exists (MechEnergy null-ref) | No needs while no world exists; saved needs load right after |
| Vanilla | Shuttle passengers walk off and the shuttle never leaves: `CompShuttle.CompTick` evicts (every 600 ticks) any pawn inside whose `despawnedTick` is older than 60000 and drops it from its lord, but that field is the pawn's last map exit, not its boarding time. Bestowing-ceremony escorts are redressed world pawns who visited years earlier, so on the first check after landing some of them were thrown out of the escort lord (the loop also removes while indexing, so every other pawn), walked off as lordless non-colonists, and the shuttle then waited forever for them after the ceremony | Postfix on `CompTransporter.Notify_ThingAdded` sets a boarding pawn's `despawnedTick` to now, so the one-day eviction counts time in the shuttle; `CompShuttle.CompTick` is fingerprinted (game 1.7.5000) |
| Vanilla | Personal tweak, not a fix: install and implant-removal operations show a grey placeholder square in the operations menu (no `uiIconThing`, no product) | Fills the icon once at startup for surgery recipes without one: the implant item they consume, else the item the hediff drops |
| Vanilla (found through Modern Needs Tab) | `MinifiedThing.LabelNoCount` null-refs once its contents were installed, so the haul-to-frame job report throws; Modern Needs Tab then turns its job-history capture off for the session | An empty minified thing uses its def label |
| Vanilla / unknown | Trade caravan whose trader has a non-trader pawn kind: dismiss does nothing, caravan never exits | A guard holding a trader kind counts as the Trader |
| Vanilla / unknown | Trade ship slave also saved as a world pawn loads twice | World copy dropped after load |
| Vanilla Social Interactions Expanded (+ UI Rethemed - Inspector Tabs) | "Cured my friend" thought shows the raw `{SECONDPAWN_nameDef} cured my {0} ...` template with "Could not resolve symbol" errors when the patient is an implied relative (sibling, cousin, grandparent): the thought checks all relations, its label only direct ones. UIR then falls back to the vanilla Social tab until restart | Label built from the same tale with the vanilla most-important relation, gendered by the patient |
| Vanilla Races Expanded - Sanguophage | Animalfeeders (strigoi) never drink animal hemogen packs on their own, even with packs in their inventory: VRE only added an auto-drink job for corpse packs, vanilla only looks for regular packs | When vanilla finds no prisoner or regular pack, an animalfeeder drinks animal packs the vanilla way (carried, inventory, then nearest on the map) up to max hemogen |
| VEF | Verb range factor mutates the shared verb def and can stick (29.85 instead of 19.9) | Factor applied to the result instead |
| VEF (MVCF) | Multi-verb weapons get 100% accuracy and 1 s cooldown; duplicate stat rows; "Range 1" | Unset verb stats fall back to the weapon's own; duplicate rows hidden; Range row rebuilt |
| VFE Tribals + UI Rethemed - Factions | Visual tweak: the Cornerstones button keeps the vanilla button look in the themed Factions tab (UIR leaves room for it in the title bar but does not restyle it) | While the tab is themed, draws it with UIR's own action button (blue, campfire icon), sized to its label, and widens that title bar gap to fit; VFE's vanilla button otherwise |
| VTE Variations + Reel's Expanded Storage | "Choose graphic" / "Randomize graphic" change nothing on furniture that is also an Adaptive Storage building (wardrobe, royal dresser, tool cabinet, ...), which draw their GraphicsDef texture (base graphic hidden) and cache it, while VEF only swaps the base graphic | For Adaptive Storage defs with VEF's random-graphic comp, a GraphicsDef layer whose texture is one of the variants draws the chosen variant; picking one redraws the building |

Companion XML tweaks: [LocalTweaks](https://github.com/hugalafutro/LocalTweaks).

Build: `Tools/build.ps1` (compiles `Source/*.cs` against the game's DLLs into `1.7/Assemblies`).

<!-- MODLIST:START -->
## Active mod list

758 mods in load order, RimWorld 1.7.5000 rev1275. Generated from ModsConfig.xml by `Tools/modlist.py`.

| # | Mod | Package ID |
|---:|---|---|
| 1 | [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) | `brrainz.harmony` |
| 2 | [Load Them Last](https://steamcommunity.com/sharedfiles/filedetails/?id=3714307386) | `ferny.loadthemlast` |
| 3 | [Loading Progress](https://steamcommunity.com/sharedfiles/filedetails/?id=3535481557) | `ilyvion.loadingprogress` |
| 4 | [Faster Game Loading - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3652938473) | `taranchuk.fastergameloading` |
| 5 | [WOWGAG's Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3797541313) | `wowgag.guiperformancepatch` |
| 6 | [Harmony 3 Strict Binding Compat (local)](https://github.com/hugalafutro/Harmony3StrictCompat) | `sammael.harmony3strictcompat` |
| 7 | Core (official) | `ludeon.rimworld` |
| 8 | Royalty (official) | `ludeon.rimworld.royalty` |
| 9 | Ideology (official) | `ludeon.rimworld.ideology` |
| 10 | Biotech (official) | `ludeon.rimworld.biotech` |
| 11 | Anomaly (official) | `ludeon.rimworld.anomaly` |
| 12 | Odyssey (official) | `ludeon.rimworld.odyssey` |
| 13 | [Cherry Picker](https://steamcommunity.com/sharedfiles/filedetails/?id=3521312241) | `owlchemist.cherrypicker` |
| 14 | [Fortified Features Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3498575851) | `aoba.framework` |
| 15 | [Vanilla Backgrounds Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2775017012) | `vanillaexpanded.backgrounds` |
| 16 | [Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359) | `adaptive.storage.framework` |
| 17 | [XML Extensions](https://steamcommunity.com/sharedfiles/filedetails/?id=2574315206) | `imranfish.xmlextensions` |
| 18 | [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013) | `oskarpotocki.vanillafactionsexpanded.core` |
| 19 | [EBSG Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3112549163) | `ebsg.framework` |
| 20 | [[AV] Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3237129936) | `veltaris.framework` |
| 21 | [Biomes! Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3709492514) | `biomesteam.coreframework` |
| 22 | [Elite Bionics Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=1665403571) | `v1024.ebframework` |
| 23 | [1trickPwnyta's Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2764625248) | `ideologypatch.1trickpwnyta` |
| 24 | [[1.6] Do Not Avoid Fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3303359387) | `antihydrogen.donotavoidfire` |
| 25 | [[CAP] Thunder Snow](https://steamcommunity.com/sharedfiles/filedetails/?id=3590107592) | `cap.thundersnow` |
| 26 | [[DG]Lovly Animal Cuddling](https://steamcommunity.com/sharedfiles/filedetails/?id=3823442009) | `local.animalcuddling` |
| 27 | [[DR] Dream's Damage Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3789796309) | `mjcg.drdamageindicators` |
| 28 | [[DR] Where's My Treasure](https://steamcommunity.com/sharedfiles/filedetails/?id=3559591864) | `mjcg.wheresmytreasure` |
| 29 | [[LFS] UI Rethemed - Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=3831602102) | `lazyfridaystudio.uirethemed.animals` |
| 30 | [[LFS] UI Rethemed - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=3830054994) | `lazyfridaystudio.uirethemed.architect` |
| 31 | [[LFS] UI Rethemed - Colonist Card](https://steamcommunity.com/sharedfiles/filedetails/?id=3828206639) | `lazyfridaystudio.uirethemed.colonistcard` |
| 32 | [[LFS] UI Rethemed - Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3832843406) | `lazyfridaystudio.uirethemed.core` |
| 33 | [[LFS] UI Rethemed - History](https://steamcommunity.com/sharedfiles/filedetails/?id=3830774158) | `lazyfridaystudio.uirethemed.history` |
| 34 | [[LFS] UI Rethemed - Inspector Tabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3825029855) | `lazyfridaystudio.uirethemed.inspectortabs` |
| 35 | [[LFS] UI Rethemed - Main Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3826865706) | `lazyfridaystudio.uirethemed.mainbar` |
| 36 | [[LFS] UI Rethemed - Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3827650605) | `lazyfridaystudio.uirethemed.notifications` |
| 37 | [[LFS] UI Rethemed - Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3829391880) | `lazyfridaystudio.uirethemed.quests` |
| 38 | [[LFS] UI Rethemed - Wildlife](https://steamcommunity.com/sharedfiles/filedetails/?id=3828731834) | `lazyfridaystudio.uirethemed.wildlife` |
| 39 | [[LTO] Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=2345493945) | `derekbickley.ltocolonygroupsfinal` |
| 40 | [[NL] Facial Animation - WIP](https://steamcommunity.com/sharedfiles/filedetails/?id=1635901197) | `nals.facialanimation` |
| 41 | [Genetic Heads Framework for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3498759997) | `sd.fa.geneticheadsframework` |
| 42 | [[Og] Automatic Power Switch](https://steamcommunity.com/sharedfiles/filedetails/?id=3031634496) | `og.automaticpowerswitch` |
| 43 | [[Og] Repair Your Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3513376486) | `og.repair.your.gear` |
| 44 | [[sbz] Bookcase](https://steamcommunity.com/sharedfiles/filedetails/?id=3789304104) | `sbz.bookcase` |
| 45 | [[sbz] Fridge](https://steamcommunity.com/sharedfiles/filedetails/?id=3486264784) | `sbz.neatstoragefridge` |
| 46 | [[sbz] Gravship Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3537905298) | `sbz.gravshipstorage` |
| 47 | [[sbz] Neat Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3416243474) | `sbz.neatstorage` |
| 48 | [[sbz] Workbench Shelf](https://steamcommunity.com/sharedfiles/filedetails/?id=3484671309) | `sbz.neatstorageworkbenchshelf` |
| 49 | [[UR] Better Filter for Storage, Clothes and Food Restrictions](https://steamcommunity.com/sharedfiles/filedetails/?id=3753101034) | `uranv.betterfilter` |
| 50 | [[XND] Memorable Auroras (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2270039395) | `mlie.xndmemorableauroras` |
| 51 | [[XND] Visible Pants](https://steamcommunity.com/sharedfiles/filedetails/?id=2264108215) | `xeonovadan.visiblepants` |
| 52 | [Absolute Legends: Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=2195744587) | `absolute.legends.hair` |
| 53 | [Adaptive Primitive Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3400037215) | `adaptive.primitivestorage` |
| 54 | [AdaptiveMealBill](https://steamcommunity.com/sharedfiles/filedetails/?id=3668761367) | `rabiosus.adaptivemealbill` |
| 55 | [Additional Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3692484664) | `ferny.additionalherobackgrounds` |
| 56 | [Advanced Battle Cloak](https://steamcommunity.com/sharedfiles/filedetails/?id=3598135811) | `darjeeling.battlecloak` |
| 57 | [AFU男士发型_Men's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2072106893) | `jintuzilamian.hairstyle.afumenshairstyles` |
| 58 | [Against all odds](https://steamcommunity.com/sharedfiles/filedetails/?id=3552336221) | `literallyfree.colonistreturnbuff` |
| 59 | [Alfred Ancient - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3579763743) | `ferny.alfredancient` |
| 60 | [All Ideoligions Are Fluid](https://steamcommunity.com/sharedfiles/filedetails/?id=2872901762) | `superniquito.allideoligionsarefluid` |
| 61 | [Allies Fight the Fleshmass Heart](https://steamcommunity.com/sharedfiles/filedetails/?id=3772519386) | `lg.fleshmassalliedtarget` |
| 62 | [Almost There! Fork](https://steamcommunity.com/sharedfiles/filedetails/?id=3515165298) | `duz.almosttherefork` |
| 63 | [Alpha Armoury](https://steamcommunity.com/sharedfiles/filedetails/?id=3622294464) | `sarg.alphaarmoury` |
| 64 | [Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677) | `sarg.alphabiomes` |
| 65 | [Alpha Implants](https://steamcommunity.com/sharedfiles/filedetails/?id=3275091331) | `sarg.alphaimplants` |
| 66 | [Alpha Memes](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814) | `sarg.alphamemes` |
| 67 | [Alpha Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021) | `sarg.alphaprefabs` |
| 68 | [Alpha Random](https://steamcommunity.com/sharedfiles/filedetails/?id=3642631969) | `sarg.alpharandom` |
| 69 | [Always Position Gravship on Landing](https://steamcommunity.com/sharedfiles/filedetails/?id=3738322550) | `ferny.alwayspositiongravshiponlanding` |
| 70 | [Always Start in Daytime](https://steamcommunity.com/sharedfiles/filedetails/?id=3323971974) | `ferny.alwaysstartindaytime` |
| 71 | [Amorphous style pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3382499324) | `xmb.amorphousstylepack.mo` |
| 72 | [ANDH - Animals Nuzzling Detects Horrors](https://steamcommunity.com/sharedfiles/filedetails/?id=3230195082) | `latta.petknowstrueyou` |
| 73 | [Animal Collab Project Vanilla-Style](https://steamcommunity.com/sharedfiles/filedetails/?id=2208467668) | `acpteam.acpvanillastyle` |
| 74 | [Animals Forage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667046888) | `mlie.animalsforage` |
| 75 | [Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1195427067) | `com.bymarcin.architecticons` |
| 76 | [Armors For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3787547731) | `bamba.allbambaarmors` |
| 77 | [Asteroid Mineral Scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3536585361) | `broms.asteroidmineralscanner` |
| 78 | [Auto-assign Inventories on Caravan arrival](https://steamcommunity.com/sharedfiles/filedetails/?id=2882784350) | `syrus.autoassigninventorycaravan` |
| 79 | [Autoclose Event Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3226920093) | `dingo.autocloseeventnotifications` |
| 80 | [AutoExtractGenes](https://steamcommunity.com/sharedfiles/filedetails/?id=2882834449) | `nibato.autoextractgenes` |
| 81 | [Automatic Foraging](https://steamcommunity.com/sharedfiles/filedetails/?id=3687750532) | `i.automaticforaging` |
| 82 | [Automatic Night Owl](https://steamcommunity.com/sharedfiles/filedetails/?id=2056012179) | `ben.automaticnightowl` |
| 83 | [Barbs And Moats](https://steamcommunity.com/sharedfiles/filedetails/?id=3601561731) | `sgtbruce.barbsandmoats` |
| 84 | [Basic Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3600443734) | `stefanlog.basictraits` |
| 85 | [Battery Capacity in Stat View](https://steamcommunity.com/sharedfiles/filedetails/?id=3651439731) | `linnun.batterycapacity` |
| 86 | [Bed Rest For Food Poisoning](https://steamcommunity.com/sharedfiles/filedetails/?id=2567901366) | `bedrestforfoodpoisoning.1trickponyta` |
| 87 | [Better Abandoned Settlements](https://steamcommunity.com/sharedfiles/filedetails/?id=3826665236) | `edybtt.betterabandonedsettlements` |
| 88 | [Better Beggars (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3006899215) | `mlie.betterbeggars` |
| 89 | [Better Context Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3696948139) | `mrk.bettercontextmenu` |
| 90 | [Better Distress Call](https://steamcommunity.com/sharedfiles/filedetails/?id=3305489753) | `nalzurin.betterdistresscall` |
| 91 | [Better Gene Inheritance](https://steamcommunity.com/sharedfiles/filedetails/?id=3046776238) | `redmattis.bettergeneinheritance` |
| 92 | [Better ground-penetrating scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3203274131) | `kikohi.bettergroundpenetratingscanner` |
| 93 | [Better Growth Moments](https://steamcommunity.com/sharedfiles/filedetails/?id=3642805464) | `arkymn.bettergrowthmoments` |
| 94 | [Better Linkable Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3577372705) | `ganja.betterlinkablecolors` |
| 95 | [Better Mechanoid Sorting](https://steamcommunity.com/sharedfiles/filedetails/?id=3730840942) | `redstonepanda.bettermechrank` |
| 96 | [Better Mod Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3636406008) | `mumei.bettermodmenu` |
| 97 | [Better Records Tab (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2359778562) | `mlie.betterrecordstab` |
| 98 | [Better Scroll Bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3579237751) | `dillyo.betterscrollbars` |
| 99 | [Better Traders Guild](https://steamcommunity.com/sharedfiles/filedetails/?id=3684587591) | `shunter.bettertradersguild` |
| 100 | [Better Trees](https://steamcommunity.com/sharedfiles/filedetails/?id=3539609975) | `chaoticenrico.bettertrees` |
| 101 | [Better Workbench Management - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819555998) | `falconne.bwm` |
| 102 | [Big Bed Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=3201556342) | `amch.bigbedbuff` |
| 103 | [Biosculpter Pod and Neural Supercharger Resize](https://steamcommunity.com/sharedfiles/filedetails/?id=2710513531) | `syrus.bpansresize` |
| 104 | [Birth Quality Lifespan Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3654799710) | `saltgin.birthqualitylifespanfix` |
| 105 | [Blindsight - Unified](https://steamcommunity.com/sharedfiles/filedetails/?id=3248249029) | `spencezilla.blindsightunified` |
| 106 | [Blindsight suffers no Unnatural Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=3258096951) | `emitbreaker.blindnessunnaturaldarkness` |
| 107 | [Block Unwanted Minutiae (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3278213153) | `mlie.blockunwantedminutiae` |
| 108 | [Blueprints - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819556044) | `nova.blueprints.waylith17` |
| 109 | [BuilderTweaksReplica](https://steamcommunity.com/sharedfiles/filedetails/?id=3521096349) | `rabiosus.buildertweaksreplica` |
| 110 | [Caravan Mood Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=2680751877) | `syrus.caravanmoodbuff` |
| 111 | [Change map edge limit](https://steamcommunity.com/sharedfiles/filedetails/?id=1546494565) | `kapitanoczywisty.changemapedge` |
| 112 | [Chester Choose - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3761449697) | `ferny.chesterchoose` |
| 113 | [Childhood Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3000770956) | `zylle.childboodbackstories` |
| 114 | [Choose Where To Land](https://steamcommunity.com/sharedfiles/filedetails/?id=3537970831) | `kearril.choosewheretoland` |
| 115 | [Chronos Pointer - Schedule Menu Enhancement](https://steamcommunity.com/sharedfiles/filedetails/?id=3408065390) | `coolnether123.chronospointer` |
| 116 | [Circinus Dev Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3771602203) | `astryl.circinusdevtools` |
| 117 | [Cleaning Area Relaunched](https://steamcommunity.com/sharedfiles/filedetails/?id=3586697885) | `cerule.cleaningarearelaunched` |
| 118 | [Colored deep resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3203269467) | `kikohi.coloreddeepresources` |
| 119 | [Common Sense](https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193) | `avilmask.commonsense` |
| 120 | [Compositable Loadouts](https://steamcommunity.com/sharedfiles/filedetails/?id=2679126859) | `wiri.compositableloadouts` |
| 121 | [Consciousness Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2913127696) | `vex.consciousness` |
| 122 | [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079) | `phoenix.continuebutton` |
| 123 | [Cook for Yourself](https://steamcommunity.com/sharedfiles/filedetails/?id=3766233150) | `lordfelix.cookforyourself` |
| 124 | [Corpse Description Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3639910228) | `ferny.corpsedescriptionfix` |
| 125 | [Corpse info](https://steamcommunity.com/sharedfiles/filedetails/?id=2913588675) | `arl85.corpseinfo` |
| 126 | [D12 Dungeon and Night Hairs HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3620959277) | `d12caedro.dynhairshd` |
| 127 | [Dark Psychic Rituals: The Following](https://steamcommunity.com/sharedfiles/filedetails/?id=3596468709) | `dprtf.darkpsychicrituals.sentinel` |
| 128 | [Deathrest Pauses Biological Age](https://steamcommunity.com/sharedfiles/filedetails/?id=2940569974) | `crykosity.deathrest` |
| 129 | [Default Ingredient Radius](https://steamcommunity.com/sharedfiles/filedetails/?id=2909706985) | `mlie.defaultingredientradius` |
| 130 | [Deselect Zone](https://steamcommunity.com/sharedfiles/filedetails/?id=3328706359) | `cheesex.deselectzone` |
| 131 | [Designations Tooltip](https://steamcommunity.com/sharedfiles/filedetails/?id=3743959668) | `cheatereater.designationstooltip` |
| 132 | [Designator Shapes](https://steamcommunity.com/sharedfiles/filedetails/?id=1235181370) | `merthsoft.designatorshapes` |
| 133 | [Det's Xenotypes - Avaloi](https://steamcommunity.com/sharedfiles/filedetails/?id=3199692643) | `det.avaloi` |
| 134 | [Det's Xenotypes - Brawnum](https://steamcommunity.com/sharedfiles/filedetails/?id=3429572581) | `det.brawnum` |
| 135 | [Det's Xenotypes - Boglegs](https://steamcommunity.com/sharedfiles/filedetails/?id=3146564944) | `det.boglegs` |
| 136 | [Det's Xenotypes - Buzzers](https://steamcommunity.com/sharedfiles/filedetails/?id=3545293786) | `det.buzzers` |
| 137 | [Det's Xenotypes - Half-foot](https://steamcommunity.com/sharedfiles/filedetails/?id=3530817307) | `det.halffoot` |
| 138 | [Det's Xenotypes - Keshig](https://steamcommunity.com/sharedfiles/filedetails/?id=3376864722) | `det.keshig` |
| 139 | [Det's Xenotypes - Stoneborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2888722722) | `det.stoneborn` |
| 140 | [Det's Xenotypes - Venators](https://steamcommunity.com/sharedfiles/filedetails/?id=3140248688) | `det.venators` |
| 141 | [Dignified Empire Names](https://steamcommunity.com/sharedfiles/filedetails/?id=3530589531) | `goosechoosesviolence.empirenames` |
| 142 | [Dlc collaboration - Void universe](https://steamcommunity.com/sharedfiles/filedetails/?id=3587277884) | `hailuan.voiduniverse` |
| 143 | [Don't Mix Cosmetic Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3621282829) | `wara.dontmixcosmeticgenes` |
| 144 | [Don't Say 100% Unless You Mean It!](https://steamcommunity.com/sharedfiles/filedetails/?id=2894636032) | `kathanon.donemeansdone` |
| 145 | [DragSelect](https://steamcommunity.com/sharedfiles/filedetails/?id=2599942235) | `telardo.dragselect` |
| 146 | [Dress for the Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607724833) | `mlie.dressfortheweather` |
| 147 | [Dubs Apparel Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2296697286) | `dubwise.dubsappareltweaks` |
| 148 | [Dune Style Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2993301551) | `dsp.halituisamaricanous` |
| 149 | [Dynamic Carrying Capacity (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2967228692) | `mlie.dynamiccarryingcapacity` |
| 150 | [Dynamic Effects Forge](https://steamcommunity.com/sharedfiles/filedetails/?id=3735443951) | `blues.forge` |
| 151 | [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506) | `zeracronius.dynamictradeinterface` |
| 152 | [ED-Embrasures (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3277482616) | `ganja.ed.embrasures` |
| 153 | [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092) | `mnmr.eprimereadouts` |
| 154 | [Erin Eccentric](https://steamcommunity.com/sharedfiles/filedetails/?id=2967829336) | `erin.storyteller` |
| 155 | [Erin's Baldur's Gate 3 Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3069933015) | `erin.bg3.hair` |
| 156 | [Erin's Hairstyles - Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=2361911135) | `erin.hairredux` |
| 157 | [Erin's Hairstyles 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2849477421) | `erin.hair2` |
| 158 | [Eternal Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669591999) | `helldan.eternalpawns` |
| 159 | [Evolving Enemy Strongholds](https://steamcommunity.com/sharedfiles/filedetails/?id=3738072776) | `wowgag.evolvingenemystrongholds` |
| 160 | [Expressive Appendages](https://steamcommunity.com/sharedfiles/filedetails/?id=3830976421) | `remi.expressiveappendages` |
| 161 | [Extra Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=1926923167) | `zylle.extraalerts` |
| 162 | [Faction Raid Cooldown (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3547098393) | `mlie.factionraidcooldown` |
| 163 | [Family Tree (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3828392274) | `sarah.familytree.continued` |
| 164 | [Features Gap Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3753339482) | `ferny.featuresgapfix` |
| 165 | [Fire At Will Gizmo - Evolving Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3770020686) | `zohryu.fireatwillgizmo.evolving` |
| 166 | [FirmamentFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3781872865) | `dorbo.firmamentfx` |
| 167 | [Fix Styled Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2957953663) | `kathanon.fixstyledblueprints` |
| 168 | [Float Sub-Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2864015430) | `kathanon.floatsubmenu` |
| 169 | [Flourish - Combat Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3793154978) | `astryl.flourish` |
| 170 | [FMOD Errors Begone](https://steamcommunity.com/sharedfiles/filedetails/?id=3799402443) | `zzz.fmoderrorsbegone` |
| 171 | [Food Poisoning Investigator](https://steamcommunity.com/sharedfiles/filedetails/?id=3697891858) | `leo.foodpoisoninginvestigator` |
| 172 | [Forgetful Mech Resurrection](https://steamcommunity.com/sharedfiles/filedetails/?id=3766269434) | `mrhydralisk.forgetfulmechresurrection` |
| 173 | [Fortifications - Medieval - Material](https://steamcommunity.com/sharedfiles/filedetails/?id=2501487160) | `aoba.fortress.medieval.material` |
| 174 | [Fortifications - Neolithic](https://steamcommunity.com/sharedfiles/filedetails/?id=2385960678) | `aoba.fortress.neolithic` |
| 175 | [Friendly Fire Tweaks (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3591533573) | `zal.friendlyfiretweaks` |
| 176 | [Full Armor - Hands and Feet [1.6]](https://steamcommunity.com/sharedfiles/filedetails/?id=3530977354) | `stevezero.fullarmorhandsfeet` |
| 177 | [Gene Bank Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3265067908) | `leoltron.genebankindicators` |
| 178 | [Geological Landforms](https://steamcommunity.com/sharedfiles/filedetails/?id=2773943594) | `m00nl1ght.geologicallandforms` |
| 179 | [Ghouls Prefer Twisted Meat](https://steamcommunity.com/sharedfiles/filedetails/?id=3230171961) | `doug.ghoulsprefertwistedmeat` |
| 180 | [Giddy-Up 2 - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861) | `memegoddess.giddyup` |
| 181 | [Glasma's Refugee Expectations](https://steamcommunity.com/sharedfiles/filedetails/?id=3785120443) | `glasma.refugeeexpectations` |
| 182 | [Go Explore!](https://steamcommunity.com/sharedfiles/filedetails/?id=1814100216) | `albion.goexplore` |
| 183 | [Gojis-Misc-Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3775114861) | `goji.gojismiscgenes` |
| 184 | [Goodwill Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=3695225114) | `bluebird.goodwillpreview` |
| 185 | [Gore Perfected](https://steamcommunity.com/sharedfiles/filedetails/?id=3814447022) | `tiepi.goreperfected` |
| 186 | [Gravship Crew Manifest Saver](https://steamcommunity.com/sharedfiles/filedetails/?id=3641927599) | `lornath.gravshipcrewmanifestsaver` |
| 187 | [Grenade Fix - Rearmed](https://steamcommunity.com/sharedfiles/filedetails/?id=797360572) | `dingo.grenadefixrearmed` |
| 188 | [Guns For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3782391721) | `bambaryla.allbambaguns` |
| 189 | [Hard Times: Hair and Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3092175321) | `botchjob.hthair` |
| 190 | [Hauler's Dream](https://steamcommunity.com/sharedfiles/filedetails/?id=3742459652) | `giwaffed.haulersdream` |
| 191 | [Heat Map (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2552838384) | `syrus.heatmap` |
| 192 | [Hive Protection](https://steamcommunity.com/sharedfiles/filedetails/?id=3536627722) | `jaxe.hiveprotection` |
| 193 | [Hunt for Me](https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720) | `arandomkiwi.huntforme` |
| 194 | [I Clearly Have Enough! (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2023661266) | `mlie.iclearlyhaveenough` |
| 195 | [I'm Off (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3251510002) | `zal.imoff` |
| 196 | [I'm Trying To Save You! (Pawn Dressing Rework)](https://steamcommunity.com/sharedfiles/filedetails/?id=3534863993) | `s4.imtryingtosaveyou` |
| 197 | [Ignore Idle Guests](https://steamcommunity.com/sharedfiles/filedetails/?id=2515048728) | `mlie.ignoreidleguests` |
| 198 | [ilyvion's Laboratory](https://steamcommunity.com/sharedfiles/filedetails/?id=3296362231) | `ilyvion.laboratory` |
| 199 | [Infestations Spawn in Darkness 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3551839891) | `koth.isid` |
| 200 | [Injured Carry](https://steamcommunity.com/sharedfiles/filedetails/?id=2413690575) | `haecriver.injuredcarry` |
| 201 | [Inspired Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2851390190) | `lm.inspiredconversion` |
| 202 | [Instant Caravan](https://steamcommunity.com/sharedfiles/filedetails/?id=3165744287) | `ferny.easycaravanformation` |
| 203 | [Integrated Creep Joiners](https://steamcommunity.com/sharedfiles/filedetails/?id=3233429182) | `grillmaster.integratedcreepjoiners` |
| 204 | [Interaction Bubbles](https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345) | `jaxe.bubbles` |
| 205 | [Ion Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532877485) | `zal.ionweaponry` |
| 206 | [IVF Equality](https://steamcommunity.com/sharedfiles/filedetails/?id=2922603310) | `kathanon.ivfequality` |
| 207 | [Just Ignore Me Passing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503627342) | `mlie.justignoremepassing` |
| 208 | [Just Put It Over There](https://steamcommunity.com/sharedfiles/filedetails/?id=2856471776) | `mlie.justputitoverthere` |
| 209 | [KanbanStockpileContinue](https://steamcommunity.com/sharedfiles/filedetails/?id=3776851378) | `igora.kanbanstockpilecontinue` |
| 210 | [Keep Converting](https://steamcommunity.com/sharedfiles/filedetails/?id=3461478214) | `linnun.keepconverting` |
| 211 | [Keyz Misc Resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3355560776) | `keyz182.keyzmiscresources` |
| 212 | [Keyz' Allow Utilities](https://steamcommunity.com/sharedfiles/filedetails/?id=3524716849) | `keyz182.keyzallowutilities` |
| 213 | [Kidnapped Pawns Die Less](https://steamcommunity.com/sharedfiles/filedetails/?id=3308076464) | `conit.kpdl` |
| 214 | [Knick Knacks - Let your colonists decorate!](https://steamcommunity.com/sharedfiles/filedetails/?id=3595196942) | `vaguelysexual.modpackageid.goeshere` |
| 215 | [Large Faction Bases (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3257781909) | `mlie.largefactionbases` |
| 216 | [Last Visited (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3507014727) | `zal.lastvisited` |
| 217 | [Leo - Events](https://steamcommunity.com/sharedfiles/filedetails/?id=3730168769) | `leo.eventsexpanded` |
| 218 | [Less Stupid Romance Attempt (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014603702) | `mlie.lessstupidromanceattempt` |
| 219 | [Letter Permanent Injury](https://steamcommunity.com/sharedfiles/filedetails/?id=2592535960) | `buggy.rimworld.letterpermanentinjury` |
| 220 | [Level Up!](https://steamcommunity.com/sharedfiles/filedetails/?id=1701592470) | `krafs.levelup` |
| 221 | [Linkables Info Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3212476920) | `deadmano.linkablesinfoexpanded` |
| 222 | [LiquidSFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3758773902) | `dorbo.watersfx` |
| 223 | [Little Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=3331281387) | `tyrannidae.littlecritters` |
| 224 | [Living Bonds](https://steamcommunity.com/sharedfiles/filedetails/?id=3708989445) | `bsandyh.livingbonds` |
| 225 | [Load in Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379) | `ferny.loadinbackground` |
| 226 | [Log Publisher from HugsLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2873415404) | `m00nl1ght.unofficialupdates.hugslogpublisher` |
| 227 | [Map Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=2800857642) | `m00nl1ght.mappreview` |
| 228 | [Mark of Manhood](https://steamcommunity.com/sharedfiles/filedetails/?id=3646943505) | `maux36.markofmanhood` |
| 229 | [Mech Recharger Standby Mode](https://steamcommunity.com/sharedfiles/filedetails/?id=2879340506) | `heremeus.mechrechargerstandby` |
| 230 | [Mechanoids dont beat fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3252095758) | `fxz.mechbeatfire` |
| 231 | [Megafauna](https://steamcommunity.com/sharedfiles/filedetails/?id=1055485938) | `spino.megafauna` |
| 232 | [Melee Weapons For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3726524693) | `bamba.allbambamelee.tiered` |
| 233 | [Milky Way](https://steamcommunity.com/sharedfiles/filedetails/?id=3773448562) | `andromeda.milkyway` |
| 234 | [Mine Sight](https://steamcommunity.com/sharedfiles/filedetails/?id=3769804600) | `rabiosus.minesight` |
| 235 | [Mo'Events (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2035143365) | `mlie.moevents` |
| 236 | [Mojave Mask - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3781130446) | `ferny.mojavemask` |
| 237 | [Mono Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2997812588) | `cat.monopack` |
| 238 | [More Childhood Nicknames](https://steamcommunity.com/sharedfiles/filedetails/?id=3688519717) | `river.childhoodnames` |
| 239 | [More Persona Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2863308112) | `arquebus.morepersonatraits` |
| 240 | [More Random Seeds](https://steamcommunity.com/sharedfiles/filedetails/?id=1501463043) | `jaxe.morerandomseeds` |
| 241 | [More Slavery Stuff](https://steamcommunity.com/sharedfiles/filedetails/?id=2896845138) | `garryflowers.moreslaverystuff` |
| 242 | [More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1931453053) | `zylle.morevanillabiomes` |
| 243 | [MutatorWorldIcons](https://steamcommunity.com/sharedfiles/filedetails/?id=3551904193) | `snobi.mutatorworldicons` |
| 244 | [Name Your Entities](https://steamcommunity.com/sharedfiles/filedetails/?id=3229905841) | `ferny.nameyourentities` |
| 245 | [Nice Short Female Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3316363751) | `andromeda.nicehairs` |
| 246 | [No Backer Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669069738) | `nobackerpawns.nobackerpawns` |
| 247 | [No Disabled Factions In Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=2892125637) | `kathanon.nodisabledfactions` |
| 248 | [No Hemogen Farm Medical Alert](https://steamcommunity.com/sharedfiles/filedetails/?id=2887053053) | `dark.nohemogenfarmmedicalalert` |
| 249 | [No Infestation On Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=3661608882) | `ialways1ose.noinfestationonbuildings` |
| 250 | [No Job Authors](https://steamcommunity.com/sharedfiles/filedetails/?id=2009825774) | `doug.nojobauthors` |
| 251 | [No Modlist on Loading](https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405) | `ferny.nomodlistonloading` |
| 252 | [No Summon Mech Threat Gizmo](https://steamcommunity.com/sharedfiles/filedetails/?id=2889317343) | `canon.nomechsummonergizmo` |
| 253 | [No Terrain Edge Fade (Hard Floors)](https://steamcommunity.com/sharedfiles/filedetails/?id=3753895045) | `fauxzyi.noterrainedgefade` |
| 254 | [No Version Warning](https://steamcommunity.com/sharedfiles/filedetails/?id=2599504692) | `mlie.noversionwarning` |
| 255 | [Nomad Friendly Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3616175831) | `jaeger972.nomadfreindlyquests` |
| 256 | [Non uno Pinata (don't drop items)](https://steamcommunity.com/sharedfiles/filedetails/?id=1778821244) | `avilmask.nonunopinata` |
| 257 | [Not My Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2870045856) | `vesper.notmyfault` |
| 258 | [Not Overhead Meal](https://steamcommunity.com/sharedfiles/filedetails/?id=2986525461) | `overheadmeal.patch` |
| 259 | [Now Playing](https://steamcommunity.com/sharedfiles/filedetails/?id=3754684545) | `dorbo.nowplaying` |
| 260 | [OgreStack](https://steamcommunity.com/sharedfiles/filedetails/?id=1447140290) | `ogre.ogrestack` |
| 261 | [Orca Shuttle](https://steamcommunity.com/sharedfiles/filedetails/?id=3528001139) | `smallmine.heavyshuttle` |
| 262 | [Packs Are Not Belts](https://steamcommunity.com/sharedfiles/filedetails/?id=2509511179) | `turnovus.submod.backpacksarenotbelts` |
| 263 | [Pawn Editor Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113) | `segaswolf.pawneditor.fork` |
| 264 | [Pawn Name Variety (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3548290568) | `mlie.pawnnamevariety` |
| 265 | [Peer Pressure (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3605155621) | `densevoid.peerpressure` |
| 266 | [Persona Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3766728761) | `shunter.personaweaponsunbound` |
| 267 | [Perspective: Eaves (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3346964576) | `mlie.perspectiveeaves` |
| 268 | [Phase Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) | `det.phaseweaponry` |
| 269 | [Pillar Planner](https://steamcommunity.com/sharedfiles/filedetails/?id=3768069893) | `astryl.pillarplanner` |
| 270 | [Planning Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2877392159) | `scherub.planningextended` |
| 271 | [Plasma Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3070942578) | `cat.plasmapack` |
| 272 | [Playwright](https://steamcommunity.com/sharedfiles/filedetails/?id=3753005413) | `rokk.playwright` |
| 273 | [Pollution Spread](https://steamcommunity.com/sharedfiles/filedetails/?id=3692324370) | `turkler.pollutingfactions` |
| 274 | [Positive Connections](https://steamcommunity.com/sharedfiles/filedetails/?id=3003882069) | `cem.positiveconnections` |
| 275 | [Power Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=899714534) | `erdelf.powerindicators` |
| 276 | [Pregnancy Attitude](https://steamcommunity.com/sharedfiles/filedetails/?id=3530918349) | `carriedactyl.pregnancyattitude` |
| 277 | [Prioritized Kill Dev Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3533940586) | `prioritizedkilltool.programmerlily.com` |
| 278 | [Prisoner Realism](https://steamcommunity.com/sharedfiles/filedetails/?id=3760196312) | `legator.prisonerrealism` |
| 279 | [Progression: Soundtrack](https://steamcommunity.com/sharedfiles/filedetails/?id=3620726714) | `ferny.progressionsoundtrack` |
| 280 | [Proper Slave Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3770087153) | `kuchiki.properslavegear` |
| 281 | [Prosthetic No Missing Body Parts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2739055353) | `mlie.prostheticnomissingbodyparts` |
| 282 | [Pulse Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3077879089) | `detvisor.pulseweaponry` |
| 283 | [Pumpkin Hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=3783859385) | `pumpkin.pumpkinhairs2` |
| 284 | [Punch Button](https://steamcommunity.com/sharedfiles/filedetails/?id=2651392585) | `fuu.punchattack` |
| 285 | [Quality Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3513846773) | `dawnsglow.qualcolor` |
| 286 | [QualityBuilder Unofficial 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3512466087) | `hatti.qualitybuilder` |
| 287 | [Quest Generation Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3625994394) | `jaeger972.questgenerationtweaks` |
| 288 | [Questing Meme](https://steamcommunity.com/sharedfiles/filedetails/?id=2826539854) | `sirmashedpotato.questingmeme` |
| 289 | [Quick Stockpile Creation](https://steamcommunity.com/sharedfiles/filedetails/?id=1742151109) | `longwater.smartstockpilecreation` |
| 290 | [Raid Limiter (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2373405081) | `mlie.raidlimiter` |
| 291 | [Raider Approach Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781897112) | `oldmanyoung.raiderapproachlagfix` |
| 292 | [Range Finder](https://steamcommunity.com/sharedfiles/filedetails/?id=1332119637) | `brrainz.rangefinder` |
| 293 | [Readable Numbers](https://steamcommunity.com/sharedfiles/filedetails/?id=3762441264) | `7h0m4s.rimworldreadablenumbers` |
| 294 | [Real Faction Guest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2886929245) | `mlie.realfactionguest` |
| 295 | [Realistic Darkness (Light)](https://steamcommunity.com/sharedfiles/filedetails/?id=1555355332) | `wemd.realisticdarknesslight` |
| 296 | [Relevant Stats In Description](https://steamcommunity.com/sharedfiles/filedetails/?id=2692669482) | `mlie.relevantstatsindescription` |
| 297 | [Remove Colonies Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=3745014294) | `ferny.removecolonieslimit` |
| 298 | [Remove Tutorial Button](https://steamcommunity.com/sharedfiles/filedetails/?id=781505663) | `merthsoft.removetutorialbutton` |
| 299 | [Replace Stuff - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526354009) | `memegoddess.replacestuff` |
| 300 | [ReplaceLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3417393194) | `ferny.replacelib` |
| 301 | [RescueeJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177170403) | `ogliss.rescueejoinsplus` |
| 302 | [Research-Based Raids Difficulty](https://steamcommunity.com/sharedfiles/filedetails/?id=3679008124) | `leo.researchbasedraidsdifficulty` |
| 303 | [Resource Delivery Helper](https://steamcommunity.com/sharedfiles/filedetails/?id=3696859196) | `ferny.resourcedeliveryhelper` |
| 304 | [Restrict Undrawn Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3629753146) | `ferny.restrictundrawnapparel` |
| 305 | [Reverse Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=858744731) | `brrainz.reversecommands` |
| 306 | [Right-Click Gizmo Indicator](https://steamcommunity.com/sharedfiles/filedetails/?id=3750529401) | `cheatereater.rightclickgizmoindicator` |
| 307 | [RimPacts - Diplomacy Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3762723122) | `wowgag.rimpacts` |
| 308 | [Rimpsyche - Personality Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3535112473) | `maux36.rimpsyche` |
| 309 | [RimSaves](https://steamcommunity.com/sharedfiles/filedetails/?id=1713367505) | `arandomkiwi.rimsaves` |
| 310 | [Rimsenal - Augmented Vanilla Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=849231601) | `rimsenal.evp` |
| 311 | [Rimsenal - Security Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=736139206) | `rimsenal.security` |
| 312 | [Rimsenal - Storytellers Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725952322) | `rimsenal.storyteller` |
| 313 | [Rimsenal Faction Pack - Federation](https://steamcommunity.com/sharedfiles/filedetails/?id=736172213) | `rimsenal.federation` |
| 314 | [Rimsenal Faction Pack - Feral](https://steamcommunity.com/sharedfiles/filedetails/?id=736207111) | `rimsenal.feral` |
| 315 | [Rimsenal Faction Pack - Spacer](https://steamcommunity.com/sharedfiles/filedetails/?id=3086137468) | `rimsenal.spacer` |
| 316 | [Rimsenal Style Pack - Techist](https://steamcommunity.com/sharedfiles/filedetails/?id=2661828028) | `rimsenal.techist` |
| 317 | [Rimsenal Style Pack - Urbworld](https://steamcommunity.com/sharedfiles/filedetails/?id=2908039338) | `rimsenal.urb` |
| 318 | [Rimsenal Xenotype Pack - Askbarn](https://steamcommunity.com/sharedfiles/filedetails/?id=2918812858) | `rimsenal.askbarn` |
| 319 | [Rimsenal Xenotype Pack - Harana](https://steamcommunity.com/sharedfiles/filedetails/?id=3228637524) | `rimsenal.harana` |
| 320 | [Rimsenal Xenotype Pack - Zohar](https://steamcommunity.com/sharedfiles/filedetails/?id=3504125251) | `rimsenal.zohar` |
| 321 | [Rimsenal: Murder diversified](https://steamcommunity.com/sharedfiles/filedetails/?id=725947920) | `rimsenal.core` |
| 322 | [RimTraits - General Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2206957172) | `sierra.rt.generaltraits` |
| 323 | [Ritual Size Attenuation](https://steamcommunity.com/sharedfiles/filedetails/?id=3262033797) | `delmaintweaks.ritualsizeattenuation` |
| 324 | [Romina Ready - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3736785369) | `ferny.rominaready` |
| 325 | [Roo's HD Hairstyles (continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3559870636) | `rooboid.hdhair.continued` |
| 326 | [Royalty Room Info (Satisfied Title and Upgrade Requirements)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600393808) | `jaeger972.royaltyroominfo` |
| 327 | [RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=728314182) | `ratys.rtfuse` |
| 328 | [Run and Hide (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2022480344) | `mlie.runandhide` |
| 329 | [Rusty's Foliage Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=3646973576) | `rustys.foliage.hairs` |
| 330 | [Satellite Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=3793766543) | `astryl.satelliteminimap` |
| 331 | [Save File Compression](https://steamcommunity.com/sharedfiles/filedetails/?id=3562729067) | `amch.savefilecompression` |
| 332 | [Save Storyteller Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3744418945) | `vc.savestorytellersettings` |
| 333 | [Scrollable Inspect Panes](https://steamcommunity.com/sharedfiles/filedetails/?id=2889151469) | `kathanon.scrollableinspectpanes` |
| 334 | [Seasonal Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2045114229) | `mlie.seasonalweather` |
| 335 | [Self Dyeing](https://steamcommunity.com/sharedfiles/filedetails/?id=2562859859) | `avilmask.selfdyeing` |
| 336 | [Sensible Bed Ownership](https://steamcommunity.com/sharedfiles/filedetails/?id=3328702448) | `sensiblebedownership.1trickpwnyta` |
| 337 | [Settlement Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=2240524945) | `dninemfive.settlementdescription` |
| 338 | [Shadow Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3797651319) | `antisodium.shadowfix` |
| 339 | [Show Buildable Material Count](https://steamcommunity.com/sharedfiles/filedetails/?id=3245101540) | `bp.showbuildablematerialcount` |
| 340 | [Show Known Techprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2920370783) | `spacemoth.showknowntechprints` |
| 341 | [Show Me Your Hands](https://steamcommunity.com/sharedfiles/filedetails/?id=2475965842) | `mlie.showmeyourhands` |
| 342 | [Show Mod Updates](https://steamcommunity.com/sharedfiles/filedetails/?id=3218690663) | `mlie.showmodupdates` |
| 343 | [Show Possible Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=3338920230) | `mlie.showpossiblematerials` |
| 344 | [Show Weapon Tallies](https://steamcommunity.com/sharedfiles/filedetails/?id=2901520677) | `kathanon.showweapontallies` |
| 345 | [Show work site faction relations](https://steamcommunity.com/sharedfiles/filedetails/?id=3487185451) | `riebi.showfactionrelations` |
| 346 | [Simple Leadership](https://steamcommunity.com/sharedfiles/filedetails/?id=3668307448) | `pb3n.simpleleadership` |
| 347 | [Simple sidearms](https://steamcommunity.com/sharedfiles/filedetails/?id=927155256) | `petetimessix.simplesidearms` |
| 348 | [SimpleCameraSetting](https://steamcommunity.com/sharedfiles/filedetails/?id=3232415388) | `ray1203.simplecamerasetting` |
| 349 | [Sin's Softer Hair Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=3790361865) | `sinner.softerhaircolors` |
| 350 | [Skilled Refueling](https://steamcommunity.com/sharedfiles/filedetails/?id=2873628478) | `mrhydralisk.skilledrefueling` |
| 351 | [Skilled Slaughtering](https://steamcommunity.com/sharedfiles/filedetails/?id=2873284405) | `mrhydralisk.skilledslaughtering` |
| 352 | [Smart Medicine - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526251680) | `memegoddess.smartmedicine` |
| 353 | [Smart Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=1504723424) | `sarg.smartspeed` |
| 354 | [Smarter Construction](https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773) | `dhultgren.smarterconstruction` |
| 355 | [Smarter Deconstruction and Mining (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3261302741) | `mlie.smarterdeconstructionandmining` |
| 356 | [Smarter Visitors](https://steamcommunity.com/sharedfiles/filedetails/?id=3012491521) | `mlie.smartervisitors` |
| 357 | [Sovs hairs and beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3368700799) | `sov.hairbeardandstuff` |
| 358 | [Spacer Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3247891820) | `det.spacerarsenal` |
| 359 | [Sparkling Worlds - Full Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=1123043922) | `albion.sparklingworlds.full` |
| 360 | [Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3386164757) | `cat.stellicarsenal` |
| 361 | [Stop, Drop, And Roll! [BAL]](https://steamcommunity.com/sharedfiles/filedetails/?id=2362707956) | `balistafreak.stopdropandroll` |
| 362 | [Storage Stats in Description](https://steamcommunity.com/sharedfiles/filedetails/?id=3740185419) | `ferny.storagestatsindescription` |
| 363 | [Style Change Anytime](https://steamcommunity.com/sharedfiles/filedetails/?id=2880145066) | `dra.stylechangeanytime` |
| 364 | [Style Tooltip Columns](https://steamcommunity.com/sharedfiles/filedetails/?id=3748051379) | `dorbo.styletooltipcolumns` |
| 365 | [SurgeryImproved](https://steamcommunity.com/sharedfiles/filedetails/?id=3233032071) | `darkshadow44.surgeryimproved` |
| 366 | [Tabula Rasa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) | `neronix17.toolbox` |
| 367 | [TD Enhancement Pack - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3525414162) | `memegoddess.tdpack` |
| 368 | [TDS Bug Fixes - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3529433984) | `memegoddess.tdsbugfixes` |
| 369 | [Text Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3773691016) | `ferny.texttool` |
| 370 | [The Sims Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3604588393) | `goji.thesimstraits` |
| 371 | [The Vanity Project - Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3143138787) | `thevanityproject.beards` |
| 372 | [Toggleable Shields](https://steamcommunity.com/sharedfiles/filedetails/?id=3261317430) | `owlchemist.toggleableshields` |
| 373 | [Tooltipped Long Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2303452428) | `reiquard.tooltippedlongnames` |
| 374 | [Trade Ships Drop Spot](https://steamcommunity.com/sharedfiles/filedetails/?id=1969732297) | `smashphil.dropspot` |
| 375 | [Trader Show Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3007612546) | `nachotoast.tradershowxenotype` |
| 376 | [Trait Rarity Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=1751884355) | `carnysenpai.traitraritycolors` |
| 377 | [Useful Marks](https://steamcommunity.com/sharedfiles/filedetails/?id=3506573327) | `andromeda.usefulmarks` |
| 378 | [Trauma Team Complete](https://steamcommunity.com/sharedfiles/filedetails/?id=3764955860) | `edelweisspirate.traumateamcomplete` |
| 379 | [Tribal Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=2879649038) | `shenanigans.tribalbackstories1.4` |
| 380 | [Triumphant Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3647618250) | `ferny.triumphantresearch` |
| 381 | [True Non-Senescence](https://steamcommunity.com/sharedfiles/filedetails/?id=3547377353) | `lunardawn.truenonsenescence` |
| 382 | [Ugly Together](https://steamcommunity.com/sharedfiles/filedetails/?id=2884233923) | `uglytogether.leeh` |
| 383 | [Ultimate Colonist Name Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3498692639) | `hol.ultcolnames` |
| 384 | [Unburned](https://steamcommunity.com/sharedfiles/filedetails/?id=3538336699) | `andromeda.unburned` |
| 385 | [Undraft After Tucking](https://steamcommunity.com/sharedfiles/filedetails/?id=2157495459) | `madarauchiha.undraftaftertucking` |
| 386 | [Unique Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3707263149) | `shunter.uniqueweaponsunbound` |
| 387 | [Universal Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3540066516) | `trigger.universalblueprints` |
| 388 | [Use Bedrolls - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3534169351) | `memegoddess.usebedrolls` |
| 389 | [Useless Hair (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3044216509) | `zal.uselesshair` |
| 390 | [Vanilla Achievements Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2288125657) | `vanillaexpanded.achievements` |
| 391 | [Vanilla Animals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2871933948) | `vanillaexpanded.vanillaanimalsexpanded` |
| 392 | [Vanilla Animals Expanded — Waste Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=2962126499) | `vanillaexpanded.vaewaste` |
| 393 | [Vanilla Anomaly Expanded - Insanity](https://steamcommunity.com/sharedfiles/filedetails/?id=3342412435) | `vanillaexpanded.vanomalyeinsanity` |
| 394 | [Vanilla Apparel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814987817) | `vanillaexpanded.vappe` |
| 395 | [Vanilla Apparel Expanded — Accessories](https://steamcommunity.com/sharedfiles/filedetails/?id=2521176396) | `vanillaexpanded.vaeaccessories` |
| 396 | [Vanilla Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3273371966) | `det.vanillaarsenal` |
| 397 | [Vanilla Backstories Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2861806869) | `vanillaexpanded.vanillabackstoriesexpanded` |
| 398 | [Vanilla Events Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742) | `vanillaexpanded.vee` |
| 399 | [Vanilla Factions Expanded - Empire](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380) | `oskarpotocki.vfe.empire` |
| 400 | [Vanilla Factions Expanded - Insectoids 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431) | `oskarpotocki.vfe.insectoid2` |
| 401 | [Vanilla Factions Expanded - Medieval 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874) | `oskarpotocki.vfe.medieval2` |
| 402 | [Vanilla Factions Expanded - Settlers](https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119) | `oskarpotocki.vanillafactionsexpanded.settlersmodule` |
| 403 | [Vanilla Factions Expanded - Tribals](https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283) | `oskarpotocki.vfe.tribals` |
| 404 | [Vanilla Furniture Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143) | `vanillaexpanded.vfecore` |
| 405 | [Vanilla Furniture Expanded - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=2608762624) | `vanillaexpanded.vfearchitect` |
| 406 | [Vanilla Furniture Expanded - Art](https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023) | `vanillaexpanded.vfeart` |
| 407 | [Vanilla Furniture Expanded - Factory](https://steamcommunity.com/sharedfiles/filedetails/?id=3686924415) | `vanillaexpanded.vfefactory` |
| 408 | [Vanilla Furniture Expanded - Farming](https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779) | `vanillaexpanded.vfefarming` |
| 409 | [Vanilla Furniture Expanded - Medical Module](https://steamcommunity.com/sharedfiles/filedetails/?id=1718191613) | `vanillaexpanded.vfemedical` |
| 410 | [Vanilla Furniture Expanded - Power](https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477) | `vanillaexpanded.vfepower` |
| 411 | [Vanilla Furniture Expanded - Production](https://steamcommunity.com/sharedfiles/filedetails/?id=1880253632) | `vanillaexpanded.vfeproduction` |
| 412 | [Vanilla Furniture Expanded - Props and Decor](https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149) | `vanillaexpanded.vfepropsanddecor` |
| 413 | [Vanilla Furniture Expanded - Security](https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007) | `vanillaexpanded.vfesecurity` |
| 414 | [Vanilla Furniture Expanded - Spacer Module](https://steamcommunity.com/sharedfiles/filedetails/?id=2028381079) | `vanillaexpanded.vfespacer` |
| 415 | [Vanilla Gravship Expanded - Chapter 1](https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606) | `vanillaexpanded.gravship` |
| 416 | [Vanilla Hair Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256) | `vanillaexpanded.vhe` |
| 417 | [Vanilla Helixien Gas Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2877699803) | `vanillaexpanded.helixiengas` |
| 418 | [Vanilla Ideology Expanded - Hats and Rags](https://steamcommunity.com/sharedfiles/filedetails/?id=2567387768) | `vanillaexpanded.viehar` |
| 419 | [Vanilla Ideology Expanded - Icons and Symbols](https://steamcommunity.com/sharedfiles/filedetails/?id=2552609458) | `vanillaexpanded.ideo.iconsandsymbols` |
| 420 | [Vanilla Ideology Expanded - Relics and Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2564895018) | `vanillaexpanded.ideo.relicsandartifacts` |
| 421 | [Vanilla Ideology Expanded - Sophian Style](https://steamcommunity.com/sharedfiles/filedetails/?id=3194606539) | `vanillaexpanded.ideo.sophianstyle` |
| 422 | [Vanilla Landmarks Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229) | `vanillaexpanded.vexploratione` |
| 423 | [Vanilla Loot Beams](https://steamcommunity.com/sharedfiles/filedetails/?id=3746929144) | `astryl.vanillalootbeams` |
| 424 | [Vanilla Nutrient Paste Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763) | `vanillaexpanded.vnutriente` |
| 425 | [Vanilla Plants Expanded - Succulents](https://steamcommunity.com/sharedfiles/filedetails/?id=2198652536) | `vanillaexpanded.vplantsesucculents` |
| 426 | [Vanilla Quests Expanded - Ancients](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) | `vanillaquestsexpanded.ancients` |
| 427 | [Vanilla Quests Expanded - The Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3411401573) | `vanillaquestsexpanded.generator` |
| 428 | [Vanilla Races Expanded - Custom Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2917311689) | `vanillaracesexpanded.customicons` |
| 429 | [Vanilla Races Expanded - Fungoid](https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053) | `vanillaracesexpanded.fungoid` |
| 430 | [Vanilla Races Expanded - Genie](https://steamcommunity.com/sharedfiles/filedetails/?id=2901424072) | `vanillaracesexpanded.genie` |
| 431 | [Vanilla Races Expanded - Highmate](https://steamcommunity.com/sharedfiles/filedetails/?id=2995385834) | `vanillaracesexpanded.highmate` |
| 432 | [Vanilla Races Expanded - Hussar](https://steamcommunity.com/sharedfiles/filedetails/?id=2893586390) | `vanillaracesexpanded.hussar` |
| 433 | [Vanilla Races Expanded - Lycanthrope](https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100) | `vanillaracesexpanded.lycanthrope` |
| 434 | [Vanilla Races Expanded - Phytokin](https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805) | `vanillaracesexpanded.phytokin` |
| 435 | [Vanilla Races Expanded - Pigskin](https://steamcommunity.com/sharedfiles/filedetails/?id=3164798809) | `vanillaracesexpanded.pigskin` |
| 436 | [Vanilla Races Expanded - Sanguophage](https://steamcommunity.com/sharedfiles/filedetails/?id=2963116383) | `vanillaracesexpanded.sanguophage` |
| 437 | [Vanilla Races Expanded - Saurid](https://steamcommunity.com/sharedfiles/filedetails/?id=2880990495) | `vanillaracesexpanded.saurid` |
| 438 | [Vanilla Races Expanded - Starjack](https://steamcommunity.com/sharedfiles/filedetails/?id=3531912428) | `vanillaracesexpanded.starjack` |
| 439 | [Vanilla Races Expanded - Waster](https://steamcommunity.com/sharedfiles/filedetails/?id=2983471725) | `vanillaracesexpanded.waster` |
| 440 | [Vanilla Recycling Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3155781848) | `vanillaexpanded.recycling` |
| 441 | [Vanilla Skills Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3400246558) | `vanillaexpanded.skills` |
| 442 | [Vanilla Social Interactions Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083) | `vanillaexpanded.vanillasocialinteractionsexpanded` |
| 443 | [Vanilla Storytellers Expanded - Perry Persistent](https://steamcommunity.com/sharedfiles/filedetails/?id=2149702069) | `vse.perrypersistent` |
| 444 | [Vanilla Temperature Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258) | `vanillaexpanded.temperature` |
| 445 | [Vanilla Traits Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655) | `vanillaexpanded.vanillatraitsexpanded` |
| 446 | [Vanilla Weapons Expanded - Bioferrite](https://steamcommunity.com/sharedfiles/filedetails/?id=3287469135) | `vanillaexpanded.vwebf` |
| 447 | [Vanilla Weapons Expanded - Coilguns](https://steamcommunity.com/sharedfiles/filedetails/?id=2375692535) | `vanillaexpanded.vwec` |
| 448 | [Vanilla Weapons Expanded - Grenades](https://steamcommunity.com/sharedfiles/filedetails/?id=2194472657) | `vanillaexpanded.vweg` |
| 449 | [Vanilla Weapons Expanded - Makeshift](https://steamcommunity.com/sharedfiles/filedetails/?id=2419690698) | `vanillaexpanded.vwems` |
| 450 | [Vanilla Weapons Expanded - Non-Lethal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918354) | `vanillaexpanded.vwenl` |
| 451 | [Vanilla Weapons Expanded - Tribal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918552) | `vanillaexpanded.vwetb` |
| 452 | [Vanometric Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3041187151) | `hlx.vanometricgenerator` |
| 453 | [Various Space Ship Chunk (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014616487) | `mlie.variousspaceshipchunk` |
| 454 | [Venerated Xenotype Flexibility (Revisited)](https://steamcommunity.com/sharedfiles/filedetails/?id=3586153375) | `dcszar.veneratedxenotypeflexibility` |
| 455 | [VFE Pack for Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3612525913) | `cantaloupetheclown.vanillafurnitureexpandedpack` |
| 456 | [Victor Veto - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3745138223) | `ferny.victorveto` |
| 457 | [Volt Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3551721422) | `det.voltweaponry` |
| 458 | [VV - New Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=3448458106) | `vvenchov.vvnewharvest` |
| 459 | [WanderJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177016016) | `ogliss.wanderjoinsplus` |
| 460 | [We Are United](https://steamcommunity.com/sharedfiles/filedetails/?id=3030804445) | `sl4vp0wer.weareunited` |
| 461 | [Weather Duration](https://steamcommunity.com/sharedfiles/filedetails/?id=2822836257) | `kathanon.weatherduration` |
| 462 | [What's Missing?](https://steamcommunity.com/sharedfiles/filedetails/?id=3231090162) | `revolus.whatsmissing` |
| 463 | [What's That Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2258431182) | `co.uk.epicguru.whatsthatmod` |
| 464 | [Who shot my leg off?](https://steamcommunity.com/sharedfiles/filedetails/?id=3491552121) | `tixiv.whoshotmylegoff` |
| 465 | [Wololoo - Better Conversion and Recruitment](https://steamcommunity.com/sharedfiles/filedetails/?id=3108763487) | `redmattis.betterconversion` |
| 466 | [Won hair_men](https://steamcommunity.com/sharedfiles/filedetails/?id=1879950403) | `won.hair` |
| 467 | [Won hair_women](https://steamcommunity.com/sharedfiles/filedetails/?id=1886150617) | `won2.hair` |
| 468 | [Work History](https://steamcommunity.com/sharedfiles/filedetails/?id=3727287885) | `lebek.workhistory` |
| 469 | [Xenotype Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3787666264) | `ceramic.xenotypebackstories` |
| 470 | [Xenotype Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832924427) | `lakinmanheim.xenotypedescriptions` |
| 471 | [Xenotype Summary](https://steamcommunity.com/sharedfiles/filedetails/?id=3710012135) | `zhuma.xenotypesummary` |
| 472 | [Xeva's Rimhair](https://steamcommunity.com/sharedfiles/filedetails/?id=705924057) | `orion.xevarimhair` |
| 473 | [[AV] Ancient Spacer Xenos](https://steamcommunity.com/sharedfiles/filedetails/?id=3241589828) | `veltaris.xenos` |
| 474 | [[AV] Kosmian Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3774474578) | `veltaris.kosmian` |
| 475 | [[AV] Mechanoid orbital traders](https://steamcommunity.com/sharedfiles/filedetails/?id=3172198798) | `veltaris.mechcrate` |
| 476 | [[AV] Mechtech](https://steamcommunity.com/sharedfiles/filedetails/?id=3339795890) | `veltaris.mechtech` |
| 477 | [[AV] Toxic Waste Disposer](https://steamcommunity.com/sharedfiles/filedetails/?id=3440190384) | `veltaris.wastecollector` |
| 478 | [[BTD] Gravship Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3575162262) | `btd.remix.gravshipblueprints` |
| 479 | [[ETF] Butchering Now Yields Hemogen!](https://steamcommunity.com/sharedfiles/filedetails/?id=3688485013) | `echotidefish.butcheringnowyieldshemogen` |
| 480 | [[LFS] UI Rethemed - Factions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832850311) | `lazyfridaystudio.uirethemed.factions` |
| 481 | [[LFS] UI Rethemed - Ideology](https://steamcommunity.com/sharedfiles/filedetails/?id=3834172773) | `lazyfridaystudio.uirethemed.ideology` |
| 482 | [[NL] Facial Animation - Experimentals](https://steamcommunity.com/sharedfiles/filedetails/?id=2581693737) | `nals.facialanimationexperimentals` |
| 483 | [Achtung!](https://steamcommunity.com/sharedfiles/filedetails/?id=730936602) | `brrainz.achtung` |
| 484 | [AFU女士发型_Women's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2020779666) | `jintuzilamian.hairstyle.afuwomenshairstyles` |
| 485 | [Alpha Props - Parks and Gardens](https://steamcommunity.com/sharedfiles/filedetails/?id=3146268928) | `sarg.alphapropsparks` |
| 486 | [Alpha Skills](https://steamcommunity.com/sharedfiles/filedetails/?id=3448953006) | `sarg.alphaskills` |
| 487 | [Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206) | `xmb.ancienturbanruins.mo` |
| 488 | [Biomes! Core](https://steamcommunity.com/sharedfiles/filedetails/?id=2038000893) | `biomesteam.biomescore` |
| 489 | [Capture Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787736781) | `ferny.captureexpansion` |
| 490 | [Circinus Performance Analyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=3773680130) | `astryl.circinus` |
| 491 | [Coilguns Addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3788730287) | `aqued.coilguns` |
| 492 | [Colony Manager Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=3310027356) | `ilyvion.colonymanagerredux` |
| 493 | [Decor into Styles](https://steamcommunity.com/sharedfiles/filedetails/?id=3771249138) | `ferny.decorintostyles` |
| 494 | [Expanded Prosthetics and Organ Engineering - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=1949064302) | `vat.epoeforked` |
| 495 | [FEB's Biopunk Livestock](https://steamcommunity.com/sharedfiles/filedetails/?id=3556058978) | `feb.biopunklivestock` |
| 496 | [Fix Multi Stack Hoppers](https://steamcommunity.com/sharedfiles/filedetails/?id=3534591980) | `zokradonh.supporthoppermultistacks` |
| 497 | [Flourish - Everyday Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3810518822) | `astryl.flourisheveryday` |
| 498 | [Fortifications - Industrial](https://steamcommunity.com/sharedfiles/filedetails/?id=2561619583) | `aoba.fortress.industrial` |
| 499 | [Gerrymon's Misc Props: Dungeon](https://steamcommunity.com/sharedfiles/filedetails/?id=3772571876) | `gmmp.dungeon` |
| 500 | [Gravship Fleet](https://steamcommunity.com/sharedfiles/filedetails/?id=3787239201) | `asunib.gravshipfleet` |
| 501 | [Gravship Raids](https://steamcommunity.com/sharedfiles/filedetails/?id=3767338163) | `sk.gravshipraids` |
| 502 | [Helixien Gas Brazier](https://steamcommunity.com/sharedfiles/filedetails/?id=2770815124) | `cixwow.helixienbrazier` |
| 503 | [Helixien Gas Production](https://steamcommunity.com/sharedfiles/filedetails/?id=2829208414) | `cixwow.helixiengasproduction` |
| 504 | [Hospitality (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3509486825) | `orion.hospitality` |
| 505 | [IMPACT - Combat Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3812370282) | `astryl.impact` |
| 506 | [Improved Map Search](https://steamcommunity.com/sharedfiles/filedetails/?id=3547866455) | `kathanon.groupedmapsearch` |
| 507 | [Info Card Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=3776660810) | `kaamalauppias.infocardplus` |
| 508 | [Junkie Knick Knacks](https://steamcommunity.com/sharedfiles/filedetails/?id=3607387774) | `ucp.junkieprops` |
| 509 | [More Phytokin Xenotypes](https://steamcommunity.com/sharedfiles/filedetails/?id=3813857865) | `pumpkin.morephytokinxenotypes` |
| 510 | [Nice Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3328729902) | `andromeda.nicehealthtab` |
| 511 | [Nice Research Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3773496046) | `andromeda.niceresearchtab` |
| 512 | [Permits: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3521089734) | `smxrez.permitsreexamined` |
| 513 | [Power: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3590186274) | `smxrez.powerreexamined` |
| 514 | [Reel's Expanded Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3237638097) | `reel.expanded.storage` |
| 515 | [Rimsenal - Hair pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725949967) | `rimsenal.hair` |
| 516 | [Searchable Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2928608119) | `kathanon.searchablemenus` |
| 517 | [Show Me Your Tools - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3774821567) | `meathax.showmeyourtools` |
| 518 | [Sleek Work Schedule Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3742689571) | `squishyjellyfish.nicescheduletab` |
| 519 | [Trait and Backstory Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2873494547) | `superniquito.traiticons` |
| 520 | [Universal Blueprints Alpha Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3573901897) | `trigger.universalblueprints.alphapack` |
| 521 | [Vanilla Armour Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814988282) | `vanillaexpanded.varme` |
| 522 | [Vanilla Chemfuel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2792917473) | `vanillaexpanded.vchemfuele` |
| 523 | [Vanilla Cooking Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519) | `vanillaexpanded.vcooke` |
| 524 | [Vanilla Expanded: Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3313700572) | `ferny.vanillaexpandedherobackgrounds` |
| 525 | [Vanilla Factions Expanded - Deserters](https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377) | `oskarpotocki.vfe.deserters` |
| 526 | [Vanilla Fishing Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942) | `vanillaexpanded.vcef` |
| 527 | [Vanilla Persona Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2826922787) | `vanillaexpanded.vpersonaweaponse` |
| 528 | [Vanilla Psycasts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) | `vanillaexpanded.vpsycastse` |
| 529 | [Vanilla Skills Rexamined](https://steamcommunity.com/sharedfiles/filedetails/?id=3235834179) | `gravenwitch.vsrexamined` |
| 530 | [Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360) | `vanillaexpanded.vwe` |
| 531 | [VFE - Insectoids 2 Deconstructable Infested Ship Chunks](https://steamcommunity.com/sharedfiles/filedetails/?id=3641480952) | `decinfshipchunkspatch.vfe.insectoid2` |
| 532 | [VSIE - Rational Trait Development](https://steamcommunity.com/sharedfiles/filedetails/?id=2916405546) | `stagz.vsierationaltraitdevelopment` |
| 533 | [Worksites Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3687071198) | `godsfathermixtape.worksitesexpanded` |
| 534 | [Zalcore Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3104631106) | `zal.prefabs` |
| 535 | [[AV] Mechanoid Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=3745387929) | `veltaris.mechstations` |
| 536 | [[BTD] Ship Pack: Community Designs](https://steamcommunity.com/sharedfiles/filedetails/?id=3832496468) | `btd.gbp.shippack.communitydesigns` |
| 537 | [[FSF] Advanced Bionics Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2006925330) | `frozensnowfox.advancedbionicsexpansion` |
| 538 | [Auto links](https://steamcommunity.com/sharedfiles/filedetails/?id=2059389912) | `automatic.autolinks` |
| 539 | [Biomes! Caverns](https://steamcommunity.com/sharedfiles/filedetails/?id=2969748433) | `biomesteam.biomescaverns` |
| 540 | [Biomes! Oasis](https://steamcommunity.com/sharedfiles/filedetails/?id=2538518381) | `biomesteam.oasis` |
| 541 | [Bradson's Main Button Icons (Forked + Expanded)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532359201) | `bs.mbifvte` |
| 542 | [Busywork](https://steamcommunity.com/sharedfiles/filedetails/?id=3775253009) | `andromeda.busywork` |
| 543 | [Cat's Boots and Gloves](https://steamcommunity.com/sharedfiles/filedetails/?id=3552813398) | `catlover366.bootsandgloves` |
| 544 | [Clean Command Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3688400757) | `leo.cleancommandbar` |
| 545 | [EPOE-Forked: Royalty DLC expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2008970276) | `vat.epoeforkedroyalty` |
| 546 | [Farming Hysteresis](https://steamcommunity.com/sharedfiles/filedetails/?id=2431191516) | `ilyvion.farminghysteresis` |
| 547 | [Fortification Industrial - Citadel](https://steamcommunity.com/sharedfiles/filedetails/?id=2887532326) | `aoba.fortress.industrial.citadel` |
| 548 | [Fortification Industrial - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=3093455596) | `aoba.fortress.frontier` |
| 549 | [Fortification Industrial -Nuclear Dawn](https://steamcommunity.com/sharedfiles/filedetails/?id=2733185331) | `aoba.fortress.industrial.nucleardawn` |
| 550 | [Genderbending Corrupted Obelisk](https://steamcommunity.com/sharedfiles/filedetails/?id=3350774891) | `abraxas.genderbending.corrupted.obelisk` |
| 551 | [Genetic Biotech for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317537) | `sd.fa.geneticheadsbiotech` |
| 552 | [Genetic Mods for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317734) | `sd.fa.geneticheadsmods` |
| 553 | [Hospitality + Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3789536584) | `fumblesneeze.guestbedgizmo` |
| 554 | [Hospitality - Invite to Stay](https://steamcommunity.com/sharedfiles/filedetails/?id=3783522506) | `gatoviejo.hospitality.invitetostay` |
| 555 | [Hospitality: Casino](https://steamcommunity.com/sharedfiles/filedetails/?id=2939292644) | `adamas.hospitalitycasino` |
| 556 | [Hospitality: Vending machines](https://steamcommunity.com/sharedfiles/filedetails/?id=3014885065) | `adamas.vendingmachines` |
| 557 | [Industrial Hardleather, Concrete and Cheese](https://steamcommunity.com/sharedfiles/filedetails/?id=3535926848) | `talias.vfehardleatherconcrete` |
| 558 | [Legendary Boss](https://steamcommunity.com/sharedfiles/filedetails/?id=3538894636) | `andromeda.legendaryboss` |
| 559 | [Modern Psycasts UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3745137736) | `astryl.modernpsycastsui` |
| 560 | [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) | `andromeda.nicebilltab` |
| 561 | [Nice Plants Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3685058533) | `andromeda.niceplantsmenu` |
| 562 | [Nicer Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3701712250) | `that0neperson.nhtprostheticsextended` |
| 563 | [Pawns Use Fire Extinguishers (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3612608024) | `mlie.pawnsusefireextinguishers` |
| 564 | [ReGrowth 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569) | `regrowth.botr.core` |
| 565 | [RimQuest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2263331727) | `mlie.rimquest` |
| 566 | [Useful Marks in Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3720856279) | `truemogician.usefulmarksincolonygroups` |
| 567 | [Vanilla Fishing Expanded - Fishing Treasures AddOn](https://steamcommunity.com/sharedfiles/filedetails/?id=2468543398) | `vanillaexpanded.vcefaddon` |
| 568 | [Vanilla Psycasts Expanded - Hemosage](https://steamcommunity.com/sharedfiles/filedetails/?id=2990596478) | `vanillaexpanded.vpe.hemosage` |
| 569 | [Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=2816938779) | `vanillaexpanded.vtexe.facialanims` |
| 570 | [Vanilla Weapons Expanded - Heavy Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) | `vanillaexpanded.vwehw` |
| 571 | [Vanilla Weapons Expanded - Laser](https://steamcommunity.com/sharedfiles/filedetails/?id=1989352844) | `vanillaexpanded.vwel` |
| 572 | [Vanilla Weapons Expanded - Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=1906780517) | `vanillaexpanded.vweq` |
| 573 | [A Dog Said... Animal Prosthetics 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3238353862) | `sambucher.adogsaidanimalprosthetics2` |
| 574 | [Biomes! Polluted Lands](https://steamcommunity.com/sharedfiles/filedetails/?id=3390196656) | `biomesteam.biomespollutedlands` |
| 575 | [Bionic icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1677616980) | `automatic.bionicicons` |
| 576 | [Bionic Irises for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3828692274) | `neztech.fabioniciris` |
| 577 | [Drafted Auto-Combat](https://steamcommunity.com/sharedfiles/filedetails/?id=3546270076) | `redmattis.autocombat` |
| 578 | [FirefliesTwo](https://steamcommunity.com/sharedfiles/filedetails/?id=3347026598) | `scurvyez.firefliestwo` |
| 579 | [Glitter Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3768428526) | `zal.glitterweaponry` |
| 580 | [Gulden Biome (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607066070) | `pphhyy.guldennew` |
| 581 | [More and Retextured Head Shapes for VTE for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3407049972) | `sd.fa.vte.moreheadshapes` |
| 582 | [More Prop Categories](https://steamcommunity.com/sharedfiles/filedetails/?id=3167021055) | `ferny.propscore` |
| 583 | [Nice Bill Tab - Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721023311) | `hicon.nicebilltabexpansion` |
| 584 | [Stem's VWE Industrial Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787566245) | `stem.vweindustrialexpansion` |
| 585 | [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) | `neronix17.tweaksgalore` |
| 586 | [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948) | `oskarpotocki.vfe.pirates` |
| 587 | [Vanilla Plants Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308522) | `vanillaexpanded.vplantse` |
| 588 | [Vanilla Textures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324) | `vanillaexpanded.vtexe` |
| 589 | [Food Texture Variety Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3354455179) | `goat.food.texture.variety.core` |
| 590 | [Vanilla Weapons Expanded - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918139) | `vanillaexpanded.vweft` |
| 591 | [WVC - Work Modes](https://steamcommunity.com/sharedfiles/filedetails/?id=2888380373) | `wvc.sergkart.biotech.moremechanoidsworkmodes` |
| 592 | [Altered Carbon 2: ReSleeved](https://steamcommunity.com/sharedfiles/filedetails/?id=2196278117) | `hlx.ultratechalteredcarbon` |
| 593 | [Decor into Props](https://steamcommunity.com/sharedfiles/filedetails/?id=3771248988) | `ferny.decorintoprops` |
| 594 | [Eltex Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532192464) | `zal.eltexweaponry` |
| 595 | [More Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=2707120862) | `tidal.morevanilla.textures` |
| 596 | [Progression: Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3361425712) | `ferny.progressionstorytellers` |
| 597 | [Unique Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=3776637622) | `shunter.uniquemeleeweapons` |
| 598 | [Vanilla Plants Expanded - More Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667) | `vanillaexpanded.vplantsemore` |
| 599 | [Vanilla Plants Expanded - Mushrooms](https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281) | `vanillaexpanded.vplantsemushrooms` |
| 600 | [Vanilla Races Expanded - Insector](https://steamcommunity.com/sharedfiles/filedetails/?id=3260509684) | `vanillaracesexpanded.insector` |
| 601 | [Alpha Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856) | `sarg.alphaanimals` |
| 602 | [Better Trees: More Djeeshka Like Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3543705507) | `maal.bettertreesmod` |
| 603 | [Vanilla Gravship Expanded - Chapter 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3799737423) | `vanillaexpanded.gravship2` |
| 604 | [Vanilla Storytellers Expanded - Igor Invader (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3582195822) | `zal.vseigorinvader` |
| 605 | [VV - New Harvest: Compatibility Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3570411438) | `octodecoy.vvnewharvest.compatpatches` |
| 606 | [Alpha Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502) | `sarg.alphagenes` |
| 607 | [Vanilla Ideology Expanded - Memes and Structures](https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500) | `vanillaexpanded.vmemese` |
| 608 | [[FSF] Better Exploration Loot](https://steamcommunity.com/sharedfiles/filedetails/?id=3526957922) | `frozensnowfox.betterexplorationloot` |
| 609 | [Alpha Genes Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3751293981) | `lucius.alphagenesfa` |
| 610 | [Alpha Genes Integrated](https://steamcommunity.com/sharedfiles/filedetails/?id=3538704272) | `kupa.alphagenesintegrated` |
| 611 | [Auto-Cast Specialist Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=3459647882) | `linnun.autocastspecialistcommands` |
| 612 | [Linkable Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3659197870) | `linnun.linkablegroups` |
| 613 | [Progression: Scenarios](https://steamcommunity.com/sharedfiles/filedetails/?id=3378384387) | `ferny.progressionscenarios` |
| 614 | [Reel's Insector Faction](https://steamcommunity.com/sharedfiles/filedetails/?id=3309022698) | `reel.insectorfaction` |
| 615 | [Tastier Vanilla Clothes](https://steamcommunity.com/sharedfiles/filedetails/?id=2808554143) | `al9000.tvc` |
| 616 | [Keyz Reference Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3689557041) | `keyz182.keyzreferencetools` |
| 617 | [Bullet Sounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3806470380) | `senilia.bulletsounds` |
| 618 | [Cherry picker scroll bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3781285156) | `lucius.cherryscroll` |
| 619 | [Ambience Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3835975372) | `neko.ambiencecore` |
| 620 | [Thunderstorm](https://steamcommunity.com/sharedfiles/filedetails/?id=3278460897) | `neko.thunderstorm` |
| 621 | [Shortcuts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503701770) | `mlie.shortcuts` |
| 622 | [Big Little Mod Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2710382569) | `daria40k.biglittlemodpatch` |
| 623 | [Muzzle Flash](https://steamcommunity.com/sharedfiles/filedetails/?id=2917732219) | `issaczhuang.muzzleflash` |
| 624 | [Turrets For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3800286808) | `bambaryla.allbambaturrets` |
| 625 | [[WYD] Better Campsites](https://steamcommunity.com/sharedfiles/filedetails/?id=3546818262) | `wyr3d.bettercampsites` |
| 626 | [Art on Neolithic Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2556397158) | `dizzy.neolithicweaponart` |
| 627 | [Balanced Eclipse and Solar Flare - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=2929407615) | `skelly.neceros.balancedeclipseandsolarflare.forked` |
| 628 | [BrainShock](https://steamcommunity.com/sharedfiles/filedetails/?id=2227796764) | `nightkosh.brainshock` |
| 629 | [Dimorphic Neanderthals](https://steamcommunity.com/sharedfiles/filedetails/?id=3612381568) | `zoid.femneanderthals` |
| 630 | [DLG.Mutations behind](https://steamcommunity.com/sharedfiles/filedetails/?id=3637525997) | `dlg.mutation.behind` |
| 631 | [Drawn Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=2917791361) | `overpl.drawnspots` |
| 632 | [High Life Drug Cooking Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=3183941628) | `bigtortoise.drugcookingspeed` |
| 633 | [Less Rebuff](https://steamcommunity.com/sharedfiles/filedetails/?id=774543761) | `dingo.lessrebuff` |
| 634 | [More Descriptive Words and Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2180384246) | `hol.words` |
| 635 | [Cultural names: Stellic](https://steamcommunity.com/sharedfiles/filedetails/?id=3161247581) | `hawkwood.culturestellic` |
| 636 | [No Headstone Till Buried](https://steamcommunity.com/sharedfiles/filedetails/?id=2845934338) | `wanzi.noheadstone` |
| 637 | [Smaller radius for Anima Trees, Shrines and Animus Stones](https://steamcommunity.com/sharedfiles/filedetails/?id=2812513517) | `longman.smallerradiusforanimatreesandshrines` |
| 638 | [Soil Contrast](https://steamcommunity.com/sharedfiles/filedetails/?id=1561838700) | `wemd.soilcontrast` |
| 639 | [Det's Xenotypes for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3565987465) | `det.facialanimation` |
| 640 | [Harana chirps quietly](https://steamcommunity.com/sharedfiles/filedetails/?id=3264370565) | `amegakull.haranachirpsquietly` |
| 641 | [Medieval Backstories Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3170653412) | `grasstypefire.medieval.backstoriespatch` |
| 642 | [Normal Stone for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=2968173847) | `mman0385.abnormalstone` |
| 643 | [RimTraits - Vanilla Trait Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=2244594116) | `sierra.rt.vanillatraitcolors` |
| 644 | [Thumper - Phercore Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3497174635) | `bb.thumper.pherocore.desc.clarify` |
| 645 | [VE Helixien Gas Space Maps Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3262619470) | `ionfrigate12345.sos2vehelixienpatch` |
| 646 | [VFE - Empire: Frugal Nobles](https://steamcommunity.com/sharedfiles/filedetails/?id=3573280041) | `cn.vfeempirelessrequirement` |
| 647 | [VTE - Proxy Heat Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3439148119) | `ewolfy.vtepatch` |
| 648 | [[NL] Facial Animation - 1.7 Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3821248395) | `local.facialanimation.compat17` |
| 649 | [Reduce Skipdoor Distortion Effects](https://steamcommunity.com/sharedfiles/filedetails/?id=3111751800) | `rabiosus.reduceskipdoordistortioneffect` |
| 650 | [Hospitality: Vending Machines (Adaptive Storage Patch)](https://steamcommunity.com/sharedfiles/filedetails/?id=3319084133) | `katsukiri.hvendingmachineadaptivepatch` |
| 651 | [ReGrowth 2 World Map Beautification for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679302) | `noxilie.regrow.wmb.alphabiomes` |
| 652 | [ReGrowth 2 World Map Beautification for More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679624) | `noxilie.regrow.wmb.morevanillabiomes` |
| 653 | [Simple Immersion - Regrowth Rimclaw Rename](https://steamcommunity.com/sharedfiles/filedetails/?id=3789160274) | `nyr.siregrowthrimclaw` |
| 654 | [Vanilla plus loot expirence for ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3536236647) | `bb.generic.loot.exp.anc.urban.ruins` |
| 655 | [VFE Pirates Warcaskets Vacuum Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3543210594) | `why.vfe.warcasketsvacuumpatch` |
| 656 | [Warcasket weaponry hyperlinks](https://steamcommunity.com/sharedfiles/filedetails/?id=2819075870) | `d0b1k.warcaskethyperlinks` |
| 657 | [Cyanobot's Plant Gen Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3218426325) | `cyanobot.plantgenpatch` |
| 658 | [Phoebe Is Just A Chill Gal \| Progression: Storytellers Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3530139996) | `koberiddle.phoebeisjustachillgal` |
| 659 | [Warcasket Weapon Quality](https://steamcommunity.com/sharedfiles/filedetails/?id=3766043780) | `danzinagri.warcasketweaponquality` |
| 660 | [Anomaly Fleshbeasts or Sarg Fleshbeasts?](https://steamcommunity.com/sharedfiles/filedetails/?id=3234422589) | `ferny.fleshbeasts` |
| 661 | [Consistent Text](https://steamcommunity.com/sharedfiles/filedetails/?id=3578462306) | `ferny.consistenttext` |
| 662 | [Local Tweaks (sammael)](https://github.com/hugalafutro/LocalTweaks) | `sammael.localtweaks` |
| 663 | [Muzzle Flash for Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3451641816) | `stellicarsenal.mf.nihao` |
| 664 | [Wood's Muzzle Flash for Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3442021825) | `charlie.muzzle.flash.for.ancientruins` |
| 665 | [[CM] Ideology DLC Texture Upscale](https://steamcommunity.com/sharedfiles/filedetails/?id=2603637629) | `cm.ideology.upscaled` |
| 666 | [[sbz] Mini Band Node](https://steamcommunity.com/sharedfiles/filedetails/?id=3646711814) | `sbz.minibandnode` |
| 667 | [Alpha Mechs](https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158) | `sarg.alphamechs` |
| 668 | [[FSF] Complex Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=2069684319) | `frozensnowfox.complexjobs` |
| 669 | [Sleek Work Priorities](https://steamcommunity.com/sharedfiles/filedetails/?id=3764537806) | `squishyjellyfish.sleekworkpriorities` |
| 670 | [[sbz] Mini Mech Booster](https://steamcommunity.com/sharedfiles/filedetails/?id=3547537902) | `sbz.minimechbooster` |
| 671 | [[sbz] Mini Wastepack Atomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3668590852) | `sbz.miniwastepackatomizer` |
| 672 | [Childrens Drawings](https://steamcommunity.com/sharedfiles/filedetails/?id=2918817041) | `zylle.childrensdrawings` |
| 673 | [FEB's Colored Gravship Lights](https://steamcommunity.com/sharedfiles/filedetails/?id=3577698779) | `feb.coloredgravlights` |
| 674 | [Injury Texture Variety](https://steamcommunity.com/sharedfiles/filedetails/?id=3561610873) | `keshash.injurytextures` |
| 675 | [Prothesis Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3236905283) | `prothesis.texture` |
| 676 | [Vanilla Genes Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=2905707100) | `redmattis.vanillagenesrebalanced` |
| 677 | [Vanilla Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2748834409) | `neronix17.retexture.vanillahair` |
| 678 | [Vanilla Pawns Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2275310562) | `neronix17.hd.pawns` |
| 679 | [EyeGenes3](https://steamcommunity.com/sharedfiles/filedetails/?id=3745223213) | `lucius.eyegenes3` |
| 680 | [Character Development](https://steamcommunity.com/sharedfiles/filedetails/?id=3770112044) | `ferny.characterdevelopment` |
| 681 | [Vanilla Experimentals for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3753978140) | `sd.fa.movingvanillaeyes` |
| 682 | [Vanilla Upscaled Floors](https://steamcommunity.com/sharedfiles/filedetails/?id=3524080368) | `adagio.vanilla.upscaled.floors` |
| 683 | [Vanilla Upscaled Terrain](https://steamcommunity.com/sharedfiles/filedetails/?id=3525395044) | `adagio.vanilla.upscaled.terrain` |
| 684 | [World Map Enhanced (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3599967849) | `zal.worldmapenhanced` |
| 685 | [[CF] VRE - Lycanthrope Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3578135470) | `cf.lycanthrope` |
| 686 | [[CF] VRE - Sanguophage Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3165181669) | `vres.betterparts` |
| 687 | [[CF] VRE - Saurid Better Head Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3539916610) | `cf.vresaurid` |
| 688 | [[sbz] Rimsenal Bench Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2668988577) | `jgh.rimsenalbench` |
| 689 | [Japester's Upscaled Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3788774863) | `japester.upscaledtextures.4x` |
| 690 | [Nice Hands Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3302328100) | `andromeda.nicehands` |
| 691 | [Optional Icons for Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1966995052) | `proxyer.optionalicons4ai` |
| 692 | [Pumpkinlass Visible Pants Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3772689105) | `pumpkinlass.pantsretexture` |
| 693 | [Retextured! - RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=2979449586) | `soulretextured.rtfuse` |
| 694 | [Vanilla Textures Expanded - Variations](https://steamcommunity.com/sharedfiles/filedetails/?id=2493234474) | `vanillaexpanded.vtexvariations` |
| 695 | [ReBuild: Doors and Corners](https://steamcommunity.com/sharedfiles/filedetails/?id=3262718980) | `rebuild.cotr.doorsandcorners` |
| 696 | [ReBuild: Cell Door Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3598547615) | `rebuild.celldoorfix` |
| 697 | [Shelf Stack Logistics Reworked](https://steamcommunity.com/sharedfiles/filedetails/?id=3724884785) | `redmattis.shelflogistics` |
| 698 | [Urban Combat Framework 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3718778478) | `technofox.urbancombatframework` |
| 699 | [VFE - Settlers textures revamped](https://steamcommunity.com/sharedfiles/filedetails/?id=3685232860) | `regen.vfestexturesrevamped` |
| 700 | [Urban Combat Expanded - Outlander Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3718778795) | `outlander.expansionfox` |
| 701 | [Urban Combat Expanded - Pirate Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3722417568) | `pirate.expansionfox` |
| 702 | [Urban Combat Expanded - Royalty Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3725511411) | `royalty.expansionfox` |
| 703 | [Urban Combat Expanded - Tribal Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721300354) | `tribal.expansionfox` |
| 704 | [Loading Screen Repository](https://steamcommunity.com/sharedfiles/filedetails/?id=3567860027) | `ferny.loadingscreenrepository` |
| 705 | [Urban Combat Expanded - City Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3731623781) | `city.expansionfox` |
| 706 | [Vanilla Base Generation Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3209927822) | `vanillaexpanded.basegeneration` |
| 707 | [[FSF] FrozenSnowFox Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2893432492) | `frozensnowfox.frozensnowfoxtweaks` |
| 708 | [Mechanitor Encounters](https://steamcommunity.com/sharedfiles/filedetails/?id=3417287863) | `makinglovetomy.warqueenmechwife.allnightlong` |
| 709 | [MSS Tweaks and Fun](https://steamcommunity.com/sharedfiles/filedetails/?id=3379574408) | `mss.flavourpack` |
| 710 | [Better Architect Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3563882422) | `ferny.betterarchitect` |
| 711 | [Too Many Mods - Compats and Rebalances](https://steamcommunity.com/sharedfiles/filedetails/?id=3250762483) | `wara.toomanymods` |
| 712 | [Temporary Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3424105991) | `ferny.temporarypatches` |
| 713 | [Wild Crops, a fork of Wild Spawn](https://steamcommunity.com/sharedfiles/filedetails/?id=3776658466) | `somethingsomething.plantsandstuff` |
| 714 | [Xeva's Rimhair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3132223992) | `neronix17.retexture.xevarimhair` |
| 715 | [[sbz] EPOE Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2616058883) | `jgh.epoe` |
| 716 | [EPOE-Forked Prosthesis Eye Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3489953096) | `nyx.epoeforkedtex` |
| 717 | [Rimsenal Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2747368450) | `neronix17.retexture.rimsenalrimhair` |
| 718 | [Upscaled - Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3766152968) | `ntxt.vweupscale` |
| 719 | [Vanilla Chemfuel Expanded Retexture and Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=2951745069) | `nomadrw.retextureandrestylevanillachemfuelexpanded` |
| 720 | [Adjustments for Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3532050275) | `sd.fa.vteadjustments` |
| 721 | [Bedrooms are always better than Barracks](https://steamcommunity.com/sharedfiles/filedetails/?id=3533595687) | `redmattis.bedroomnotbarracks` |
| 722 | [ReBuild: Doors and Corners NO Perspective Doors](https://steamcommunity.com/sharedfiles/filedetails/?id=3584349796) | `st.rusik.rebuild.no.perspective.doors` |
| 723 | [Upscaled - VWE Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=3780787134) | `ntxt.upscaled.vwequickdraw` |
| 724 | [[CF] Anomaly Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3538584455) | `cf.anomalyupscaled2x` |
| 725 | [[CF] Bionic Icons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3239007545) | `bionicicons.hd` |
| 726 | [[CF] Odyssey Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3535506453) | `cf.odysseyupscaled2x` |
| 727 | [[LM] LusTech Vanilla Weapons Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3602372541) | `lusty.lustechvwr` |
| 728 | [[S16] Royalty Weapons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=2826282032) | `s16.rwhd` |
| 729 | [[sbz] Mini Mech Rechargers](https://steamcommunity.com/sharedfiles/filedetails/?id=3554922769) | `sbz.minimechrecharger` |
| 730 | [[sbz] WVC - Work Modes Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2913464579) | `jgh.wvcwvc` |
| 731 | [ATH's Retexture Female Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3145326932) | `anthitei.athsretexturefemalapparel.retexture` |
| 732 | [Biotech Mechanoid Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3164022710) | `el.biotechmechrt` |
| 733 | [Erin's Body Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2662457442) | `erin.body.texture` |
| 734 | [Gerrymon's Upscaled Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3276562906) | `gerrymon.uvt` |
| 735 | [Gerrymon's Misc Props: Graveyard](https://steamcommunity.com/sharedfiles/filedetails/?id=3780458386) | `gmmp.graveyard` |
| 736 | [Sexier Visage Masks](https://steamcommunity.com/sharedfiles/filedetails/?id=3638159640) | `gerrymon.sexymasks` |
| 737 | [Mechanoid Cluster Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3523231836) | `happycam.conditioncauserretexture` |
| 738 | [Van's Retexture : Mechanitor](https://steamcommunity.com/sharedfiles/filedetails/?id=2943977908) | `sirvan.mechanitorretexture` |
| 739 | [Van's Retexture : Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2922441211) | `sirvan.mwretextured` |
| 740 | [Van's Retexture : Steel](https://steamcommunity.com/sharedfiles/filedetails/?id=2955560866) | `sirvan.steelretexture` |
| 741 | [Vanilla Mechanoid Slag Chunks Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3228786091) | `auntydonna.mechanoidslagchunksretextured` |
| 742 | [VFE Pirates Custom Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3755883834) | `yourmother.vfepirates.retexture` |
| 743 | [Food Texture Variety - Babies](https://steamcommunity.com/sharedfiles/filedetails/?id=3366505975) | `goat.food.texture.variety.babies` |
| 744 | [Food Texture Variety - Beer](https://steamcommunity.com/sharedfiles/filedetails/?id=3361892196) | `goat.food.texture.variety.beer` |
| 745 | [Food Texture Variety - Chocolate](https://steamcommunity.com/sharedfiles/filedetails/?id=3362958345) | `goat.food.texture.variety.chocolate` |
| 746 | [Food Texture Variety - Nutrient Paste](https://steamcommunity.com/sharedfiles/filedetails/?id=3357299556) | `goat.food.texture.variety.paste` |
| 747 | [Food Texture Variety - Replacement](https://steamcommunity.com/sharedfiles/filedetails/?id=3366513037) | `goat.food.texture.variety.replacement` |
| 748 | [Food Texture Variety - Vanilla Expanded Cooking](https://steamcommunity.com/sharedfiles/filedetails/?id=3388883044) | `goat.food.texture.variety.vecooking` |
| 749 | [H.C_AnimalReskins_Complete2 (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600202032) | `hc.animalreskins.complete2` |
| 750 | [Pointed Elf Ears Refined](https://steamcommunity.com/sharedfiles/filedetails/?id=2886966525) | `hysila.refinedelfears` |
| 751 | [VFME - Caravan Packs!](https://steamcommunity.com/sharedfiles/filedetails/?id=3609057140) | `vfme.caravanpacks` |
| 752 | [Alpha Animals Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3536598972) | `ks.aaretextured` |
| 753 | [Charge Lance Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=3697662862) | `regen.chargelancerestyle` |
| 754 | [UI Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2978831421) | `katsudon.uiretexture` |
| 755 | [Consistent Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3586064345) | `ferny.consistenttextures` |
| 756 | [Pacing Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3613142246) | `ferny.pacingmanager` |
| 757 | [Architect Menu Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3697920753) | `mrk.architectmenuoptimizer` |
| 758 | [Facial Animation Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3790129900) | `astryl.facialanimationperformancepatch` |
<!-- MODLIST:END -->
