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
| Common Sense | "Clean before working" queues a sweep of the bed's room before every lie-down on a bed; it only skips pawns that need a tend right now, so a tended patient on medical bed rest (flu, plague, healing wounds) gets up to sweep each time it lies down again, and a doctor's tend fails while it is out of bed | Prefix on `OpportunisticTasks.Cleaning_Opportunity`: no sweep before `LayDown` while the pawn should seek medical rest; cleaning before ordinary sleep, bills and recreation unchanged |
| Designator Shapes + Better Architect Menu | Flood fill (e.g. a floor) runs through doors and fills the next room: Shapes only treats things in the architect category named `Structure` as barriers, and BAM moves `DoorBase` to `Ferny_Doors` | Shapes' private `FloodFill.IsStructure` also answers true for doors (`Building_Door` subclasses and anything in `Ferny_Doors`); only Shapes' flood fill calls it |
| Eternal Pawns | Personal tweak, not a fix: the memory and notes gizmo disappears while a pawn is your prisoner, because `IsPawnSavable` rejects prisoners | Adds the same gizmo for colony prisoners; the veteran pool is unchanged. Use its Pin button or the note is cleared at the next cleanup unless the pawn is a veteran |
| Eternal Pawns | Recycles faction leaders and warcasket pawns as ordinary visitors/raiders (kind rewritten, casket lost); ignores `MustBeCapableOfViolence` | Keeps leaders, leader kinds and warcasket wearers out of the veteran draw; honours the violence flag |
| Eternal Pawns | An issued veteran stays a free world pawn, so vanilla redresses it again in the same group: duplicate pawns in a trade caravan, the trader turned into a guard kind, Giddy-Up gives one rider two mounts and the caravan never arrives (starves) | Issued veterans leave WorldPawns like vanilla-redressed pawns |
| Flourish - Everyday Animations | The telescope pose uses one fixed world offset and facing (`JobAnimationWorker_Anchored`), set up for the telescope's normal art, but vanilla draws that art mirrored when the telescope is rotated West, so there the pawn stands on the wrong side of the eyepiece | When the target is drawn mirrored (`Graphic.WestFlipped`/`EastFlipped` for its rotation), the worker reads the anchor with x negated and East/West facing swapped; the shared def is never written (render threads) |
| Flourish - Everyday Animations | Visual tweak: in the telescope pose the pawn's body covers the telescope (buildings always draw below pawns), so the hands hold nothing visible | While the anchored pose runs, the telescope is drawn a second time just above the pawn and below Flourish's hands (mesh only, no second shadow) |
| Giddy-Up 2 | Dismounted caravan animals park where the rider stepped off, so the caravan never arrives or leaves | Keeps lord animals' travel/follow focus across `Dismount` |
| Hauler's Dream + Compositable Loadouts | CL's 1.7 `Item` has no `Def` (items hold several alternative defs), so HD logs "loadout keep API ... did not fully resolve" and stops shielding loadout items from "adopt all surplus": possible unload / re-fetch loop | Turns HD's shield back on and counts every loadout item whose `Defs` include the def (HD's own startup warning still prints once) |
| Hauler's Dream + Smart Medicine - Continued | HD protects Stock Up items only on stacks it has not tagged as its own haul, and the tag spreads to every inventory stack of a def the pawn hauls. A tagged stocked stack is partly unloaded, Stock Up fetches it back, and the stack's growing tag age raises "Cannot unload inventory" whatever the hours setting | Postfix on HD's `InventorySurplus.KeepCountOf` raises the keep count to the pawn's Stock Up count, the path HD also honours on tagged stacks |
| Hauler's Dream + Kanban Stockpile Continue | HD ignores Kanban's max stack size: its bulk pick-up counts each slot at the full stack limit and its unload drops the whole carried stack, so vending machines capped at 25 held 248 cheese; Kanban's "remove excess" hauls the surplus out, HD hauls it back (churn, HD "net-zero" warnings) | While HD measures storage space, slots of capped storage count at the cap; HD's unload onto a capped cell drops only what the cell may still take, the rest goes to the next cell |
| Kanban Stockpile Continue | With a similar stack limit, the accept check counts only stacks the item can merge with, but "remove excess" counts stacks per def. Same-def meals with different ingredient kinds cannot merge, so a capped fridge accepts a third fine meal stack, remove-excess carries it out, the next haul brings it back: a pawn shuttles the same meals between two storages | The accept check also counts every stored stack of the same def and stuff, like the removal does. Topping up a mergeable stack still works |
| Vanilla Ideology + Alpha Memes (heart extraction) | A ritual sacrifice of an innocent prisoner counts as "executed innocent prisoner", so with "Execution: horrible if innocent" every member hates the moral guide for holding the ideoligion's own ritual (-30). Alpha Memes' heart extraction also kills with Cut instead of ExecutionCut damage, so the death counts again as "responsible for the death of a prisoner" (-15) | While the killer is in a ritual, an innocent prisoner's death records no execution events and the death thoughts see execution damage. Guilty prisoners and executions outside rituals are unchanged |
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
| MSS Tweaks and Fun | Haunt portraits saved upside down on 1.7/Vulkan | Flips the rows before the PNG is encoded |
| MSS Tweaks and Fun | VFE Pirates Buckoneer culture patch targets `VFEP_Junkers/VFEP_Mercenaries` | Exact xpath remap to the `VFEP_Mercenaries` faction |
| Prisoner Realism + Cleaning Area Relaunched | Prisoners stand still in an endless Clean job loop when their room has filth inside Home but outside the Clean area: Prisoner Realism picks it, the Cleaning Area Relaunched driver skips it, the job ends at once and is handed out again | Prisoner cleaning only picks filth inside the Clean area |
| Pulse Weaponry (any weapon def with the same mistake) | `DV_PulseRifle` adds `CompProperties_EquippableAbilityReloadable` without `<comps Inherit="False">`, so it also keeps BaseWeapon's plain `CompEquippable`. Both comps save a verbTracker with the same load IDs: "Exception registering ... in loaded object directory" for every such weapon on load | At startup, drops the plain `CompEquippable` from every ThingDef that also has a `CompEquippable` subclass (logs the defs); the extra saved verbTracker is ignored on load |
| ReGrowth plant patches | Duplicate wild plant records abort the world-gen Landmarks / Mutators steps | Dedupes `BiomeDef.wildPlants` |
| RimPacts | A government in exile that leaves or is dismissed deletes the fallen faction's leader without a successor: "Faction leader for X is null" every session | A new leader is picked right away (and after load for any faction that should have one but doesn't), as vanilla does when a leader dies |
| RimPacts + vanilla gatherings | RimPacts makes the first spawned colonist the host of its expo party, whatever that pawn is doing; a host whose schedule forbids recreation (or who is asleep, starving, bleeding ...) leaves at once and vanilla ends the party: "The party has been called off." moments after it starts, with no reason | The host is picked by vanilla's own organizer rules (RimPacts' pick only when nobody qualifies), and the host of any party stays until it ends even when its schedule turns to work or sleep (it still leaves when asleep, starving, bleeding ...), and every gathering's called-off message names the reason ("...: the host X is downed.") |
| Rimsenal Faction Pack - Spacer | Second style comp on the mech commander helmet, Dresser, EndTable and FloodLight (duplicate load IDs) | Those conditionals skipped, for that mod only |
| Rimsenal - Security Pack (with LocalTweaks) | Vanilla `PlaceWorker_ShowTurretRadius` only looks for a `Verb_Shoot` / `Verb_Spray` verb and null-refs every frame while placing a turret whose gun has neither (incinerator and graser turrets use `Verb_ShootBeam`) | Prefix on `PlaceWorker_ShowTurretRadius.AllowsPlacing`: such guns draw the range / min range ring of their first ranged verb; LocalTweaks adds the placeworker to those turrets |
| Sensible Bed Ownership | `OwnedBed` rescans every colonist bed on every call; UI and mods call it 200k-520k times a minute (Circinus: 2.3-5.1% of frame, single calls up to 79 ms with 150 beds) | Remembers SBO's answer per pawn while nothing it depends on changed (assign/unassign/claim methods, SBO's bed list, preferred bed, map, bed still owned; max 250 ticks); every 64th hit is re-checked against SBO and a mismatch is logged. Works from the parallel render threads too (Capture Expansion asks it for every pawn lying in a bed: 95 sleeping animals halved FPS), and SBO's bed list scan runs under a lock (it threw "Collection was modified" in pawn rendering) |
| Sensible Bed Ownership | Reservation prefix null-refs on just-despawned beds | Skipped for beds with no map |
| Show Mod Updates + UI Rethemed - Main Bar | Visual tweak: the "N updated mods" button in the themed Menu has no icon (UIR only knows the vanilla options) | Matches Show Mod Updates' label in the active language (also with its "RimWorld updated" line after a game update, or that line alone) and gives it UIR's `arrow-clockwise` icon |
| Skilled Refueling | `GetRefuelDuration` caches one iterator field from whichever refuel job runs first, so the other job type (normal or `atomicFueling`, e.g. Rimsenal - Security Pack) throws `ArgumentException`; atomic refuels also ignore refuel speed | Prefix refreshes the cache for the current job; atomic refuels take 240 ticks divided by the pawn's refuel speed |
| Smarter Visitors + Hospitality | Its prefix on `Trigger_TicksPassed.ActivateOn` runs for every ticks trigger of a visitor lord, not only Hospitality's stay timer: also vanilla's 1200-tick `Trigger_TicksPassedWithoutHarm` that sends a group back to travelling after a fight. With "Visitors will not leave if needing medical rest" on, a hurt visitor pushes that deadline out 2500 ticks every time, so a group hurt while leaving stands at its defend point eating and sleeping forever | The prefix only handles `Trigger_TicksPassedAndOkayToLeave`; every other ticks trigger runs vanilla |
| Smart Medicine - Continued | Personal tweak, not a fix: the Stock Up picker only lists drugs, medicine and the vanilla hemogen pack, so VRE Sanguophage's animal, corpse and sanguophage hemogen packs and carried meals (plain Foods) cannot be stocked without the dev-only "Stock ANYTHING" list | Postfix on the picker's filter lambda (`Dialog_StockUp.<DoWindowContents>b__10_0`) also admits `VRE_HemogenPack_Animal`, `VRE_HemogenPack_Corpse`, `VRE_HemogenPack_Sanguophage`, `MealSurvivalPack` and `MealNutrientPackaged` (Rimsenal Spacer) |
| Smart Medicine - Continued | Stocked food-type items such as hemogen packs (preferability `DesperateOnly`) are dropped by the idle `JobGiver_DropUnusedInventory` in the Home area once the pawn has not used raw inventory food for 150000 ticks, then fetched again: its keep check only covers the drug loop | Prefix on `JobGiver_DropUnusedInventory.Drop` keeps anything the pawn is stocking up on |
| Smart Medicine - Continued | "Use nearby medicine (if same quality)" compares the ground medicine's pathfinding cost (walking ticks, about 10-13 a tile, from the medicine to the doctor plus to the patient) against the doctor-patient distance in tiles + 2 x the setting, so "6 spaces" meant about one tile: doctors kept using the patient's Stock Up medicine with a full medicine cabinet 4 tiles from the bed, and the patient then fetched a new one | Transpiler on `FindBestMedicine.Find` (with a prefix remembering doctor and patient): that one check uses the Manhattan distance in tiles from the medicine to the doctor plus to the patient; sorting and the time-until-death limit keep Smart Medicine's path cost |
| Stem's VWE Industrial Expansion, Fortification Industrial - Citadel | "No Verse.SoundDef named ..." load warnings and silent weapons: `Interact_Pistol` (VFE Settlers only), `VWEE_Shot_SawnOffRepeater` and `VFE_Shot_InfernoSpewer` (defined nowhere) | XML: pistols use `Interact_Autopistol`, the sawn-off repeater `Shot_BoltActionRifle`, the flame bunker `InfernoCannon_Fire` (`1.7/Patches/MissingSoundDefs.xml`) |
| Too Many Mods | 22 lowercase `Defs/thingDef[` xpaths (Rimsenal items) | Case fixed |
| Triumphant Research | Blank research-complete window with a per-frame index error | Shows the project's full unlock list |
| TD Enhancement Pack - Continued | Personal tweak, not a fix: the "Deteriorating" alert leaves out rotting corpses but lists every fresh one, so raid dead bury the real items | Postfix on `Alert_Deterioration.DeterioratingThings`: no corpses in the alert |
| UI Rethemed - Animals | Hovering an animal row in an unfolded group also lights the row at the same position in every other unfolded group (hover fades are keyed by the row's rect, and each group body draws its rows in its own local coordinates) | Prefix on `Motion.Hover`: its fade key uses the rect's screen position |
| UI Rethemed - Colonist Card + TD Enhancement Pack | Personal tweak, not a fix: TD's skill gain arrows only draw on the vanilla skill rows in the Bio tab | Postfix on `Glance.DrawSkill` runs TD's own `LabelLearning` over the right end of each card skill bar, so its up/down arrow settings and fade still apply |
| UI Rethemed - Inspector Tabs | Hover card rows with a value have a fixed height, so a long label (Vanilla Social Interactions Expanded's opinion thoughts are full sentences) overlaps its own value and the row below | A row that does not fit on one line wraps its label beside the value and grows; other rows keep UIR's drawing |
| UI Rethemed - Inspector Tabs | Gear tab vacuum resistance row flickers every rebuild between "Vacuum resist... 9..." and the full label and value, and its bar restarts its fill animation: the gauge measures its text widths in whatever font the last drawer left (its other gauges force Small first) but draws in Small | Forces Small around `GearTab.RefreshVacuumGauge`, restoring the previous font after; personal tweak on top: the row is relabelled "Vacuum res." so the bar keeps some width |
| UI Rethemed - Inspector Tabs + Full Armor - Hands and Feet | Gloves and boots never appear in the doll's Hands/Feet cells and hover as "Headgear": Full Armor moves every gloves/boots def to the Overhead layer (its `moveGlovesHeadgearLayer`), and UIR classifies by layer before body part, so they land in Extra | `GearTab.VanillaSlotFor`: an Overhead/EyeCover item covering hands or feet and no head, torso or leg group goes to the Hands/Feet cell |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the Gear tab overview orders its cards loadout, mass, stock-up, comfy temperature, combat | Transpiler on `GearTab.DrawStatsPanel` moves the combat card in front of the temperature card and the stock-up cards to the end: mass, combat, temperature, stock-up |
| UI Rethemed - Inspector Tabs (+ Packs Are Not Belts) | Personal tweak, not a fix: empty doll slot captions "Mechanitor", "Exoskeleton" and "Ammunition" (Tiny font) are too wide for the cell and wrap onto a second line | `GearTab.SlotLabel` returns "Mech.", "Exo." and "Ammo" for those layers |
| UI Rethemed - Main Bar (+ Heat Map) | Personal tweak, not a fix: the temperature readout only shows a hover card, and its face swallows every click, so it can no longer toggle the temperature overlay the way Heat Map's outdoor thermometer did | Left- or right-click on the temperature readout (text or tile) toggles the vanilla temperature overlay (`showTemperatureOverlay`, the flag Heat Map recolours and its key binding flips), with the checkbox on/off sounds |
| UI Rethemed - Notifications | Visual tweak: the game's transient top-left messages stay plain text outside UIR's notification column (its own `MessagesDoGUI` patch only shifts them sideways) | While a game is loaded and the column is bottom-right, draws each live message as a UIR `StatusRow` (the alert-row widget, UIR's own kind colours/icons) stacked above the alerts; left-click jumps / opens the quest, right-click dismisses with UIR's card click sound, hover highlights and shows UIR's own hover card beside the column at once, as its alerts do (header naming the message type, rule, full text) in the row's colour; messages live 30 s instead of 13 and a hovered one restarts its timer; with UIR's section headings on, the rows (sharing UIR's column width: their own wanted width, worked out as UIR's sections do, widens the whole column through a `HudColumn.Shared` postfix) sit under their own folding "Messages" heading (UIR's `SectionHeading`, list icon, count, red/yellow when a bad/warning message is live; folded state saved with UIR's other sections) |
| UI Rethemed - Quests + Quality Colors (Continued) | Visual tweak: quest reward tiles and their hover cards show item names without the quality colour (the labels reach the tiles untagged, and the hover card strips tags from its title). The market value pill beside the name takes half the tile and cuts the name short | Postfix on `RewardTiles.Of` finds each tile's item through its click closure, wraps the label in the item's Quality Colors colour and moves the market value to the line under the name (a stack count on that line is dimmed); such a label is drawn whole in its colour (prefix on `Theme.LabelTruncated`) and the hover card title gets the colour back after the strip (prefixes on `HoverCard.Region` / `ContentRegion` / `Register`). The value line also works without Quality Colors |
| Unburned + VEF | Drafted pawns holding reach melee weapons (halberd, pike, Mono lance) only auto-attack adjacent enemies: Unburned's prefix copies `JobDriver_Wait.CheckForAutoAttack` and skips the original, so VEF's reach transpiler on the original never runs | Transpiler on Unburned's prefix applies VEF's own `IsVanillaMeleeAttack` test in its copy |
| Useful Marks | Its two mark widgets in the rename dialog (label-in-world checkbox, colour square) sit at a fixed offset from the dialog bottom: beside the Title row when renaming a colonist, but on top of the last-name "Suggested..." button in the newborn naming dialog, which has no Title row | The widgets are only drawn when the dialog's bottom row is the Title row |
| Useful Marks | Its inspect pane prefix redraws the info card button for any single selected thing but only skips vanilla for pawns, so items and buildings get a second info card button (stacked on the first in vanilla, a second "i" in UI Rethemed's card header) | Its prefix only runs when the selection is a pawn |
| Vanilla | Pawns deep-saved in quests are built before the world exists (MechEnergy null-ref) | No needs while no world exists; saved needs load right after |
| Vanilla | Shuttle passengers walk off and the shuttle never leaves: `CompShuttle.CompTick` evicts (every 600 ticks) any pawn inside whose `despawnedTick` is older than 60000 and drops it from its lord, but that field is the pawn's last map exit, not its boarding time. Bestowing-ceremony escorts are redressed world pawns who visited years earlier, so on the first check after landing some of them were thrown out of the escort lord (the loop also removes while indexing, so every other pawn), walked off as lordless non-colonists, and the shuttle then waited forever for them after the ceremony | Postfix on `CompTransporter.Notify_ThingAdded` sets a boarding pawn's `despawnedTick` to now, so the one-day eviction counts time in the shuttle; `CompShuttle.CompTick` is fingerprinted (game 1.7.5000) |
| Vanilla | Personal tweak, not a fix: install and implant-removal operations show a grey placeholder square in the operations menu (no `uiIconThing`, no product) | Fills the icon once at startup for surgery recipes without one: the implant item they consume, else the item the hediff drops |
| Vanilla (found through Modern Needs Tab) | `MinifiedThing.LabelNoCount` null-refs once its contents were installed, so the haul-to-frame job report throws; Modern Needs Tab then turns its job-history capture off for the session | An empty minified thing uses its def label |
| Vanilla + VFE Pirates (Junkers) | Quest says "raid of roughly this size:" followed by nothing, then the raid comes anyway. `PawnGroupKindWorker_Normal.CanGenerateFrom` checks options against the full point total, but pawn picking caps each pawn at the faction's `maxPawnCostPerTotalPointsCurve`, so the melee-only Junker group (Hussars, 600 each) passes below ~5800 points and yields no pawns | Postfix on `CanGenerateFrom` also rejects a normal group when no option fits under that cap, so another group is rolled |
| Vanilla / unknown | Trade caravan whose trader has a non-trader pawn kind: dismiss does nothing, caravan never exits | A guard holding a trader kind counts as the Trader |
| Vanilla / unknown | Trade ship slave also saved as a world pawn loads twice | World copy dropped after load |
| Vanilla | Trade caravans and visitor groups arrive and leave at once "because of dangerous temperature" at a comfortable temperature: hypothermia/heatstroke stays frozen on a mothballed world pawn (mothballed pawns only age) and the 15% leave trigger fires when that pawn is reused, even a year later | While a world pawn is mothballed, its hypothermia, heatstroke and insect hypothermic slowdown recover at vanilla's off-map rate; caravan members (never mothballed) are unchanged |
| Vanilla Quests Expanded - Ancients | Personal tweak, not a fix: the pneumatic capsule arrival message does not say what arrived, and the items are hauled away before you can look | Transpiler on `Building_PneumaticTubeLaunchPort.Deliver` sends the message after the contents are known and lists up to 6 kinds with total counts |
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

759 mods in load order, RimWorld 1.7.5005 rev1082. Generated from ModsConfig.xml by `Tools/modlist.py`.

| # | Mod | Package ID |
|---:|---|---|
| 1 | [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) | `brrainz.harmony` |
| 2 | [Load Them Last](https://steamcommunity.com/sharedfiles/filedetails/?id=3714307386) | `ferny.loadthemlast` |
| 3 | [Loading Progress](https://steamcommunity.com/sharedfiles/filedetails/?id=3535481557) | `ilyvion.loadingprogress` |
| 4 | [WOWGAG's Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3797541313) | `wowgag.guiperformancepatch` |
| 5 | [Harmony 3 Strict Binding Compat (local)](https://github.com/hugalafutro/Harmony3StrictCompat) | `sammael.harmony3strictcompat` |
| 6 | Core (official) | `ludeon.rimworld` |
| 7 | Royalty (official) | `ludeon.rimworld.royalty` |
| 8 | Ideology (official) | `ludeon.rimworld.ideology` |
| 9 | Biotech (official) | `ludeon.rimworld.biotech` |
| 10 | Anomaly (official) | `ludeon.rimworld.anomaly` |
| 11 | Odyssey (official) | `ludeon.rimworld.odyssey` |
| 12 | [Cherry Picker](https://steamcommunity.com/sharedfiles/filedetails/?id=3521312241) | `owlchemist.cherrypicker` |
| 13 | [Fortified Features Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3498575851) | `aoba.framework` |
| 14 | [Vanilla Backgrounds Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2775017012) | `vanillaexpanded.backgrounds` |
| 15 | [Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359) | `adaptive.storage.framework` |
| 16 | [XML Extensions](https://steamcommunity.com/sharedfiles/filedetails/?id=2574315206) | `imranfish.xmlextensions` |
| 17 | [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013) | `oskarpotocki.vanillafactionsexpanded.core` |
| 18 | [EBSG Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3112549163) | `ebsg.framework` |
| 19 | [[AV] Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3237129936) | `veltaris.framework` |
| 20 | [Biomes! Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3709492514) | `biomesteam.coreframework` |
| 21 | [Elite Bionics Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=1665403571) | `v1024.ebframework` |
| 22 | [1trickPwnyta's Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2764625248) | `ideologypatch.1trickpwnyta` |
| 23 | [[1.6] Do Not Avoid Fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3303359387) | `antihydrogen.donotavoidfire` |
| 24 | [[CAP] Thunder Snow](https://steamcommunity.com/sharedfiles/filedetails/?id=3590107592) | `cap.thundersnow` |
| 25 | [[DG]Lovly Animal Cuddling](https://steamcommunity.com/sharedfiles/filedetails/?id=3823442009) | `local.animalcuddling` |
| 26 | [[DR] Dream's Damage Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3789796309) | `mjcg.drdamageindicators` |
| 27 | [[DR] Where's My Treasure](https://steamcommunity.com/sharedfiles/filedetails/?id=3559591864) | `mjcg.wheresmytreasure` |
| 28 | [[LFS] UI Rethemed - Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=3831602102) | `lazyfridaystudio.uirethemed.animals` |
| 29 | [[LFS] UI Rethemed - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=3830054994) | `lazyfridaystudio.uirethemed.architect` |
| 30 | [[LFS] UI Rethemed - Colonist Card](https://steamcommunity.com/sharedfiles/filedetails/?id=3828206639) | `lazyfridaystudio.uirethemed.colonistcard` |
| 31 | [[LFS] UI Rethemed - Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3832843406) | `lazyfridaystudio.uirethemed.core` |
| 32 | [[LFS] UI Rethemed - History](https://steamcommunity.com/sharedfiles/filedetails/?id=3830774158) | `lazyfridaystudio.uirethemed.history` |
| 33 | [[LFS] UI Rethemed - Inspector Tabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3825029855) | `lazyfridaystudio.uirethemed.inspectortabs` |
| 34 | [[LFS] UI Rethemed - Main Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3826865706) | `lazyfridaystudio.uirethemed.mainbar` |
| 35 | [[LFS] UI Rethemed - Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3827650605) | `lazyfridaystudio.uirethemed.notifications` |
| 36 | [[LFS] UI Rethemed - Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3829391880) | `lazyfridaystudio.uirethemed.quests` |
| 37 | [[LFS] UI Rethemed - Wildlife](https://steamcommunity.com/sharedfiles/filedetails/?id=3828731834) | `lazyfridaystudio.uirethemed.wildlife` |
| 38 | [[LTO] Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=2345493945) | `derekbickley.ltocolonygroupsfinal` |
| 39 | [[NL] Facial Animation - WIP](https://steamcommunity.com/sharedfiles/filedetails/?id=1635901197) | `nals.facialanimation` |
| 40 | [Genetic Heads Framework for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3498759997) | `sd.fa.geneticheadsframework` |
| 41 | [[Og] Automatic Power Switch](https://steamcommunity.com/sharedfiles/filedetails/?id=3031634496) | `og.automaticpowerswitch` |
| 42 | [[Og] Repair Your Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3513376486) | `og.repair.your.gear` |
| 43 | [[sbz] Bookcase](https://steamcommunity.com/sharedfiles/filedetails/?id=3789304104) | `sbz.bookcase` |
| 44 | [[sbz] Fridge](https://steamcommunity.com/sharedfiles/filedetails/?id=3486264784) | `sbz.neatstoragefridge` |
| 45 | [[sbz] Gravship Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3537905298) | `sbz.gravshipstorage` |
| 46 | [[sbz] Neat Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3416243474) | `sbz.neatstorage` |
| 47 | [[sbz] Workbench Shelf](https://steamcommunity.com/sharedfiles/filedetails/?id=3484671309) | `sbz.neatstorageworkbenchshelf` |
| 48 | [[UR] Better Filter for Storage, Clothes and Food Restrictions](https://steamcommunity.com/sharedfiles/filedetails/?id=3753101034) | `uranv.betterfilter` |
| 49 | [[XND] Memorable Auroras (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2270039395) | `mlie.xndmemorableauroras` |
| 50 | [[XND] Visible Pants](https://steamcommunity.com/sharedfiles/filedetails/?id=2264108215) | `xeonovadan.visiblepants` |
| 51 | [Absolute Legends: Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=2195744587) | `absolute.legends.hair` |
| 52 | [Adaptive Primitive Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3400037215) | `adaptive.primitivestorage` |
| 53 | [AdaptiveMealBill](https://steamcommunity.com/sharedfiles/filedetails/?id=3668761367) | `rabiosus.adaptivemealbill` |
| 54 | [Additional Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3692484664) | `ferny.additionalherobackgrounds` |
| 55 | [Advanced Battle Cloak](https://steamcommunity.com/sharedfiles/filedetails/?id=3598135811) | `darjeeling.battlecloak` |
| 56 | [AFU男士发型_Men's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2072106893) | `jintuzilamian.hairstyle.afumenshairstyles` |
| 57 | [Against all odds](https://steamcommunity.com/sharedfiles/filedetails/?id=3552336221) | `literallyfree.colonistreturnbuff` |
| 58 | [Alfred Ancient - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3579763743) | `ferny.alfredancient` |
| 59 | [All Ideoligions Are Fluid](https://steamcommunity.com/sharedfiles/filedetails/?id=2872901762) | `superniquito.allideoligionsarefluid` |
| 60 | [Allies Fight the Fleshmass Heart](https://steamcommunity.com/sharedfiles/filedetails/?id=3772519386) | `lg.fleshmassalliedtarget` |
| 61 | [Almost There! Fork](https://steamcommunity.com/sharedfiles/filedetails/?id=3515165298) | `duz.almosttherefork` |
| 62 | [Alpha Armoury](https://steamcommunity.com/sharedfiles/filedetails/?id=3622294464) | `sarg.alphaarmoury` |
| 63 | [Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677) | `sarg.alphabiomes` |
| 64 | [Alpha Implants](https://steamcommunity.com/sharedfiles/filedetails/?id=3275091331) | `sarg.alphaimplants` |
| 65 | [Alpha Memes](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814) | `sarg.alphamemes` |
| 66 | [Alpha Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021) | `sarg.alphaprefabs` |
| 67 | [Alpha Random](https://steamcommunity.com/sharedfiles/filedetails/?id=3642631969) | `sarg.alpharandom` |
| 68 | [Always Position Gravship on Landing](https://steamcommunity.com/sharedfiles/filedetails/?id=3738322550) | `ferny.alwayspositiongravshiponlanding` |
| 69 | [Always Start in Daytime](https://steamcommunity.com/sharedfiles/filedetails/?id=3323971974) | `ferny.alwaysstartindaytime` |
| 70 | [Amorphous style pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3382499324) | `xmb.amorphousstylepack.mo` |
| 71 | [ANDH - Animals Nuzzling Detects Horrors](https://steamcommunity.com/sharedfiles/filedetails/?id=3230195082) | `latta.petknowstrueyou` |
| 72 | [Animal Collab Project Vanilla-Style](https://steamcommunity.com/sharedfiles/filedetails/?id=2208467668) | `acpteam.acpvanillastyle` |
| 73 | [Animals Forage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667046888) | `mlie.animalsforage` |
| 74 | [Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1195427067) | `com.bymarcin.architecticons` |
| 75 | [Armors For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3787547731) | `bamba.allbambaarmors` |
| 76 | [Asteroid Mineral Scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3536585361) | `broms.asteroidmineralscanner` |
| 77 | [Auto-assign Inventories on Caravan arrival](https://steamcommunity.com/sharedfiles/filedetails/?id=2882784350) | `syrus.autoassigninventorycaravan` |
| 78 | [Autoclose Event Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3226920093) | `dingo.autocloseeventnotifications` |
| 79 | [AutoExtractGenes](https://steamcommunity.com/sharedfiles/filedetails/?id=2882834449) | `nibato.autoextractgenes` |
| 80 | [Automatic Foraging](https://steamcommunity.com/sharedfiles/filedetails/?id=3687750532) | `i.automaticforaging` |
| 81 | [Automatic Night Owl](https://steamcommunity.com/sharedfiles/filedetails/?id=2056012179) | `ben.automaticnightowl` |
| 82 | [Barbs And Moats](https://steamcommunity.com/sharedfiles/filedetails/?id=3601561731) | `sgtbruce.barbsandmoats` |
| 83 | [Basic Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3600443734) | `stefanlog.basictraits` |
| 84 | [Battery Capacity in Stat View](https://steamcommunity.com/sharedfiles/filedetails/?id=3651439731) | `linnun.batterycapacity` |
| 85 | [Bed Rest For Food Poisoning](https://steamcommunity.com/sharedfiles/filedetails/?id=2567901366) | `bedrestforfoodpoisoning.1trickponyta` |
| 86 | [Better Abandoned Settlements](https://steamcommunity.com/sharedfiles/filedetails/?id=3826665236) | `edybtt.betterabandonedsettlements` |
| 87 | [Better Beggars (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3006899215) | `mlie.betterbeggars` |
| 88 | [Better Context Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3696948139) | `mrk.bettercontextmenu` |
| 89 | [Better Distress Call](https://steamcommunity.com/sharedfiles/filedetails/?id=3305489753) | `nalzurin.betterdistresscall` |
| 90 | [Better Gene Inheritance](https://steamcommunity.com/sharedfiles/filedetails/?id=3046776238) | `redmattis.bettergeneinheritance` |
| 91 | [Better ground-penetrating scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3203274131) | `kikohi.bettergroundpenetratingscanner` |
| 92 | [Better Growth Moments](https://steamcommunity.com/sharedfiles/filedetails/?id=3642805464) | `arkymn.bettergrowthmoments` |
| 93 | [Better Linkable Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3577372705) | `ganja.betterlinkablecolors` |
| 94 | [Better Mechanoid Sorting](https://steamcommunity.com/sharedfiles/filedetails/?id=3730840942) | `redstonepanda.bettermechrank` |
| 95 | [Better Mod Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3636406008) | `mumei.bettermodmenu` |
| 96 | [Better Records Tab (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2359778562) | `mlie.betterrecordstab` |
| 97 | [Better Scroll Bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3579237751) | `dillyo.betterscrollbars` |
| 98 | [Better Traders Guild](https://steamcommunity.com/sharedfiles/filedetails/?id=3684587591) | `shunter.bettertradersguild` |
| 99 | [Better Trees](https://steamcommunity.com/sharedfiles/filedetails/?id=3539609975) | `chaoticenrico.bettertrees` |
| 100 | [Better Workbench Management - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819555998) | `falconne.bwm` |
| 101 | [Big Bed Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=3201556342) | `amch.bigbedbuff` |
| 102 | [Biosculpter Pod and Neural Supercharger Resize](https://steamcommunity.com/sharedfiles/filedetails/?id=2710513531) | `syrus.bpansresize` |
| 103 | [Birth Quality Lifespan Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3654799710) | `saltgin.birthqualitylifespanfix` |
| 104 | [Blindsight - Unified](https://steamcommunity.com/sharedfiles/filedetails/?id=3248249029) | `spencezilla.blindsightunified` |
| 105 | [Blindsight suffers no Unnatural Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=3258096951) | `emitbreaker.blindnessunnaturaldarkness` |
| 106 | [Block Unwanted Minutiae (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3278213153) | `mlie.blockunwantedminutiae` |
| 107 | [Blueprints - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819556044) | `nova.blueprints.waylith17` |
| 108 | [BuilderTweaksReplica](https://steamcommunity.com/sharedfiles/filedetails/?id=3521096349) | `rabiosus.buildertweaksreplica` |
| 109 | [Caravan Mood Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=2680751877) | `syrus.caravanmoodbuff` |
| 110 | [Change map edge limit](https://steamcommunity.com/sharedfiles/filedetails/?id=1546494565) | `kapitanoczywisty.changemapedge` |
| 111 | [Chester Choose - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3761449697) | `ferny.chesterchoose` |
| 112 | [Childhood Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3000770956) | `zylle.childboodbackstories` |
| 113 | [Choose Where To Land](https://steamcommunity.com/sharedfiles/filedetails/?id=3537970831) | `kearril.choosewheretoland` |
| 114 | [Chronos Pointer - Schedule Menu Enhancement](https://steamcommunity.com/sharedfiles/filedetails/?id=3408065390) | `coolnether123.chronospointer` |
| 115 | [Circinus Dev Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3771602203) | `astryl.circinusdevtools` |
| 116 | [Cleaning Area Relaunched](https://steamcommunity.com/sharedfiles/filedetails/?id=3586697885) | `cerule.cleaningarearelaunched` |
| 117 | [Colored deep resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3203269467) | `kikohi.coloreddeepresources` |
| 118 | [Common Sense](https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193) | `avilmask.commonsense` |
| 119 | [Compositable Loadouts](https://steamcommunity.com/sharedfiles/filedetails/?id=2679126859) | `wiri.compositableloadouts` |
| 120 | [Consciousness Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2913127696) | `vex.consciousness` |
| 121 | [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079) | `phoenix.continuebutton` |
| 122 | [Cook for Yourself](https://steamcommunity.com/sharedfiles/filedetails/?id=3766233150) | `lordfelix.cookforyourself` |
| 123 | [Corpse Description Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3639910228) | `ferny.corpsedescriptionfix` |
| 124 | [Corpse info](https://steamcommunity.com/sharedfiles/filedetails/?id=2913588675) | `arl85.corpseinfo` |
| 125 | [D12 Dungeon and Night Hairs HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3620959277) | `d12caedro.dynhairshd` |
| 126 | [Dark Psychic Rituals: The Following](https://steamcommunity.com/sharedfiles/filedetails/?id=3596468709) | `dprtf.darkpsychicrituals.sentinel` |
| 127 | [Deathrest Pauses Biological Age](https://steamcommunity.com/sharedfiles/filedetails/?id=2940569974) | `crykosity.deathrest` |
| 128 | [Default Ingredient Radius](https://steamcommunity.com/sharedfiles/filedetails/?id=2909706985) | `mlie.defaultingredientradius` |
| 129 | [Deselect Zone](https://steamcommunity.com/sharedfiles/filedetails/?id=3328706359) | `cheesex.deselectzone` |
| 130 | [Designations Tooltip](https://steamcommunity.com/sharedfiles/filedetails/?id=3743959668) | `cheatereater.designationstooltip` |
| 131 | [Designator Shapes](https://steamcommunity.com/sharedfiles/filedetails/?id=1235181370) | `merthsoft.designatorshapes` |
| 132 | [Det's Xenotypes - Avaloi](https://steamcommunity.com/sharedfiles/filedetails/?id=3199692643) | `det.avaloi` |
| 133 | [Det's Xenotypes - Brawnum](https://steamcommunity.com/sharedfiles/filedetails/?id=3429572581) | `det.brawnum` |
| 134 | [Det's Xenotypes - Boglegs](https://steamcommunity.com/sharedfiles/filedetails/?id=3146564944) | `det.boglegs` |
| 135 | [Det's Xenotypes - Buzzers](https://steamcommunity.com/sharedfiles/filedetails/?id=3545293786) | `det.buzzers` |
| 136 | [Det's Xenotypes - Half-foot](https://steamcommunity.com/sharedfiles/filedetails/?id=3530817307) | `det.halffoot` |
| 137 | [Det's Xenotypes - Keshig](https://steamcommunity.com/sharedfiles/filedetails/?id=3376864722) | `det.keshig` |
| 138 | [Det's Xenotypes - Stoneborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2888722722) | `det.stoneborn` |
| 139 | [Det's Xenotypes - Venators](https://steamcommunity.com/sharedfiles/filedetails/?id=3140248688) | `det.venators` |
| 140 | [Dignified Empire Names](https://steamcommunity.com/sharedfiles/filedetails/?id=3530589531) | `goosechoosesviolence.empirenames` |
| 141 | [Dlc collaboration - Void universe](https://steamcommunity.com/sharedfiles/filedetails/?id=3587277884) | `hailuan.voiduniverse` |
| 142 | [Don't Mix Cosmetic Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3621282829) | `wara.dontmixcosmeticgenes` |
| 143 | [Don't Say 100% Unless You Mean It!](https://steamcommunity.com/sharedfiles/filedetails/?id=2894636032) | `kathanon.donemeansdone` |
| 144 | [DragSelect](https://steamcommunity.com/sharedfiles/filedetails/?id=2599942235) | `telardo.dragselect` |
| 145 | [Dress for the Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607724833) | `mlie.dressfortheweather` |
| 146 | [Dubs Apparel Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2296697286) | `dubwise.dubsappareltweaks` |
| 147 | [Dune Style Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2993301551) | `dsp.halituisamaricanous` |
| 148 | [Dynamic Carrying Capacity (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2967228692) | `mlie.dynamiccarryingcapacity` |
| 149 | [Dynamic Effects Forge](https://steamcommunity.com/sharedfiles/filedetails/?id=3735443951) | `blues.forge` |
| 150 | [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506) | `zeracronius.dynamictradeinterface` |
| 151 | [ED-Embrasures (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3277482616) | `ganja.ed.embrasures` |
| 152 | [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092) | `mnmr.eprimereadouts` |
| 153 | [Erin Eccentric](https://steamcommunity.com/sharedfiles/filedetails/?id=2967829336) | `erin.storyteller` |
| 154 | [Erin's Baldur's Gate 3 Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3069933015) | `erin.bg3.hair` |
| 155 | [Erin's Hairstyles - Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=2361911135) | `erin.hairredux` |
| 156 | [Erin's Hairstyles 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2849477421) | `erin.hair2` |
| 157 | [Eternal Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669591999) | `helldan.eternalpawns` |
| 158 | [Evolving Enemy Strongholds](https://steamcommunity.com/sharedfiles/filedetails/?id=3738072776) | `wowgag.evolvingenemystrongholds` |
| 159 | [Expressive Appendages](https://steamcommunity.com/sharedfiles/filedetails/?id=3830976421) | `remi.expressiveappendages` |
| 160 | [Extra Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=1926923167) | `zylle.extraalerts` |
| 161 | [Faction Raid Cooldown (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3547098393) | `mlie.factionraidcooldown` |
| 162 | [Family Tree (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3828392274) | `sarah.familytree.continued` |
| 163 | [Features Gap Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3753339482) | `ferny.featuresgapfix` |
| 164 | [Fire At Will Gizmo - Evolving Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3770020686) | `zohryu.fireatwillgizmo.evolving` |
| 165 | [FirmamentFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3781872865) | `dorbo.firmamentfx` |
| 166 | [Fix Styled Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2957953663) | `kathanon.fixstyledblueprints` |
| 167 | [Float Sub-Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2864015430) | `kathanon.floatsubmenu` |
| 168 | [Flourish - Combat Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3793154978) | `astryl.flourish` |
| 169 | [FMOD Errors Begone](https://steamcommunity.com/sharedfiles/filedetails/?id=3799402443) | `zzz.fmoderrorsbegone` |
| 170 | [Food Poisoning Investigator](https://steamcommunity.com/sharedfiles/filedetails/?id=3697891858) | `leo.foodpoisoninginvestigator` |
| 171 | [Forgetful Mech Resurrection](https://steamcommunity.com/sharedfiles/filedetails/?id=3766269434) | `mrhydralisk.forgetfulmechresurrection` |
| 172 | [Fortifications - Medieval - Material](https://steamcommunity.com/sharedfiles/filedetails/?id=2501487160) | `aoba.fortress.medieval.material` |
| 173 | [Fortifications - Neolithic](https://steamcommunity.com/sharedfiles/filedetails/?id=2385960678) | `aoba.fortress.neolithic` |
| 174 | [Friendly Fire Tweaks (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3591533573) | `zal.friendlyfiretweaks` |
| 175 | [Full Armor - Hands and Feet [1.6]](https://steamcommunity.com/sharedfiles/filedetails/?id=3530977354) | `stevezero.fullarmorhandsfeet` |
| 176 | [Gene Bank Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3265067908) | `leoltron.genebankindicators` |
| 177 | [Geological Landforms](https://steamcommunity.com/sharedfiles/filedetails/?id=2773943594) | `m00nl1ght.geologicallandforms` |
| 178 | [Ghouls Prefer Twisted Meat](https://steamcommunity.com/sharedfiles/filedetails/?id=3230171961) | `doug.ghoulsprefertwistedmeat` |
| 179 | [Giddy-Up 2 - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861) | `memegoddess.giddyup` |
| 180 | [Glasma's Refugee Expectations](https://steamcommunity.com/sharedfiles/filedetails/?id=3785120443) | `glasma.refugeeexpectations` |
| 181 | [Go Explore!](https://steamcommunity.com/sharedfiles/filedetails/?id=1814100216) | `albion.goexplore` |
| 182 | [Gojis-Misc-Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3775114861) | `goji.gojismiscgenes` |
| 183 | [Goodwill Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=3695225114) | `bluebird.goodwillpreview` |
| 184 | [Gore Perfected](https://steamcommunity.com/sharedfiles/filedetails/?id=3814447022) | `tiepi.goreperfected` |
| 185 | [Gravship Crew Manifest Saver](https://steamcommunity.com/sharedfiles/filedetails/?id=3641927599) | `lornath.gravshipcrewmanifestsaver` |
| 186 | [Grenade Fix - Rearmed](https://steamcommunity.com/sharedfiles/filedetails/?id=797360572) | `dingo.grenadefixrearmed` |
| 187 | [Guns For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3782391721) | `bambaryla.allbambaguns` |
| 188 | [Hard Times: Hair and Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3092175321) | `botchjob.hthair` |
| 189 | [Hauler's Dream](https://steamcommunity.com/sharedfiles/filedetails/?id=3742459652) | `giwaffed.haulersdream` |
| 190 | [Heat Map (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2552838384) | `syrus.heatmap` |
| 191 | [Hive Protection](https://steamcommunity.com/sharedfiles/filedetails/?id=3536627722) | `jaxe.hiveprotection` |
| 192 | [Hunt for Me](https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720) | `arandomkiwi.huntforme` |
| 193 | [I Clearly Have Enough! (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2023661266) | `mlie.iclearlyhaveenough` |
| 194 | [I'm Off (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3251510002) | `zal.imoff` |
| 195 | [I'm Trying To Save You! (Pawn Dressing Rework)](https://steamcommunity.com/sharedfiles/filedetails/?id=3534863993) | `s4.imtryingtosaveyou` |
| 196 | [Ignore Idle Guests](https://steamcommunity.com/sharedfiles/filedetails/?id=2515048728) | `mlie.ignoreidleguests` |
| 197 | [ilyvion's Laboratory](https://steamcommunity.com/sharedfiles/filedetails/?id=3296362231) | `ilyvion.laboratory` |
| 198 | [Infestations Spawn in Darkness 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3551839891) | `koth.isid` |
| 199 | [Injured Carry](https://steamcommunity.com/sharedfiles/filedetails/?id=2413690575) | `haecriver.injuredcarry` |
| 200 | [Inspired Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2851390190) | `lm.inspiredconversion` |
| 201 | [Instant Caravan](https://steamcommunity.com/sharedfiles/filedetails/?id=3165744287) | `ferny.easycaravanformation` |
| 202 | [Integrated Creep Joiners](https://steamcommunity.com/sharedfiles/filedetails/?id=3233429182) | `grillmaster.integratedcreepjoiners` |
| 203 | [Interaction Bubbles](https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345) | `jaxe.bubbles` |
| 204 | [Ion Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532877485) | `zal.ionweaponry` |
| 205 | [IVF Equality](https://steamcommunity.com/sharedfiles/filedetails/?id=2922603310) | `kathanon.ivfequality` |
| 206 | [Just Ignore Me Passing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503627342) | `mlie.justignoremepassing` |
| 207 | [Just Put It Over There](https://steamcommunity.com/sharedfiles/filedetails/?id=2856471776) | `mlie.justputitoverthere` |
| 208 | [KanbanStockpileContinue](https://steamcommunity.com/sharedfiles/filedetails/?id=3776851378) | `igora.kanbanstockpilecontinue` |
| 209 | [Keep Converting](https://steamcommunity.com/sharedfiles/filedetails/?id=3461478214) | `linnun.keepconverting` |
| 210 | [Keyz Misc Resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3355560776) | `keyz182.keyzmiscresources` |
| 211 | [Keyz' Allow Utilities](https://steamcommunity.com/sharedfiles/filedetails/?id=3524716849) | `keyz182.keyzallowutilities` |
| 212 | [Kidnapped Pawns Die Less](https://steamcommunity.com/sharedfiles/filedetails/?id=3308076464) | `conit.kpdl` |
| 213 | [Knick Knacks - Let your colonists decorate!](https://steamcommunity.com/sharedfiles/filedetails/?id=3595196942) | `vaguelysexual.modpackageid.goeshere` |
| 214 | [Large Faction Bases (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3257781909) | `mlie.largefactionbases` |
| 215 | [Last Visited (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3507014727) | `zal.lastvisited` |
| 216 | [Leo - Events](https://steamcommunity.com/sharedfiles/filedetails/?id=3730168769) | `leo.eventsexpanded` |
| 217 | [Less Stupid Romance Attempt (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014603702) | `mlie.lessstupidromanceattempt` |
| 218 | [Letter Permanent Injury](https://steamcommunity.com/sharedfiles/filedetails/?id=2592535960) | `buggy.rimworld.letterpermanentinjury` |
| 219 | [Level Up!](https://steamcommunity.com/sharedfiles/filedetails/?id=1701592470) | `krafs.levelup` |
| 220 | [Linkables Info Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3212476920) | `deadmano.linkablesinfoexpanded` |
| 221 | [LiquidSFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3758773902) | `dorbo.watersfx` |
| 222 | [Little Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=3331281387) | `tyrannidae.littlecritters` |
| 223 | [Living Bonds](https://steamcommunity.com/sharedfiles/filedetails/?id=3708989445) | `bsandyh.livingbonds` |
| 224 | [Load in Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379) | `ferny.loadinbackground` |
| 225 | [Log Publisher from HugsLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2873415404) | `m00nl1ght.unofficialupdates.hugslogpublisher` |
| 226 | [Map Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=2800857642) | `m00nl1ght.mappreview` |
| 227 | [Mark of Manhood](https://steamcommunity.com/sharedfiles/filedetails/?id=3646943505) | `maux36.markofmanhood` |
| 228 | [Mech Recharger Standby Mode](https://steamcommunity.com/sharedfiles/filedetails/?id=2879340506) | `heremeus.mechrechargerstandby` |
| 229 | [Mechanoids dont beat fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3252095758) | `fxz.mechbeatfire` |
| 230 | [Megafauna](https://steamcommunity.com/sharedfiles/filedetails/?id=1055485938) | `spino.megafauna` |
| 231 | [Melee Weapons For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3726524693) | `bamba.allbambamelee.tiered` |
| 232 | [Milky Way](https://steamcommunity.com/sharedfiles/filedetails/?id=3773448562) | `andromeda.milkyway` |
| 233 | [Mine Sight](https://steamcommunity.com/sharedfiles/filedetails/?id=3769804600) | `rabiosus.minesight` |
| 234 | [Mo'Events (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2035143365) | `mlie.moevents` |
| 235 | [Mojave Mask - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3781130446) | `ferny.mojavemask` |
| 236 | [Mono Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2997812588) | `cat.monopack` |
| 237 | [More Childhood Nicknames](https://steamcommunity.com/sharedfiles/filedetails/?id=3688519717) | `river.childhoodnames` |
| 238 | [More Persona Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2863308112) | `arquebus.morepersonatraits` |
| 239 | [More Random Seeds](https://steamcommunity.com/sharedfiles/filedetails/?id=1501463043) | `jaxe.morerandomseeds` |
| 240 | [More Slavery Stuff](https://steamcommunity.com/sharedfiles/filedetails/?id=2896845138) | `garryflowers.moreslaverystuff` |
| 241 | [More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1931453053) | `zylle.morevanillabiomes` |
| 242 | [MutatorWorldIcons](https://steamcommunity.com/sharedfiles/filedetails/?id=3551904193) | `snobi.mutatorworldicons` |
| 243 | [Name Your Entities](https://steamcommunity.com/sharedfiles/filedetails/?id=3229905841) | `ferny.nameyourentities` |
| 244 | [Nice Short Female Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3316363751) | `andromeda.nicehairs` |
| 245 | [No Backer Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669069738) | `nobackerpawns.nobackerpawns` |
| 246 | [No Disabled Factions In Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=2892125637) | `kathanon.nodisabledfactions` |
| 247 | [No Hemogen Farm Medical Alert](https://steamcommunity.com/sharedfiles/filedetails/?id=2887053053) | `dark.nohemogenfarmmedicalalert` |
| 248 | [No Infestation On Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=3661608882) | `ialways1ose.noinfestationonbuildings` |
| 249 | [No Job Authors](https://steamcommunity.com/sharedfiles/filedetails/?id=2009825774) | `doug.nojobauthors` |
| 250 | [No Modlist on Loading](https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405) | `ferny.nomodlistonloading` |
| 251 | [No Summon Mech Threat Gizmo](https://steamcommunity.com/sharedfiles/filedetails/?id=2889317343) | `canon.nomechsummonergizmo` |
| 252 | [No Terrain Edge Fade (Hard Floors)](https://steamcommunity.com/sharedfiles/filedetails/?id=3753895045) | `fauxzyi.noterrainedgefade` |
| 253 | [No Version Warning](https://steamcommunity.com/sharedfiles/filedetails/?id=2599504692) | `mlie.noversionwarning` |
| 254 | [Nomad Friendly Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3616175831) | `jaeger972.nomadfreindlyquests` |
| 255 | [Non uno Pinata (don't drop items)](https://steamcommunity.com/sharedfiles/filedetails/?id=1778821244) | `avilmask.nonunopinata` |
| 256 | [Not My Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2870045856) | `vesper.notmyfault` |
| 257 | [Not Overhead Meal](https://steamcommunity.com/sharedfiles/filedetails/?id=2986525461) | `overheadmeal.patch` |
| 258 | [Now Playing](https://steamcommunity.com/sharedfiles/filedetails/?id=3754684545) | `dorbo.nowplaying` |
| 259 | [OgreStack](https://steamcommunity.com/sharedfiles/filedetails/?id=1447140290) | `ogre.ogrestack` |
| 260 | [Packs Are Not Belts](https://steamcommunity.com/sharedfiles/filedetails/?id=2509511179) | `turnovus.submod.backpacksarenotbelts` |
| 261 | [Pawn Editor Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113) | `segaswolf.pawneditor.fork` |
| 262 | [Pawn Name Variety (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3548290568) | `mlie.pawnnamevariety` |
| 263 | [Peer Pressure (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3605155621) | `densevoid.peerpressure` |
| 264 | [Persona Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3766728761) | `shunter.personaweaponsunbound` |
| 265 | [Perspective: Eaves (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3346964576) | `mlie.perspectiveeaves` |
| 266 | [Phase Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) | `det.phaseweaponry` |
| 267 | [Pillar Planner](https://steamcommunity.com/sharedfiles/filedetails/?id=3768069893) | `astryl.pillarplanner` |
| 268 | [Planning Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2877392159) | `scherub.planningextended` |
| 269 | [Plasma Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3070942578) | `cat.plasmapack` |
| 270 | [Playwright](https://steamcommunity.com/sharedfiles/filedetails/?id=3753005413) | `rokk.playwright` |
| 271 | [Pollution Spread](https://steamcommunity.com/sharedfiles/filedetails/?id=3692324370) | `turkler.pollutingfactions` |
| 272 | [Positive Connections](https://steamcommunity.com/sharedfiles/filedetails/?id=3003882069) | `cem.positiveconnections` |
| 273 | [Power Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=899714534) | `erdelf.powerindicators` |
| 274 | [Pregnancy Attitude](https://steamcommunity.com/sharedfiles/filedetails/?id=3530918349) | `carriedactyl.pregnancyattitude` |
| 275 | [Prioritized Kill Dev Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3533940586) | `prioritizedkilltool.programmerlily.com` |
| 276 | [Prisoner Realism](https://steamcommunity.com/sharedfiles/filedetails/?id=3760196312) | `legator.prisonerrealism` |
| 277 | [Progression: Soundtrack](https://steamcommunity.com/sharedfiles/filedetails/?id=3620726714) | `ferny.progressionsoundtrack` |
| 278 | [Proper Slave Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3770087153) | `kuchiki.properslavegear` |
| 279 | [Prosthetic No Missing Body Parts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2739055353) | `mlie.prostheticnomissingbodyparts` |
| 280 | [Pulse Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3077879089) | `detvisor.pulseweaponry` |
| 281 | [Pumpkin Hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=3783859385) | `pumpkin.pumpkinhairs2` |
| 282 | [Punch Button](https://steamcommunity.com/sharedfiles/filedetails/?id=2651392585) | `fuu.punchattack` |
| 283 | [Quality Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3513846773) | `dawnsglow.qualcolor` |
| 284 | [QualityBuilder Unofficial 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3512466087) | `hatti.qualitybuilder` |
| 285 | [Quest Generation Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3625994394) | `jaeger972.questgenerationtweaks` |
| 286 | [Questing Meme](https://steamcommunity.com/sharedfiles/filedetails/?id=2826539854) | `sirmashedpotato.questingmeme` |
| 287 | [Quick Stockpile Creation](https://steamcommunity.com/sharedfiles/filedetails/?id=1742151109) | `longwater.smartstockpilecreation` |
| 288 | [Raider Approach Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781897112) | `oldmanyoung.raiderapproachlagfix` |
| 289 | [Range Finder](https://steamcommunity.com/sharedfiles/filedetails/?id=1332119637) | `brrainz.rangefinder` |
| 290 | [Readable Numbers](https://steamcommunity.com/sharedfiles/filedetails/?id=3762441264) | `7h0m4s.rimworldreadablenumbers` |
| 291 | [Real Faction Guest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2886929245) | `mlie.realfactionguest` |
| 292 | [Relevant Stats In Description](https://steamcommunity.com/sharedfiles/filedetails/?id=2692669482) | `mlie.relevantstatsindescription` |
| 293 | [Remove Colonies Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=3745014294) | `ferny.removecolonieslimit` |
| 294 | [Remove Tutorial Button](https://steamcommunity.com/sharedfiles/filedetails/?id=781505663) | `merthsoft.removetutorialbutton` |
| 295 | [Replace Stuff - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526354009) | `memegoddess.replacestuff` |
| 296 | [ReplaceLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3417393194) | `ferny.replacelib` |
| 297 | [RescueeJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177170403) | `ogliss.rescueejoinsplus` |
| 298 | [Research-Based Raids Difficulty](https://steamcommunity.com/sharedfiles/filedetails/?id=3679008124) | `leo.researchbasedraidsdifficulty` |
| 299 | [Resource Delivery Helper](https://steamcommunity.com/sharedfiles/filedetails/?id=3696859196) | `ferny.resourcedeliveryhelper` |
| 300 | [Restrict Undrawn Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3629753146) | `ferny.restrictundrawnapparel` |
| 301 | [Reverse Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=858744731) | `brrainz.reversecommands` |
| 302 | [Right-Click Gizmo Indicator](https://steamcommunity.com/sharedfiles/filedetails/?id=3750529401) | `cheatereater.rightclickgizmoindicator` |
| 303 | [RimPacts - Diplomacy Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3762723122) | `wowgag.rimpacts` |
| 304 | [Rimpsyche - Personality Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3535112473) | `maux36.rimpsyche` |
| 305 | [RimSaves](https://steamcommunity.com/sharedfiles/filedetails/?id=1713367505) | `arandomkiwi.rimsaves` |
| 306 | [Rimsenal - Augmented Vanilla Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=849231601) | `rimsenal.evp` |
| 307 | [Rimsenal - Security Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=736139206) | `rimsenal.security` |
| 308 | [Rimsenal - Storytellers Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725952322) | `rimsenal.storyteller` |
| 309 | [Rimsenal Faction Pack - Federation](https://steamcommunity.com/sharedfiles/filedetails/?id=736172213) | `rimsenal.federation` |
| 310 | [Rimsenal Faction Pack - Feral](https://steamcommunity.com/sharedfiles/filedetails/?id=736207111) | `rimsenal.feral` |
| 311 | [Rimsenal Faction Pack - Spacer](https://steamcommunity.com/sharedfiles/filedetails/?id=3086137468) | `rimsenal.spacer` |
| 312 | [Rimsenal Style Pack - Techist](https://steamcommunity.com/sharedfiles/filedetails/?id=2661828028) | `rimsenal.techist` |
| 313 | [Rimsenal Style Pack - Urbworld](https://steamcommunity.com/sharedfiles/filedetails/?id=2908039338) | `rimsenal.urb` |
| 314 | [Rimsenal Xenotype Pack - Askbarn](https://steamcommunity.com/sharedfiles/filedetails/?id=2918812858) | `rimsenal.askbarn` |
| 315 | [Rimsenal Xenotype Pack - Harana](https://steamcommunity.com/sharedfiles/filedetails/?id=3228637524) | `rimsenal.harana` |
| 316 | [Rimsenal Xenotype Pack - Zohar](https://steamcommunity.com/sharedfiles/filedetails/?id=3504125251) | `rimsenal.zohar` |
| 317 | [Rimsenal: Murder diversified](https://steamcommunity.com/sharedfiles/filedetails/?id=725947920) | `rimsenal.core` |
| 318 | [RimTraits - General Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2206957172) | `sierra.rt.generaltraits` |
| 319 | [Ritual Size Attenuation](https://steamcommunity.com/sharedfiles/filedetails/?id=3262033797) | `delmaintweaks.ritualsizeattenuation` |
| 320 | [Romina Ready - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3736785369) | `ferny.rominaready` |
| 321 | [Roo's HD Hairstyles (continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3559870636) | `rooboid.hdhair.continued` |
| 322 | [Royalty Room Info (Satisfied Title and Upgrade Requirements)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600393808) | `jaeger972.royaltyroominfo` |
| 323 | [RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=728314182) | `ratys.rtfuse` |
| 324 | [Run and Hide (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2022480344) | `mlie.runandhide` |
| 325 | [Rusty's Foliage Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=3646973576) | `rustys.foliage.hairs` |
| 326 | [Satellite Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=3793766543) | `astryl.satelliteminimap` |
| 327 | [Save File Compression](https://steamcommunity.com/sharedfiles/filedetails/?id=3562729067) | `amch.savefilecompression` |
| 328 | [Save Storyteller Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3744418945) | `vc.savestorytellersettings` |
| 329 | [Scrollable Inspect Panes](https://steamcommunity.com/sharedfiles/filedetails/?id=2889151469) | `kathanon.scrollableinspectpanes` |
| 330 | [Seasonal Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2045114229) | `mlie.seasonalweather` |
| 331 | [Self Dyeing](https://steamcommunity.com/sharedfiles/filedetails/?id=2562859859) | `avilmask.selfdyeing` |
| 332 | [Sensible Bed Ownership](https://steamcommunity.com/sharedfiles/filedetails/?id=3328702448) | `sensiblebedownership.1trickpwnyta` |
| 333 | [Settlement Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=2240524945) | `dninemfive.settlementdescription` |
| 334 | [Shadow Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3797651319) | `antisodium.shadowfix` |
| 335 | [Show Buildable Material Count](https://steamcommunity.com/sharedfiles/filedetails/?id=3245101540) | `bp.showbuildablematerialcount` |
| 336 | [Show Known Techprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2920370783) | `spacemoth.showknowntechprints` |
| 337 | [Show Me Your Hands](https://steamcommunity.com/sharedfiles/filedetails/?id=2475965842) | `mlie.showmeyourhands` |
| 338 | [Show Mod Updates](https://steamcommunity.com/sharedfiles/filedetails/?id=3218690663) | `mlie.showmodupdates` |
| 339 | [Show Possible Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=3338920230) | `mlie.showpossiblematerials` |
| 340 | [Show Weapon Tallies](https://steamcommunity.com/sharedfiles/filedetails/?id=2901520677) | `kathanon.showweapontallies` |
| 341 | [Show work site faction relations](https://steamcommunity.com/sharedfiles/filedetails/?id=3487185451) | `riebi.showfactionrelations` |
| 342 | [Simple Leadership](https://steamcommunity.com/sharedfiles/filedetails/?id=3668307448) | `pb3n.simpleleadership` |
| 343 | [Simple sidearms](https://steamcommunity.com/sharedfiles/filedetails/?id=927155256) | `petetimessix.simplesidearms` |
| 344 | [SimpleCameraSetting](https://steamcommunity.com/sharedfiles/filedetails/?id=3232415388) | `ray1203.simplecamerasetting` |
| 345 | [Sin's Softer Hair Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=3790361865) | `sinner.softerhaircolors` |
| 346 | [Skilled Refueling](https://steamcommunity.com/sharedfiles/filedetails/?id=2873628478) | `mrhydralisk.skilledrefueling` |
| 347 | [Skilled Slaughtering](https://steamcommunity.com/sharedfiles/filedetails/?id=2873284405) | `mrhydralisk.skilledslaughtering` |
| 348 | [Smart Medicine - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526251680) | `memegoddess.smartmedicine` |
| 349 | [Smart Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=1504723424) | `sarg.smartspeed` |
| 350 | [Smarter Construction](https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773) | `dhultgren.smarterconstruction` |
| 351 | [Smarter Deconstruction and Mining (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3261302741) | `mlie.smarterdeconstructionandmining` |
| 352 | [Smarter Visitors](https://steamcommunity.com/sharedfiles/filedetails/?id=3012491521) | `mlie.smartervisitors` |
| 353 | [Sovs hairs and beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3368700799) | `sov.hairbeardandstuff` |
| 354 | [Spacer Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3247891820) | `det.spacerarsenal` |
| 355 | [Sparkling Worlds - Full Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=1123043922) | `albion.sparklingworlds.full` |
| 356 | [Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3386164757) | `cat.stellicarsenal` |
| 357 | [Stop, Drop, And Roll! [BAL]](https://steamcommunity.com/sharedfiles/filedetails/?id=2362707956) | `balistafreak.stopdropandroll` |
| 358 | [Storage Stats in Description](https://steamcommunity.com/sharedfiles/filedetails/?id=3740185419) | `ferny.storagestatsindescription` |
| 359 | [Style Change Anytime](https://steamcommunity.com/sharedfiles/filedetails/?id=2880145066) | `dra.stylechangeanytime` |
| 360 | [Style Tooltip Columns](https://steamcommunity.com/sharedfiles/filedetails/?id=3748051379) | `dorbo.styletooltipcolumns` |
| 361 | [SurgeryImproved](https://steamcommunity.com/sharedfiles/filedetails/?id=3233032071) | `darkshadow44.surgeryimproved` |
| 362 | [Tabula Rasa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) | `neronix17.toolbox` |
| 363 | [TD Enhancement Pack - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3525414162) | `memegoddess.tdpack` |
| 364 | [TDS Bug Fixes - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3529433984) | `memegoddess.tdsbugfixes` |
| 365 | [Text Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3773691016) | `ferny.texttool` |
| 366 | [The Sims Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3604588393) | `goji.thesimstraits` |
| 367 | [The Vanity Project - Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3143138787) | `thevanityproject.beards` |
| 368 | [Toggleable Shields](https://steamcommunity.com/sharedfiles/filedetails/?id=3261317430) | `owlchemist.toggleableshields` |
| 369 | [Tooltipped Long Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2303452428) | `reiquard.tooltippedlongnames` |
| 370 | [Trade Ships Drop Spot](https://steamcommunity.com/sharedfiles/filedetails/?id=1969732297) | `smashphil.dropspot` |
| 371 | [Trader Show Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3007612546) | `nachotoast.tradershowxenotype` |
| 372 | [Trait Rarity Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=1751884355) | `carnysenpai.traitraritycolors` |
| 373 | [Useful Marks](https://steamcommunity.com/sharedfiles/filedetails/?id=3506573327) | `andromeda.usefulmarks` |
| 374 | [Tribal Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=2879649038) | `shenanigans.tribalbackstories1.4` |
| 375 | [Triumphant Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3647618250) | `ferny.triumphantresearch` |
| 376 | [True Non-Senescence](https://steamcommunity.com/sharedfiles/filedetails/?id=3547377353) | `lunardawn.truenonsenescence` |
| 377 | [Ugly Together](https://steamcommunity.com/sharedfiles/filedetails/?id=2884233923) | `uglytogether.leeh` |
| 378 | [Ultimate Colonist Name Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3498692639) | `hol.ultcolnames` |
| 379 | [Unburned](https://steamcommunity.com/sharedfiles/filedetails/?id=3538336699) | `andromeda.unburned` |
| 380 | [Undraft After Tucking](https://steamcommunity.com/sharedfiles/filedetails/?id=2157495459) | `madarauchiha.undraftaftertucking` |
| 381 | [Unique Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3707263149) | `shunter.uniqueweaponsunbound` |
| 382 | [Universal Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3540066516) | `trigger.universalblueprints` |
| 383 | [Use Bedrolls - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3534169351) | `memegoddess.usebedrolls` |
| 384 | [Useless Hair (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3044216509) | `zal.uselesshair` |
| 385 | [Vanilla Achievements Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2288125657) | `vanillaexpanded.achievements` |
| 386 | [Vanilla Animals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2871933948) | `vanillaexpanded.vanillaanimalsexpanded` |
| 387 | [Vanilla Animals Expanded — Waste Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=2962126499) | `vanillaexpanded.vaewaste` |
| 388 | [Vanilla Anomaly Expanded - Insanity](https://steamcommunity.com/sharedfiles/filedetails/?id=3342412435) | `vanillaexpanded.vanomalyeinsanity` |
| 389 | [Vanilla Apparel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814987817) | `vanillaexpanded.vappe` |
| 390 | [Vanilla Apparel Expanded — Accessories](https://steamcommunity.com/sharedfiles/filedetails/?id=2521176396) | `vanillaexpanded.vaeaccessories` |
| 391 | [Vanilla Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3273371966) | `det.vanillaarsenal` |
| 392 | [Vanilla Backstories Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2861806869) | `vanillaexpanded.vanillabackstoriesexpanded` |
| 393 | [Vanilla Events Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742) | `vanillaexpanded.vee` |
| 394 | [Vanilla Factions Expanded - Empire](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380) | `oskarpotocki.vfe.empire` |
| 395 | [Vanilla Factions Expanded - Insectoids 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431) | `oskarpotocki.vfe.insectoid2` |
| 396 | [Vanilla Factions Expanded - Medieval 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874) | `oskarpotocki.vfe.medieval2` |
| 397 | [Vanilla Factions Expanded - Settlers](https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119) | `oskarpotocki.vanillafactionsexpanded.settlersmodule` |
| 398 | [Vanilla Factions Expanded - Tribals](https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283) | `oskarpotocki.vfe.tribals` |
| 399 | [Vanilla Furniture Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143) | `vanillaexpanded.vfecore` |
| 400 | [Vanilla Furniture Expanded - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=2608762624) | `vanillaexpanded.vfearchitect` |
| 401 | [Vanilla Furniture Expanded - Art](https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023) | `vanillaexpanded.vfeart` |
| 402 | [Vanilla Furniture Expanded - Factory](https://steamcommunity.com/sharedfiles/filedetails/?id=3686924415) | `vanillaexpanded.vfefactory` |
| 403 | [Vanilla Furniture Expanded - Farming](https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779) | `vanillaexpanded.vfefarming` |
| 404 | [Vanilla Furniture Expanded - Medical Module](https://steamcommunity.com/sharedfiles/filedetails/?id=1718191613) | `vanillaexpanded.vfemedical` |
| 405 | [Vanilla Furniture Expanded - Power](https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477) | `vanillaexpanded.vfepower` |
| 406 | [Vanilla Furniture Expanded - Production](https://steamcommunity.com/sharedfiles/filedetails/?id=1880253632) | `vanillaexpanded.vfeproduction` |
| 407 | [Vanilla Furniture Expanded - Props and Decor](https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149) | `vanillaexpanded.vfepropsanddecor` |
| 408 | [Vanilla Furniture Expanded - Security](https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007) | `vanillaexpanded.vfesecurity` |
| 409 | [Vanilla Furniture Expanded - Spacer Module](https://steamcommunity.com/sharedfiles/filedetails/?id=2028381079) | `vanillaexpanded.vfespacer` |
| 410 | [Vanilla Gravship Expanded - Chapter 1](https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606) | `vanillaexpanded.gravship` |
| 411 | [Vanilla Hair Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256) | `vanillaexpanded.vhe` |
| 412 | [Vanilla Helixien Gas Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2877699803) | `vanillaexpanded.helixiengas` |
| 413 | [Vanilla Ideology Expanded - Hats and Rags](https://steamcommunity.com/sharedfiles/filedetails/?id=2567387768) | `vanillaexpanded.viehar` |
| 414 | [Vanilla Ideology Expanded - Icons and Symbols](https://steamcommunity.com/sharedfiles/filedetails/?id=2552609458) | `vanillaexpanded.ideo.iconsandsymbols` |
| 415 | [Vanilla Ideology Expanded - Relics and Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2564895018) | `vanillaexpanded.ideo.relicsandartifacts` |
| 416 | [Vanilla Ideology Expanded - Sophian Style](https://steamcommunity.com/sharedfiles/filedetails/?id=3194606539) | `vanillaexpanded.ideo.sophianstyle` |
| 417 | [Vanilla Landmarks Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229) | `vanillaexpanded.vexploratione` |
| 418 | [Vanilla Loot Beams](https://steamcommunity.com/sharedfiles/filedetails/?id=3746929144) | `astryl.vanillalootbeams` |
| 419 | [Vanilla Nutrient Paste Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763) | `vanillaexpanded.vnutriente` |
| 420 | [Vanilla Plants Expanded - Succulents](https://steamcommunity.com/sharedfiles/filedetails/?id=2198652536) | `vanillaexpanded.vplantsesucculents` |
| 421 | [Vanilla Quests Expanded - Ancients](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) | `vanillaquestsexpanded.ancients` |
| 422 | [Vanilla Quests Expanded - The Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3411401573) | `vanillaquestsexpanded.generator` |
| 423 | [Vanilla Races Expanded - Custom Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2917311689) | `vanillaracesexpanded.customicons` |
| 424 | [Vanilla Races Expanded - Fungoid](https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053) | `vanillaracesexpanded.fungoid` |
| 425 | [Vanilla Races Expanded - Genie](https://steamcommunity.com/sharedfiles/filedetails/?id=2901424072) | `vanillaracesexpanded.genie` |
| 426 | [Vanilla Races Expanded - Highmate](https://steamcommunity.com/sharedfiles/filedetails/?id=2995385834) | `vanillaracesexpanded.highmate` |
| 427 | [Vanilla Races Expanded - Hussar](https://steamcommunity.com/sharedfiles/filedetails/?id=2893586390) | `vanillaracesexpanded.hussar` |
| 428 | [Vanilla Races Expanded - Lycanthrope](https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100) | `vanillaracesexpanded.lycanthrope` |
| 429 | [Vanilla Races Expanded - Phytokin](https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805) | `vanillaracesexpanded.phytokin` |
| 430 | [Vanilla Races Expanded - Pigskin](https://steamcommunity.com/sharedfiles/filedetails/?id=3164798809) | `vanillaracesexpanded.pigskin` |
| 431 | [Vanilla Races Expanded - Sanguophage](https://steamcommunity.com/sharedfiles/filedetails/?id=2963116383) | `vanillaracesexpanded.sanguophage` |
| 432 | [Vanilla Races Expanded - Saurid](https://steamcommunity.com/sharedfiles/filedetails/?id=2880990495) | `vanillaracesexpanded.saurid` |
| 433 | [Vanilla Races Expanded - Starjack](https://steamcommunity.com/sharedfiles/filedetails/?id=3531912428) | `vanillaracesexpanded.starjack` |
| 434 | [Vanilla Races Expanded - Waster](https://steamcommunity.com/sharedfiles/filedetails/?id=2983471725) | `vanillaracesexpanded.waster` |
| 435 | [Vanilla Recycling Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3155781848) | `vanillaexpanded.recycling` |
| 436 | [Vanilla Skills Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3400246558) | `vanillaexpanded.skills` |
| 437 | [Vanilla Social Interactions Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083) | `vanillaexpanded.vanillasocialinteractionsexpanded` |
| 438 | [Vanilla Storytellers Expanded - Perry Persistent](https://steamcommunity.com/sharedfiles/filedetails/?id=2149702069) | `vse.perrypersistent` |
| 439 | [Vanilla Temperature Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258) | `vanillaexpanded.temperature` |
| 440 | [Vanilla Traits Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655) | `vanillaexpanded.vanillatraitsexpanded` |
| 441 | [Vanilla Weapons Expanded - Bioferrite](https://steamcommunity.com/sharedfiles/filedetails/?id=3287469135) | `vanillaexpanded.vwebf` |
| 442 | [Vanilla Weapons Expanded - Coilguns](https://steamcommunity.com/sharedfiles/filedetails/?id=2375692535) | `vanillaexpanded.vwec` |
| 443 | [Vanilla Weapons Expanded - Grenades](https://steamcommunity.com/sharedfiles/filedetails/?id=2194472657) | `vanillaexpanded.vweg` |
| 444 | [Vanilla Weapons Expanded - Makeshift](https://steamcommunity.com/sharedfiles/filedetails/?id=2419690698) | `vanillaexpanded.vwems` |
| 445 | [Vanilla Weapons Expanded - Non-Lethal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918354) | `vanillaexpanded.vwenl` |
| 446 | [Vanilla Weapons Expanded - Tribal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918552) | `vanillaexpanded.vwetb` |
| 447 | [Vanometric Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3041187151) | `hlx.vanometricgenerator` |
| 448 | [Various Space Ship Chunk (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014616487) | `mlie.variousspaceshipchunk` |
| 449 | [Venerated Xenotype Flexibility (Revisited)](https://steamcommunity.com/sharedfiles/filedetails/?id=3586153375) | `dcszar.veneratedxenotypeflexibility` |
| 450 | [VFE Pack for Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3612525913) | `cantaloupetheclown.vanillafurnitureexpandedpack` |
| 451 | [Victor Veto - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3745138223) | `ferny.victorveto` |
| 452 | [Volt Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3551721422) | `det.voltweaponry` |
| 453 | [VV - New Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=3448458106) | `vvenchov.vvnewharvest` |
| 454 | [WanderJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177016016) | `ogliss.wanderjoinsplus` |
| 455 | [We Are United](https://steamcommunity.com/sharedfiles/filedetails/?id=3030804445) | `sl4vp0wer.weareunited` |
| 456 | [Weather Duration](https://steamcommunity.com/sharedfiles/filedetails/?id=2822836257) | `kathanon.weatherduration` |
| 457 | [What's Missing?](https://steamcommunity.com/sharedfiles/filedetails/?id=3231090162) | `revolus.whatsmissing` |
| 458 | [What's That Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2258431182) | `co.uk.epicguru.whatsthatmod` |
| 459 | [Who shot my leg off?](https://steamcommunity.com/sharedfiles/filedetails/?id=3491552121) | `tixiv.whoshotmylegoff` |
| 460 | [Wololoo - Better Conversion and Recruitment](https://steamcommunity.com/sharedfiles/filedetails/?id=3108763487) | `redmattis.betterconversion` |
| 461 | [Won hair_men](https://steamcommunity.com/sharedfiles/filedetails/?id=1879950403) | `won.hair` |
| 462 | [Won hair_women](https://steamcommunity.com/sharedfiles/filedetails/?id=1886150617) | `won2.hair` |
| 463 | [Work History](https://steamcommunity.com/sharedfiles/filedetails/?id=3727287885) | `lebek.workhistory` |
| 464 | [Xenotype Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3787666264) | `ceramic.xenotypebackstories` |
| 465 | [Xenotype Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832924427) | `lakinmanheim.xenotypedescriptions` |
| 466 | [Xenotype Summary](https://steamcommunity.com/sharedfiles/filedetails/?id=3710012135) | `zhuma.xenotypesummary` |
| 467 | [Xeva's Rimhair](https://steamcommunity.com/sharedfiles/filedetails/?id=705924057) | `orion.xevarimhair` |
| 468 | [[AV] Ancient Spacer Xenos](https://steamcommunity.com/sharedfiles/filedetails/?id=3241589828) | `veltaris.xenos` |
| 469 | [[AV] Kosmian Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3774474578) | `veltaris.kosmian` |
| 470 | [[AV] Mechanoid orbital traders](https://steamcommunity.com/sharedfiles/filedetails/?id=3172198798) | `veltaris.mechcrate` |
| 471 | [[AV] Mechtech](https://steamcommunity.com/sharedfiles/filedetails/?id=3339795890) | `veltaris.mechtech` |
| 472 | [[AV] Toxic Waste Disposer](https://steamcommunity.com/sharedfiles/filedetails/?id=3440190384) | `veltaris.wastecollector` |
| 473 | [[BTD] Gravship Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3575162262) | `btd.remix.gravshipblueprints` |
| 474 | [[ETF] Butchering Now Yields Hemogen!](https://steamcommunity.com/sharedfiles/filedetails/?id=3688485013) | `echotidefish.butcheringnowyieldshemogen` |
| 475 | [[LFS] UI Rethemed - Factions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832850311) | `lazyfridaystudio.uirethemed.factions` |
| 476 | [[LFS] UI Rethemed - Ideology](https://steamcommunity.com/sharedfiles/filedetails/?id=3834172773) | `lazyfridaystudio.uirethemed.ideology` |
| 477 | [[NL] Facial Animation - Experimentals](https://steamcommunity.com/sharedfiles/filedetails/?id=2581693737) | `nals.facialanimationexperimentals` |
| 478 | [Achtung!](https://steamcommunity.com/sharedfiles/filedetails/?id=730936602) | `brrainz.achtung` |
| 479 | [AFU女士发型_Women's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2020779666) | `jintuzilamian.hairstyle.afuwomenshairstyles` |
| 480 | [Alpha Props - Parks and Gardens](https://steamcommunity.com/sharedfiles/filedetails/?id=3146268928) | `sarg.alphapropsparks` |
| 481 | [Alpha Skills](https://steamcommunity.com/sharedfiles/filedetails/?id=3448953006) | `sarg.alphaskills` |
| 482 | [Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206) | `xmb.ancienturbanruins.mo` |
| 483 | [Biomes! Core](https://steamcommunity.com/sharedfiles/filedetails/?id=2038000893) | `biomesteam.biomescore` |
| 484 | [Capture Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787736781) | `ferny.captureexpansion` |
| 485 | [Circinus Performance Analyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=3773680130) | `astryl.circinus` |
| 486 | [Coilguns Addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3788730287) | `aqued.coilguns` |
| 487 | [Colony Manager Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=3310027356) | `ilyvion.colonymanagerredux` |
| 488 | [Decor into Styles](https://steamcommunity.com/sharedfiles/filedetails/?id=3771249138) | `ferny.decorintostyles` |
| 489 | [Expanded Prosthetics and Organ Engineering - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=1949064302) | `vat.epoeforked` |
| 490 | [FEB's Biopunk Livestock](https://steamcommunity.com/sharedfiles/filedetails/?id=3556058978) | `feb.biopunklivestock` |
| 491 | [Fix Multi Stack Hoppers](https://steamcommunity.com/sharedfiles/filedetails/?id=3534591980) | `zokradonh.supporthoppermultistacks` |
| 492 | [Flourish - Everyday Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3810518822) | `astryl.flourisheveryday` |
| 493 | [Fortifications - Industrial](https://steamcommunity.com/sharedfiles/filedetails/?id=2561619583) | `aoba.fortress.industrial` |
| 494 | [Gerrymon's Misc Props: Dungeon](https://steamcommunity.com/sharedfiles/filedetails/?id=3772571876) | `gmmp.dungeon` |
| 495 | [Gravship Fleet](https://steamcommunity.com/sharedfiles/filedetails/?id=3787239201) | `asunib.gravshipfleet` |
| 496 | [Gravship Raids](https://steamcommunity.com/sharedfiles/filedetails/?id=3767338163) | `sk.gravshipraids` |
| 497 | [Helixien Gas Brazier](https://steamcommunity.com/sharedfiles/filedetails/?id=2770815124) | `cixwow.helixienbrazier` |
| 498 | [Helixien Gas Production](https://steamcommunity.com/sharedfiles/filedetails/?id=2829208414) | `cixwow.helixiengasproduction` |
| 499 | [Hospitality (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3509486825) | `orion.hospitality` |
| 500 | [IMPACT - Combat Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3812370282) | `astryl.impact` |
| 501 | [Improved Map Search](https://steamcommunity.com/sharedfiles/filedetails/?id=3547866455) | `kathanon.groupedmapsearch` |
| 502 | [Info Card Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=3776660810) | `kaamalauppias.infocardplus` |
| 503 | [Junkie Knick Knacks](https://steamcommunity.com/sharedfiles/filedetails/?id=3607387774) | `ucp.junkieprops` |
| 504 | [More Phytokin Xenotypes](https://steamcommunity.com/sharedfiles/filedetails/?id=3813857865) | `pumpkin.morephytokinxenotypes` |
| 505 | [Nice Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3328729902) | `andromeda.nicehealthtab` |
| 506 | [Nice Research Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3773496046) | `andromeda.niceresearchtab` |
| 507 | [Permits: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3521089734) | `smxrez.permitsreexamined` |
| 508 | [Power: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3590186274) | `smxrez.powerreexamined` |
| 509 | [Reel's Expanded Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3237638097) | `reel.expanded.storage` |
| 510 | [Rimsenal - Hair pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725949967) | `rimsenal.hair` |
| 511 | [Searchable Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2928608119) | `kathanon.searchablemenus` |
| 512 | [Show Me Your Tools - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3774821567) | `meathax.showmeyourtools` |
| 513 | [Sleek Work Schedule Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3742689571) | `squishyjellyfish.nicescheduletab` |
| 514 | [Trait and Backstory Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2873494547) | `superniquito.traiticons` |
| 515 | [Universal Blueprints Alpha Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3573901897) | `trigger.universalblueprints.alphapack` |
| 516 | [Vanilla Armour Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814988282) | `vanillaexpanded.varme` |
| 517 | [Vanilla Chemfuel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2792917473) | `vanillaexpanded.vchemfuele` |
| 518 | [Vanilla Cooking Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519) | `vanillaexpanded.vcooke` |
| 519 | [Vanilla Expanded: Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3313700572) | `ferny.vanillaexpandedherobackgrounds` |
| 520 | [Vanilla Factions Expanded - Deserters](https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377) | `oskarpotocki.vfe.deserters` |
| 521 | [Vanilla Fishing Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942) | `vanillaexpanded.vcef` |
| 522 | [Vanilla Persona Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2826922787) | `vanillaexpanded.vpersonaweaponse` |
| 523 | [Vanilla Psycasts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) | `vanillaexpanded.vpsycastse` |
| 524 | [Vanilla Skills Rexamined](https://steamcommunity.com/sharedfiles/filedetails/?id=3235834179) | `gravenwitch.vsrexamined` |
| 525 | [Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360) | `vanillaexpanded.vwe` |
| 526 | [VFE - Insectoids 2 Deconstructable Infested Ship Chunks](https://steamcommunity.com/sharedfiles/filedetails/?id=3641480952) | `decinfshipchunkspatch.vfe.insectoid2` |
| 527 | [VSIE - Rational Trait Development](https://steamcommunity.com/sharedfiles/filedetails/?id=2916405546) | `stagz.vsierationaltraitdevelopment` |
| 528 | [Worksites Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3687071198) | `godsfathermixtape.worksitesexpanded` |
| 529 | [Zalcore Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3104631106) | `zal.prefabs` |
| 530 | [[AV] Mechanoid Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=3745387929) | `veltaris.mechstations` |
| 531 | [[BTD] Ship Pack: Community Designs](https://steamcommunity.com/sharedfiles/filedetails/?id=3832496468) | `btd.gbp.shippack.communitydesigns` |
| 532 | [[FSF] Advanced Bionics Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2006925330) | `frozensnowfox.advancedbionicsexpansion` |
| 533 | [Auto links](https://steamcommunity.com/sharedfiles/filedetails/?id=2059389912) | `automatic.autolinks` |
| 534 | [Biomes! Caverns](https://steamcommunity.com/sharedfiles/filedetails/?id=2969748433) | `biomesteam.biomescaverns` |
| 535 | [Biomes! Oasis](https://steamcommunity.com/sharedfiles/filedetails/?id=2538518381) | `biomesteam.oasis` |
| 536 | [Bradson's Main Button Icons (Forked + Expanded)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532359201) | `bs.mbifvte` |
| 537 | [Busywork](https://steamcommunity.com/sharedfiles/filedetails/?id=3775253009) | `andromeda.busywork` |
| 538 | [Cat's Boots and Gloves](https://steamcommunity.com/sharedfiles/filedetails/?id=3552813398) | `catlover366.bootsandgloves` |
| 539 | [Clean Command Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3688400757) | `leo.cleancommandbar` |
| 540 | [EPOE-Forked: Royalty DLC expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2008970276) | `vat.epoeforkedroyalty` |
| 541 | [Farming Hysteresis](https://steamcommunity.com/sharedfiles/filedetails/?id=2431191516) | `ilyvion.farminghysteresis` |
| 542 | [Fortification Industrial - Citadel](https://steamcommunity.com/sharedfiles/filedetails/?id=2887532326) | `aoba.fortress.industrial.citadel` |
| 543 | [Fortification Industrial - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=3093455596) | `aoba.fortress.frontier` |
| 544 | [Fortification Industrial -Nuclear Dawn](https://steamcommunity.com/sharedfiles/filedetails/?id=2733185331) | `aoba.fortress.industrial.nucleardawn` |
| 545 | [Genderbending Corrupted Obelisk](https://steamcommunity.com/sharedfiles/filedetails/?id=3350774891) | `abraxas.genderbending.corrupted.obelisk` |
| 546 | [Genetic Biotech for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317537) | `sd.fa.geneticheadsbiotech` |
| 547 | [Genetic Mods for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317734) | `sd.fa.geneticheadsmods` |
| 548 | [Hospitality + Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3789536584) | `fumblesneeze.guestbedgizmo` |
| 549 | [Hospitality - Invite to Stay](https://steamcommunity.com/sharedfiles/filedetails/?id=3783522506) | `gatoviejo.hospitality.invitetostay` |
| 550 | [Hospitality: Casino](https://steamcommunity.com/sharedfiles/filedetails/?id=2939292644) | `adamas.hospitalitycasino` |
| 551 | [Hospitality: Vending machines](https://steamcommunity.com/sharedfiles/filedetails/?id=3014885065) | `adamas.vendingmachines` |
| 552 | [Industrial Hardleather, Concrete and Cheese](https://steamcommunity.com/sharedfiles/filedetails/?id=3535926848) | `talias.vfehardleatherconcrete` |
| 553 | [Legendary Boss](https://steamcommunity.com/sharedfiles/filedetails/?id=3538894636) | `andromeda.legendaryboss` |
| 554 | [Modern Psycasts UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3745137736) | `astryl.modernpsycastsui` |
| 555 | [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) | `andromeda.nicebilltab` |
| 556 | [Nice Plants Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3685058533) | `andromeda.niceplantsmenu` |
| 557 | [Nicer Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3701712250) | `that0neperson.nhtprostheticsextended` |
| 558 | [Pawns Use Fire Extinguishers (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3612608024) | `mlie.pawnsusefireextinguishers` |
| 559 | [ReGrowth 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569) | `regrowth.botr.core` |
| 560 | [RimQuest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2263331727) | `mlie.rimquest` |
| 561 | [Useful Marks in Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3720856279) | `truemogician.usefulmarksincolonygroups` |
| 562 | [Vanilla Fishing Expanded - Fishing Treasures AddOn](https://steamcommunity.com/sharedfiles/filedetails/?id=2468543398) | `vanillaexpanded.vcefaddon` |
| 563 | [Vanilla Psycasts Expanded - Hemosage](https://steamcommunity.com/sharedfiles/filedetails/?id=2990596478) | `vanillaexpanded.vpe.hemosage` |
| 564 | [Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=2816938779) | `vanillaexpanded.vtexe.facialanims` |
| 565 | [Vanilla Weapons Expanded - Heavy Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) | `vanillaexpanded.vwehw` |
| 566 | [Vanilla Weapons Expanded - Laser](https://steamcommunity.com/sharedfiles/filedetails/?id=1989352844) | `vanillaexpanded.vwel` |
| 567 | [Vanilla Weapons Expanded - Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=1906780517) | `vanillaexpanded.vweq` |
| 568 | [A Dog Said... Animal Prosthetics 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3238353862) | `sambucher.adogsaidanimalprosthetics2` |
| 569 | [Biomes! Polluted Lands](https://steamcommunity.com/sharedfiles/filedetails/?id=3390196656) | `biomesteam.biomespollutedlands` |
| 570 | [Bionic icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1677616980) | `automatic.bionicicons` |
| 571 | [Bionic Irises for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3828692274) | `neztech.fabioniciris` |
| 572 | [Drafted Auto-Combat](https://steamcommunity.com/sharedfiles/filedetails/?id=3546270076) | `redmattis.autocombat` |
| 573 | [FirefliesTwo](https://steamcommunity.com/sharedfiles/filedetails/?id=3347026598) | `scurvyez.firefliestwo` |
| 574 | [Glitter Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3768428526) | `zal.glitterweaponry` |
| 575 | [Gulden Biome (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607066070) | `pphhyy.guldennew` |
| 576 | [More and Retextured Head Shapes for VTE for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3407049972) | `sd.fa.vte.moreheadshapes` |
| 577 | [More Prop Categories](https://steamcommunity.com/sharedfiles/filedetails/?id=3167021055) | `ferny.propscore` |
| 578 | [Nice Bill Tab - Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721023311) | `hicon.nicebilltabexpansion` |
| 579 | [Stem's VWE Industrial Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787566245) | `stem.vweindustrialexpansion` |
| 580 | [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) | `neronix17.tweaksgalore` |
| 581 | [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948) | `oskarpotocki.vfe.pirates` |
| 582 | [Vanilla Plants Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308522) | `vanillaexpanded.vplantse` |
| 583 | [Vanilla Textures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324) | `vanillaexpanded.vtexe` |
| 584 | [Food Texture Variety Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3354455179) | `goat.food.texture.variety.core` |
| 585 | [Vanilla Weapons Expanded - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918139) | `vanillaexpanded.vweft` |
| 586 | [WVC - Work Modes](https://steamcommunity.com/sharedfiles/filedetails/?id=2888380373) | `wvc.sergkart.biotech.moremechanoidsworkmodes` |
| 587 | [Altered Carbon 2: ReSleeved](https://steamcommunity.com/sharedfiles/filedetails/?id=2196278117) | `hlx.ultratechalteredcarbon` |
| 588 | [Decor into Props](https://steamcommunity.com/sharedfiles/filedetails/?id=3771248988) | `ferny.decorintoprops` |
| 589 | [Eltex Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532192464) | `zal.eltexweaponry` |
| 590 | [More Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=2707120862) | `tidal.morevanilla.textures` |
| 591 | [Progression: Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3361425712) | `ferny.progressionstorytellers` |
| 592 | [Unique Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=3776637622) | `shunter.uniquemeleeweapons` |
| 593 | [Vanilla Plants Expanded - More Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667) | `vanillaexpanded.vplantsemore` |
| 594 | [Vanilla Plants Expanded - Mushrooms](https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281) | `vanillaexpanded.vplantsemushrooms` |
| 595 | [Vanilla Races Expanded - Insector](https://steamcommunity.com/sharedfiles/filedetails/?id=3260509684) | `vanillaracesexpanded.insector` |
| 596 | [Alpha Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856) | `sarg.alphaanimals` |
| 597 | [Better Trees: More Djeeshka Like Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3543705507) | `maal.bettertreesmod` |
| 598 | [Vanilla Gravship Expanded - Chapter 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3799737423) | `vanillaexpanded.gravship2` |
| 599 | [Vanilla Storytellers Expanded - Igor Invader (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3582195822) | `zal.vseigorinvader` |
| 600 | [VV - New Harvest: Compatibility Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3570411438) | `octodecoy.vvnewharvest.compatpatches` |
| 601 | [Alpha Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502) | `sarg.alphagenes` |
| 602 | [Vanilla Ideology Expanded - Memes and Structures](https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500) | `vanillaexpanded.vmemese` |
| 603 | [[FSF] Better Exploration Loot](https://steamcommunity.com/sharedfiles/filedetails/?id=3526957922) | `frozensnowfox.betterexplorationloot` |
| 604 | [Alpha Genes Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3751293981) | `lucius.alphagenesfa` |
| 605 | [Alpha Genes Integrated](https://steamcommunity.com/sharedfiles/filedetails/?id=3538704272) | `kupa.alphagenesintegrated` |
| 606 | [Auto-Cast Specialist Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=3459647882) | `linnun.autocastspecialistcommands` |
| 607 | [Linkable Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3659197870) | `linnun.linkablegroups` |
| 608 | [Progression: Scenarios](https://steamcommunity.com/sharedfiles/filedetails/?id=3378384387) | `ferny.progressionscenarios` |
| 609 | [Reel's Insector Faction](https://steamcommunity.com/sharedfiles/filedetails/?id=3309022698) | `reel.insectorfaction` |
| 610 | [Tastier Vanilla Clothes](https://steamcommunity.com/sharedfiles/filedetails/?id=2808554143) | `al9000.tvc` |
| 611 | [Keyz Reference Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3689557041) | `keyz182.keyzreferencetools` |
| 612 | [Bullet Sounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3806470380) | `senilia.bulletsounds` |
| 613 | [Cherry picker scroll bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3781285156) | `lucius.cherryscroll` |
| 614 | [Ambience Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3835975372) | `neko.ambiencecore` |
| 615 | [Thunderstorm](https://steamcommunity.com/sharedfiles/filedetails/?id=3278460897) | `neko.thunderstorm` |
| 616 | [Shortcuts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503701770) | `mlie.shortcuts` |
| 617 | [Big Little Mod Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2710382569) | `daria40k.biglittlemodpatch` |
| 618 | [Muzzle Flash](https://steamcommunity.com/sharedfiles/filedetails/?id=2917732219) | `issaczhuang.muzzleflash` |
| 619 | [Turrets For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3800286808) | `bambaryla.allbambaturrets` |
| 620 | [Trystan Traveller's Shuttle Schematics](https://steamcommunity.com/sharedfiles/filedetails/?id=3655212016) | `trouperton.trystantravellersshuttleschematics` |
| 621 | [Help me build](https://steamcommunity.com/sharedfiles/filedetails/?id=3534699220) | `andromeda.helpmebuild` |
| 622 | [RimWorld: Living Waters](https://steamcommunity.com/sharedfiles/filedetails/?id=3834245888) | `patrickschwarzer.rivers` |
| 623 | [Realistic Darkness (Light)](https://steamcommunity.com/sharedfiles/filedetails/?id=1555355332) | `wemd.realisticdarknesslight` |
| 624 | [Float Menu Performance Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3784467506) | `vorsel.floatmenuperformanceoptimizer` |
| 625 | [Save Your Storage and Tick Every Minute](https://steamcommunity.com/sharedfiles/filedetails/?id=3795549633) | `nanaloveyuki.saveyourstorageandtickeveryminute` |
| 626 | [[WYD] Better Campsites](https://steamcommunity.com/sharedfiles/filedetails/?id=3546818262) | `wyr3d.bettercampsites` |
| 627 | [Art on Neolithic Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2556397158) | `dizzy.neolithicweaponart` |
| 628 | [Balanced Eclipse and Solar Flare - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=2929407615) | `skelly.neceros.balancedeclipseandsolarflare.forked` |
| 629 | [BrainShock](https://steamcommunity.com/sharedfiles/filedetails/?id=2227796764) | `nightkosh.brainshock` |
| 630 | [Dimorphic Neanderthals](https://steamcommunity.com/sharedfiles/filedetails/?id=3612381568) | `zoid.femneanderthals` |
| 631 | [DLG.Mutations behind](https://steamcommunity.com/sharedfiles/filedetails/?id=3637525997) | `dlg.mutation.behind` |
| 632 | [Drawn Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=2917791361) | `overpl.drawnspots` |
| 633 | [High Life Drug Cooking Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=3183941628) | `bigtortoise.drugcookingspeed` |
| 634 | [Less Rebuff](https://steamcommunity.com/sharedfiles/filedetails/?id=774543761) | `dingo.lessrebuff` |
| 635 | [More Descriptive Words and Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2180384246) | `hol.words` |
| 636 | [Cultural names: Stellic](https://steamcommunity.com/sharedfiles/filedetails/?id=3161247581) | `hawkwood.culturestellic` |
| 637 | [No Headstone Till Buried](https://steamcommunity.com/sharedfiles/filedetails/?id=2845934338) | `wanzi.noheadstone` |
| 638 | [Smaller radius for Anima Trees, Shrines and Animus Stones](https://steamcommunity.com/sharedfiles/filedetails/?id=2812513517) | `longman.smallerradiusforanimatreesandshrines` |
| 639 | [Soil Contrast](https://steamcommunity.com/sharedfiles/filedetails/?id=1561838700) | `wemd.soilcontrast` |
| 640 | [Det's Xenotypes for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3565987465) | `det.facialanimation` |
| 641 | [Harana chirps quietly](https://steamcommunity.com/sharedfiles/filedetails/?id=3264370565) | `amegakull.haranachirpsquietly` |
| 642 | [Medieval Backstories Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3170653412) | `grasstypefire.medieval.backstoriespatch` |
| 643 | [Normal Stone for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=2968173847) | `mman0385.abnormalstone` |
| 644 | [RimTraits - Vanilla Trait Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=2244594116) | `sierra.rt.vanillatraitcolors` |
| 645 | [Thumper - Phercore Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3497174635) | `bb.thumper.pherocore.desc.clarify` |
| 646 | [VE Helixien Gas Space Maps Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3262619470) | `ionfrigate12345.sos2vehelixienpatch` |
| 647 | [VFE - Empire: Frugal Nobles](https://steamcommunity.com/sharedfiles/filedetails/?id=3573280041) | `cn.vfeempirelessrequirement` |
| 648 | [VTE - Proxy Heat Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3439148119) | `ewolfy.vtepatch` |
| 649 | [[NL] Facial Animation - 1.7 Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3821248395) | `local.facialanimation.compat17` |
| 650 | [Reduce Skipdoor Distortion Effects](https://steamcommunity.com/sharedfiles/filedetails/?id=3111751800) | `rabiosus.reduceskipdoordistortioneffect` |
| 651 | [Hospitality: Vending Machines (Adaptive Storage Patch)](https://steamcommunity.com/sharedfiles/filedetails/?id=3319084133) | `katsukiri.hvendingmachineadaptivepatch` |
| 652 | [ReGrowth 2 World Map Beautification for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679302) | `noxilie.regrow.wmb.alphabiomes` |
| 653 | [ReGrowth 2 World Map Beautification for More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679624) | `noxilie.regrow.wmb.morevanillabiomes` |
| 654 | [Simple Immersion - Regrowth Rimclaw Rename](https://steamcommunity.com/sharedfiles/filedetails/?id=3789160274) | `nyr.siregrowthrimclaw` |
| 655 | [Vanilla plus loot expirence for ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3536236647) | `bb.generic.loot.exp.anc.urban.ruins` |
| 656 | [VFE Pirates Warcaskets Vacuum Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3543210594) | `why.vfe.warcasketsvacuumpatch` |
| 657 | [Warcasket weaponry hyperlinks](https://steamcommunity.com/sharedfiles/filedetails/?id=2819075870) | `d0b1k.warcaskethyperlinks` |
| 658 | [Cyanobot's Plant Gen Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3218426325) | `cyanobot.plantgenpatch` |
| 659 | [Phoebe Is Just A Chill Gal \| Progression: Storytellers Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3530139996) | `koberiddle.phoebeisjustachillgal` |
| 660 | [Warcasket Weapon Quality](https://steamcommunity.com/sharedfiles/filedetails/?id=3766043780) | `danzinagri.warcasketweaponquality` |
| 661 | [Anomaly Fleshbeasts or Sarg Fleshbeasts?](https://steamcommunity.com/sharedfiles/filedetails/?id=3234422589) | `ferny.fleshbeasts` |
| 662 | [Consistent Text](https://steamcommunity.com/sharedfiles/filedetails/?id=3578462306) | `ferny.consistenttext` |
| 663 | [Local Tweaks (sammael)](https://github.com/hugalafutro/LocalTweaks) | `sammael.localtweaks` |
| 664 | [Muzzle Flash for Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3451641816) | `stellicarsenal.mf.nihao` |
| 665 | [Wood's Muzzle Flash for Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3442021825) | `charlie.muzzle.flash.for.ancientruins` |
| 666 | [[CM] Ideology DLC Texture Upscale](https://steamcommunity.com/sharedfiles/filedetails/?id=2603637629) | `cm.ideology.upscaled` |
| 667 | [[sbz] Mini Band Node](https://steamcommunity.com/sharedfiles/filedetails/?id=3646711814) | `sbz.minibandnode` |
| 668 | [Alpha Mechs](https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158) | `sarg.alphamechs` |
| 669 | [[FSF] Complex Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=2069684319) | `frozensnowfox.complexjobs` |
| 670 | [Sleek Work Priorities](https://steamcommunity.com/sharedfiles/filedetails/?id=3764537806) | `squishyjellyfish.sleekworkpriorities` |
| 671 | [[sbz] Mini Mech Booster](https://steamcommunity.com/sharedfiles/filedetails/?id=3547537902) | `sbz.minimechbooster` |
| 672 | [[sbz] Mini Wastepack Atomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3668590852) | `sbz.miniwastepackatomizer` |
| 673 | [Childrens Drawings](https://steamcommunity.com/sharedfiles/filedetails/?id=2918817041) | `zylle.childrensdrawings` |
| 674 | [FEB's Colored Gravship Lights](https://steamcommunity.com/sharedfiles/filedetails/?id=3577698779) | `feb.coloredgravlights` |
| 675 | [Injury Texture Variety](https://steamcommunity.com/sharedfiles/filedetails/?id=3561610873) | `keshash.injurytextures` |
| 676 | [Prothesis Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3236905283) | `prothesis.texture` |
| 677 | [Vanilla Genes Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=2905707100) | `redmattis.vanillagenesrebalanced` |
| 678 | [Vanilla Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2748834409) | `neronix17.retexture.vanillahair` |
| 679 | [Vanilla Pawns Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2275310562) | `neronix17.hd.pawns` |
| 680 | [EyeGenes3](https://steamcommunity.com/sharedfiles/filedetails/?id=3745223213) | `lucius.eyegenes3` |
| 681 | [Character Development](https://steamcommunity.com/sharedfiles/filedetails/?id=3770112044) | `ferny.characterdevelopment` |
| 682 | [Vanilla Experimentals for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3753978140) | `sd.fa.movingvanillaeyes` |
| 683 | [Vanilla Upscaled Floors](https://steamcommunity.com/sharedfiles/filedetails/?id=3524080368) | `adagio.vanilla.upscaled.floors` |
| 684 | [Vanilla Upscaled Terrain](https://steamcommunity.com/sharedfiles/filedetails/?id=3525395044) | `adagio.vanilla.upscaled.terrain` |
| 685 | [World Map Enhanced (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3599967849) | `zal.worldmapenhanced` |
| 686 | [[CF] VRE - Lycanthrope Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3578135470) | `cf.lycanthrope` |
| 687 | [[CF] VRE - Sanguophage Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3165181669) | `vres.betterparts` |
| 688 | [[CF] VRE - Saurid Better Head Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3539916610) | `cf.vresaurid` |
| 689 | [[sbz] Rimsenal Bench Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2668988577) | `jgh.rimsenalbench` |
| 690 | [Japester's Upscaled Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3788774863) | `japester.upscaledtextures.4x` |
| 691 | [Nice Hands Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3302328100) | `andromeda.nicehands` |
| 692 | [Optional Icons for Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1966995052) | `proxyer.optionalicons4ai` |
| 693 | [Pumpkinlass Visible Pants Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3772689105) | `pumpkinlass.pantsretexture` |
| 694 | [Retextured! - RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=2979449586) | `soulretextured.rtfuse` |
| 695 | [Vanilla Textures Expanded - Variations](https://steamcommunity.com/sharedfiles/filedetails/?id=2493234474) | `vanillaexpanded.vtexvariations` |
| 696 | [ReBuild: Doors and Corners](https://steamcommunity.com/sharedfiles/filedetails/?id=3262718980) | `rebuild.cotr.doorsandcorners` |
| 697 | [ReBuild: Cell Door Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3598547615) | `rebuild.celldoorfix` |
| 698 | [Shelf Stack Logistics Reworked](https://steamcommunity.com/sharedfiles/filedetails/?id=3724884785) | `redmattis.shelflogistics` |
| 699 | [Urban Combat Framework 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3718778478) | `technofox.urbancombatframework` |
| 700 | [VFE - Settlers textures revamped](https://steamcommunity.com/sharedfiles/filedetails/?id=3685232860) | `regen.vfestexturesrevamped` |
| 701 | [Urban Combat Expanded - Outlander Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3718778795) | `outlander.expansionfox` |
| 702 | [Urban Combat Expanded - Pirate Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3722417568) | `pirate.expansionfox` |
| 703 | [Urban Combat Expanded - Royalty Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3725511411) | `royalty.expansionfox` |
| 704 | [Urban Combat Expanded - Tribal Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721300354) | `tribal.expansionfox` |
| 705 | [Loading Screen Repository](https://steamcommunity.com/sharedfiles/filedetails/?id=3567860027) | `ferny.loadingscreenrepository` |
| 706 | [Urban Combat Expanded - City Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3731623781) | `city.expansionfox` |
| 707 | [Vanilla Base Generation Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3209927822) | `vanillaexpanded.basegeneration` |
| 708 | [[FSF] FrozenSnowFox Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2893432492) | `frozensnowfox.frozensnowfoxtweaks` |
| 709 | [Mechanitor Encounters](https://steamcommunity.com/sharedfiles/filedetails/?id=3417287863) | `makinglovetomy.warqueenmechwife.allnightlong` |
| 710 | [MSS Tweaks and Fun](https://steamcommunity.com/sharedfiles/filedetails/?id=3379574408) | `mss.flavourpack` |
| 711 | [Better Architect Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3563882422) | `ferny.betterarchitect` |
| 712 | [Too Many Mods - Compats and Rebalances](https://steamcommunity.com/sharedfiles/filedetails/?id=3250762483) | `wara.toomanymods` |
| 713 | [Temporary Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3424105991) | `ferny.temporarypatches` |
| 714 | [Wild Crops, a fork of Wild Spawn](https://steamcommunity.com/sharedfiles/filedetails/?id=3776658466) | `somethingsomething.plantsandstuff` |
| 715 | [Xeva's Rimhair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3132223992) | `neronix17.retexture.xevarimhair` |
| 716 | [[sbz] EPOE Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2616058883) | `jgh.epoe` |
| 717 | [EPOE-Forked Prosthesis Eye Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3489953096) | `nyx.epoeforkedtex` |
| 718 | [Rimsenal Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2747368450) | `neronix17.retexture.rimsenalrimhair` |
| 719 | [Upscaled - Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3766152968) | `ntxt.vweupscale` |
| 720 | [Vanilla Chemfuel Expanded Retexture and Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=2951745069) | `nomadrw.retextureandrestylevanillachemfuelexpanded` |
| 721 | [Adjustments for Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3532050275) | `sd.fa.vteadjustments` |
| 722 | [Bedrooms are always better than Barracks](https://steamcommunity.com/sharedfiles/filedetails/?id=3533595687) | `redmattis.bedroomnotbarracks` |
| 723 | [ReBuild: Doors and Corners NO Perspective Doors](https://steamcommunity.com/sharedfiles/filedetails/?id=3584349796) | `st.rusik.rebuild.no.perspective.doors` |
| 724 | [Upscaled - VWE Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=3780787134) | `ntxt.upscaled.vwequickdraw` |
| 725 | [[CF] Anomaly Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3538584455) | `cf.anomalyupscaled2x` |
| 726 | [[CF] Bionic Icons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3239007545) | `bionicicons.hd` |
| 727 | [[CF] Odyssey Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3535506453) | `cf.odysseyupscaled2x` |
| 728 | [[LM] LusTech Vanilla Weapons Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3602372541) | `lusty.lustechvwr` |
| 729 | [[S16] Royalty Weapons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=2826282032) | `s16.rwhd` |
| 730 | [[sbz] Mini Mech Rechargers](https://steamcommunity.com/sharedfiles/filedetails/?id=3554922769) | `sbz.minimechrecharger` |
| 731 | [[sbz] WVC - Work Modes Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2913464579) | `jgh.wvcwvc` |
| 732 | [ATH's Retexture Female Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3145326932) | `anthitei.athsretexturefemalapparel.retexture` |
| 733 | [Biotech Mechanoid Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3164022710) | `el.biotechmechrt` |
| 734 | [Erin's Body Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2662457442) | `erin.body.texture` |
| 735 | [Gerrymon's Upscaled Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3276562906) | `gerrymon.uvt` |
| 736 | [Gerrymon's Misc Props: Graveyard](https://steamcommunity.com/sharedfiles/filedetails/?id=3780458386) | `gmmp.graveyard` |
| 737 | [Sexier Visage Masks](https://steamcommunity.com/sharedfiles/filedetails/?id=3638159640) | `gerrymon.sexymasks` |
| 738 | [Mechanoid Cluster Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3523231836) | `happycam.conditioncauserretexture` |
| 739 | [Van's Retexture : Mechanitor](https://steamcommunity.com/sharedfiles/filedetails/?id=2943977908) | `sirvan.mechanitorretexture` |
| 740 | [Van's Retexture : Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2922441211) | `sirvan.mwretextured` |
| 741 | [Van's Retexture : Steel](https://steamcommunity.com/sharedfiles/filedetails/?id=2955560866) | `sirvan.steelretexture` |
| 742 | [Vanilla Mechanoid Slag Chunks Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3228786091) | `auntydonna.mechanoidslagchunksretextured` |
| 743 | [VFE Pirates Custom Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3755883834) | `yourmother.vfepirates.retexture` |
| 744 | [Food Texture Variety - Babies](https://steamcommunity.com/sharedfiles/filedetails/?id=3366505975) | `goat.food.texture.variety.babies` |
| 745 | [Food Texture Variety - Beer](https://steamcommunity.com/sharedfiles/filedetails/?id=3361892196) | `goat.food.texture.variety.beer` |
| 746 | [Food Texture Variety - Chocolate](https://steamcommunity.com/sharedfiles/filedetails/?id=3362958345) | `goat.food.texture.variety.chocolate` |
| 747 | [Food Texture Variety - Nutrient Paste](https://steamcommunity.com/sharedfiles/filedetails/?id=3357299556) | `goat.food.texture.variety.paste` |
| 748 | [Food Texture Variety - Replacement](https://steamcommunity.com/sharedfiles/filedetails/?id=3366513037) | `goat.food.texture.variety.replacement` |
| 749 | [Food Texture Variety - Vanilla Expanded Cooking](https://steamcommunity.com/sharedfiles/filedetails/?id=3388883044) | `goat.food.texture.variety.vecooking` |
| 750 | [H.C_AnimalReskins_Complete2 (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600202032) | `hc.animalreskins.complete2` |
| 751 | [Pointed Elf Ears Refined](https://steamcommunity.com/sharedfiles/filedetails/?id=2886966525) | `hysila.refinedelfears` |
| 752 | [VFME - Caravan Packs!](https://steamcommunity.com/sharedfiles/filedetails/?id=3609057140) | `vfme.caravanpacks` |
| 753 | [Alpha Animals Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3536598972) | `ks.aaretextured` |
| 754 | [Charge Lance Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=3697662862) | `regen.chargelancerestyle` |
| 755 | [UI Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2978831421) | `katsudon.uiretexture` |
| 756 | [Consistent Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3586064345) | `ferny.consistenttextures` |
| 757 | [Pacing Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3613142246) | `ferny.pacingmanager` |
| 758 | [Architect Menu Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3697920753) | `mrk.architectmenuoptimizer` |
| 759 | [Facial Animation Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3790129900) | `astryl.facialanimationperformancepatch` |
<!-- MODLIST:END -->
