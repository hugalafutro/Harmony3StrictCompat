# Harmony 3 Strict Binding Compat (local)

Local stop-gap fixes for a large RimWorld 1.7 mod list (Harmony 3, UnityStubs). Every fix is temporary: remove it
once the mod it covers is fixed upstream.

## Fixes

Each C# fix fingerprints the foreign method it touches (FNV-1a of the IL). When that mod updates, the log shows a
red `[Harmony3StrictCompat] ... changed` error so the fix can be re-checked. XML fixes use PatchOperationTest steps
that fail loudly the same way. `Tools/fpcheck.py` runs the same fingerprint check offline against the DLLs on disk
(`Tools/fpcheck.cmd` wraps it for a pre-launch shortcut: silent when nothing changed, otherwise a window listing
the changed methods per mod).

| Mod | Problem | Fix |
|---|---|---|
| Alpha Books | Ruined and burned books never crumble after reading: the 1.6 DLL runs its read results (`BookReadResult_DestroyBook`) only in its own `ABooks_Reading` job, but these are plain `Book`, read with vanilla `JobDriver_Reading` (the 1.5 DLL had a `ReadBook` postfix for that, 1.6 dropped it) | Postfix on `JobDriver_Reading.ReadBook` wraps the driver's finalizer: a read of a book in Alpha Books' `ruinedBooks` set that ends Succeeded destroys it before vanilla hauls it to storage; interrupted reads keep it. Skipped if Alpha Books ships its own `ReadBook` patch again |
| Alpha Books + Living Bonds | Ruined and burned books read through Living Bonds' "read book together" are left on the floor: its `JobDriver_FamilyReading` is a third reading driver that places the book in the cell after the read and runs no Alpha Books read result | Postfix on `JobDriver_FamilyReading.MakeNewToils` adds a finish action for the initiator: a job that ends Succeeded with a book in Alpha Books' `ruinedBooks` set destroys it; interrupted reads keep it |
| Vanilla (seen with Alpha Books ruined libraries) | "Read now" drops the book within a few ticks: the float menu does not check forbidden but the read toil fails on a forbidden book (the library loot spawns forbidden), and `JoyUtility.JoyTickCheckEnd` ends any read during a Work schedule slot because "Read now" never sets `ignoreJoyTimeAssignment` | Postfix on `Notify_Starting` of `JobDriver_Reading` and Alpha Books' `JobDriver_ReadAlphaBook`: a player-ordered read unforbids its book, as vanilla's Equip and Wear orders do, and ignores the recreation schedule, as vanilla's meditate-in-bed and visit-sick jobs do |
| Altered Carbon 2 + Too Many Mods - Compats and Rebalances | Pawns wearing the Cuirassier belt threw a NullReferenceException in `JobGiver_Reload_TryGiveJob_Patch.Postfix` on every job search: Too Many Mods swaps the belt's VEF shield (`CompShieldBubble`) for the vanilla `CompShield`, and Altered Carbon's postfix still reads the VEF comp | While the belt def has no VEF shield, Altered Carbon's postfix is skipped. It only adds the "charge the belt at a station" job, which the self-recharging vanilla shield never needs |
| Auto-Cast Specialist Commands + Det's Xenotypes - Stoneborn | Personal tweak, not a fix: Auto-Cast only knows the command abilities by name, so the Stoneborn Stonecall (self-only +30% mining and construction speed) is never auto-cast | On a mine job or when finishing a construction frame, hands `DV_Stonecall` to Auto-Cast's own toil injector, gated by its "Auto-Cast Mining Command" setting; right-click on the Stonecall gizmo toggles that setting and Auto-Cast's check icon shows it, as on its own commands |
| Basic Traits | "TraitHediffApplier: Processing pawn" debug spam | Log calls removed, logic kept |
| Better Workbench Management | Its bill options (output filter, count away, restrict workbench) overlap the Ideology "Use global style" dropdown at the bottom of the bill dialog | Lifts BWM's block 50 px whenever Ideology is active and the recipe makes a thing (vanilla classic-mode dropdown and Style Change Anytime's dropdown both sit there) |
| Better Workbench Management | Personal tweak, not a fix: the previous/next bill arrows overlap the top of the ingredient filter by a few pixels | Draws them 7 px higher |
| Biomes! Caverns | `BMT_ResourceBlueCrystal` "is equipment but has no verbs or tools" config error | XML: strips its equipment nodes (`1.7/Patches/BiomesCaverns_BlueCrystal.xml`) |
| Biomes! Caverns (Caveworld Flora) | Wild fungus colonies never regrow: `Mycelium.NotifyPlantRemoved` is never called, so a colony that once filled up stays "full" with no mushrooms | Each wild mushroom removed lowers its colony's count (floored at 0, colony kept); on load every count is reset to the live mushrooms |
| Biomes! Caverns (Caveworld Flora) | Wild fungi mouseover reads `Brightbells)`, growth missing | Re-inserts ` (NN% growth` in `FruitingBody.LabelMouseover` |
| Biosculpter Pod and Neural Supercharger Resize + Vanilla Textures Expanded | With the pod resized (e.g. 1x2), its blueprints, frames and architect button still show VTE's full 3x2 menu icon squeezed into the new shape: the resizer sets the icon before the game loads VTE's `uiIconPath`, which then overwrites it | Startup fix: when the resizer's graphic is in use, the pod's icon is its resized south texture, as the resizer intends |
| Busywork + LTO Colony Groups | With marker position "Upper" the job icon never shows on Colony Groups portraits: Busywork draws it only from a postfix on the vanilla colonist bar drawer, which LTO replaces | Runs Busywork's own portrait postfix from LTO's `DrawColonist` (icon in the portrait's top-left corner, as on the vanilla bar) |
| Common Sense | "Getting random element from empty collection" warnings, repeated on every stack split: "add meal ingredients" looks for a recipe for food that no recipe makes and never caches the miss | Skips that lookup for food no ingredient recipe produces |
| Common Sense | Eating job ends with an error (`CleanFilthToil` null-ref) when the filth a pawn cleans before eating vanishes first | Moves on to the next filth / the meal when the target is gone |
| Common Sense | "Clean before working" queues a sweep of the bed's room before every lie-down on a bed; it only skips pawns that need a tend right now, so a tended patient on medical bed rest (flu, plague, healing wounds) gets up to sweep each time it lies down again, and a doctor's tend fails while it is out of bed | Prefix on `OpportunisticTasks.Cleaning_Opportunity`: no sweep before `LayDown` while the pawn should seek medical rest; cleaning before ordinary sleep, bills and recreation unchanged |
| Designator Shapes + Better Architect Menu | Flood fill (e.g. a floor) runs through doors and fills the next room: Shapes only treats things in the architect category named `Structure` as barriers, and BAM moves `DoorBase` to `Ferny_Doors` | Shapes' private `FloodFill.IsStructure` also answers true for doors (`Building_Door` subclasses and anything in `Ferny_Doors`); only Shapes' flood fill calls it |
| Flourish - Everyday Animations | The telescope pose uses one fixed world offset and facing (`JobAnimationWorker_Anchored`), set up for the telescope's normal art, but vanilla draws that art mirrored when the telescope is rotated West, so there the pawn stands on the wrong side of the eyepiece | When the target is drawn mirrored (`Graphic.WestFlipped`/`EastFlipped` for its rotation), the worker reads the anchor with x negated and East/West facing swapped; the shared def is never written (render threads) |
| Flourish - Everyday Animations | Visual tweak: in the telescope pose the pawn's body covers the telescope (buildings always draw below pawns), so the hands hold nothing visible | While the anchored pose runs, the telescope is drawn a second time just above the pawn and below Flourish's hands (mesh only, no second shadow) |
| Giddy-Up 2 | Dismounted caravan animals park where the rider stepped off, so the caravan never arrives or leaves | Keeps lord animals' travel/follow focus across `Dismount` |
| Giddy-Up 2 | An animal rescuing a downed pawn can be picked as a mount: Giddy-Up stops its job at once, so it drops the patient and waits for a rider who may be far away | Adds vanilla `Rescue` to Giddy-Up's private busy-job set (`IsMountableUtility.BusyJobs`), so auto-mount skips the animal and the right-click menu shows it as busy until the rescue ends |
| Goji's Misc Genes + Living Bonds | `NullReferenceException` every ~40 s from `LivingBonds_Manager.GameComponentTick`: Goji's Parroting gene answers every interaction through `recipient.interactions`, which is null while the recipient is not spawned, and Living Bonds' baby babble interacts with a carried baby | Prefix on Goji's `InteractionWorker_Interacted_Patch.Postfix`: no parroting when the recipient has no interactions tracker; the original interaction is unchanged |
| Vanilla Social Interactions Expanded + Goji's Misc Genes | "Exception ticking <pawn> ... `KeyNotFoundException`: The given key '<pawn>' was not present in the dictionary" from `InteractionWorker_Teaching.Interacted`: VSIE stores a teacher's topic only when it weighs the interaction, and a pupil with Goji's Parroting gene answers with the same interaction, so the pupil "teaches" back without one. The exception aborts the first teacher's tick; logged once per pawn, then silent | A teaching interaction without a stored topic picks one the way VSIE does; with nothing to teach it ends quietly |
| Hauler's Dream | A bulk-hauled load goes straight back to the storage it came from ("bulk-hauled 6 times ... net-zero", "WorkGiver_HaulGeneral ... yielded no actual job", food left in a lower-priority storage instead of the fridge). HD ignores a pawn's own "on its way" claim only while the pawn carries the load, but its test for a load in the inventory (`StorageCommitments.IsDelivering`) compares `Thing.ParentHolder` with the inventory's `ThingOwner` and is never true. A haul that claims all the free room of its destination (Kanban cap, nearly full feeder / fridge / hopper / log pile) then finds it full of its own claim at unload, and the search falls through to the next priority | Counts a load in the pawn's own inventory as delivering |
| Hauler's Dream + Compositable Loadouts | CL's 1.7 `Item` has no `Def` (items hold several alternative defs), so HD logs "loadout keep API ... did not fully resolve" and stops shielding loadout items from "adopt all surplus": possible unload / re-fetch loop | Turns HD's shield back on and counts every loadout item whose `Defs` include the def (HD's own startup warning still prints once) |
| Hauler's Dream + Smart Medicine - Continued | HD protects Stock Up items only on stacks it has not tagged as its own haul, and the tag spreads to every inventory stack of a def the pawn hauls. A tagged stocked stack is partly unloaded, Stock Up fetches it back, and the stack's growing tag age raises "Cannot unload inventory" whatever the hours setting | Postfix on HD's `InventorySurplus.KeepCountOf` raises the keep count to the pawn's Stock Up count, the path HD also honours on tagged stacks |
| Hauler's Dream + Kanban Stockpile Continue | HD ignores Kanban's max stack size: its bulk pick-up counts each slot at the full stack limit and its unload drops the whole carried stack, so vending machines capped at 25 held 248 cheese; Kanban's "remove excess" hauls the surplus out, HD hauls it back (churn, HD "net-zero" warnings) | While HD measures storage space, slots of capped storage count at the cap; HD's unload onto a capped cell drops only what the cell may still take, the rest goes to the next cell. HD's space count also follows Kanban's refill threshold and its similar stack limit per storage (counting it per cell made HD pick up cellar hay for two linked, already-full animal feeders and drop it straight back) |
| AdaptiveMealBill + Hauler's Dream | HD batch cooking replaces the bill job, so AMB loses the meal kind it picked, and HD splits the batch by AMB's placeholder recipe (any raw food), so a rep can be mostly meat with a little veg; AMB then logs "Could not recover the meal recipe from the job or its ingredients." and cooks the placeholder's product | When AMB's own recovery finds nothing, picks the veg-only or meat-only recipe if every ingredient fits it, else the mixed one |
| Kanban Stockpile Continue | With a similar stack limit, the accept check counts only stacks the item can merge with, but "remove excess" counts stacks per def. Same-def meals with different ingredient kinds cannot merge, so a capped fridge accepts a third fine meal stack, remove-excess carries it out, the next haul brings it back: a pawn shuttles the same meals between two storages | The accept check also counts every stored stack of the same def and stuff, like the removal does. Topping up a mergeable stack still works |
| Vanilla Ideology + Alpha Memes (heart extraction) | A ritual sacrifice of an innocent prisoner counts as "executed innocent prisoner", so with "Execution: horrible if innocent" every member hates the moral guide for holding the ideoligion's own ritual (-30). Alpha Memes' heart extraction also kills with Cut instead of ExecutionCut damage, so the death counts again as "responsible for the death of a prisoner" (-15) | While the killer is in a ritual, an innocent prisoner's death records no execution events and the death thoughts see execution damage. Guilty prisoners and executions outside rituals are unchanged |
| Hospitality | Guests with a conceited royal title buy food the title forbids (a Duke bought legendary cheese and simple meals), refuse to eat it and get the "has no food" thought; the same food counts as "carrying enough", so they stop shopping for food | Postfix on `JoyGiver_BuyStuff.CanEat`: food that vanilla `FoodUtility.InappropriateForTitle` rejects for the guest is not bought as food and not counted as carried food |
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
| Positive Connections | Selecting any building in one room throws `NullReferenceException` in `Gizmo_RoomStats.GizmoOnGUI` and the gizmo bar (UI Rethemed Main Bar) stops drawing: `RoomRoleWorker_Den.CountDoors` walks region links without null checks, a one-sided link throws inside `Room.UpdateRoomStatsAndRole`, and the room's role stays null | Prefix replaces `CountDoors` with the same count that skips null regions |
| Prisoner Realism + Cleaning Area Relaunched | Prisoners stand still in an endless Clean job loop when their room has filth inside Home but outside the Clean area: Prisoner Realism picks it, the Cleaning Area Relaunched driver skips it, the job ends at once and is handed out again | Prisoner cleaning only picks filth inside the Clean area |
| Pulse Weaponry (any weapon def with the same mistake) | `DV_PulseRifle` adds `CompProperties_EquippableAbilityReloadable` without `<comps Inherit="False">`, so it also keeps BaseWeapon's plain `CompEquippable`. Both comps save a verbTracker with the same load IDs: "Exception registering ... in loaded object directory" for every such weapon on load | At startup, drops the plain `CompEquippable` from every ThingDef that also has a `CompEquippable` subclass (logs the defs); the extra saved verbTracker is ignored on load |
| ReGrowth plant patches | Duplicate wild plant records abort the world-gen Landmarks / Mutators steps | Dedupes `BiomeDef.wildPlants` |
| IMPACT - Combat Overhaul | Natural armor of animals, insects and anomaly entities can never be breached: armor is deterministic (penetration / armor <= 0.5 always deflects) and the focal breach that wears armor down is only read and added for mechanoids, so a weak attacker deflects forever (megaspider, nociosphere; seen with a hunting animal on a downed insect) | Transpiler on `ArmorDeflection.Prefix` makes the breach gates true for every creature; the metal-armor effect flags keep the mechanoid check |
| ReplaceLib + Fortifications - Industrial | The lathe's "make component" bill vanished on load (three times): ReplaceLib removes "duplicate" recipes after every game load, judging by label, product and `recipeUsers`, which is null for recipes a bench lists in its own `<recipes>`, so Fortifications' lathe recipe counted as a duplicate of vanilla's fabrication bench recipe and left the DefDatabase. A second load in the same session then hit `Could not load reference to Verse.RecipeDef named FT_Make_ComponentIndustrial` and dropped the bill | ReplaceLib's `RecipeUsersAreSame` compares the benches that actually offer each recipe (`AllRecipeUsers`) when `recipeUsers` is null, so recipes on different benches are no longer duplicates |
| RimPacts | A government in exile that leaves or is dismissed deletes the fallen faction's leader without a successor: "Faction leader for X is null" every session | A new leader is picked right away (and after load for any faction that should have one but doesn't), as vanilla does when a leader dies |
| RimPacts | Trade protection pact: any settlement of the pact faction falling tears up the pact and the trade agreement (trust -25, "A promise unkept"), though a defense battle is only ever offered for settlements your trade routes end at | The penalty only applies when the fallen settlement had one of your trade routes |
| RimPacts | Auto-resolved defense: fighters who flew out get their "minor wounds" (bruises with no source and no log entry) only when the next defense plea arrives, weeks later, and the result letter with them. `DefAutoTick` only runs while a defend mission exists, and the mission ends at settlement | Postfix on `WorldComponentTick` runs `DefAutoTick` every 250 ticks while auto-defense records are left and no mission is active |
| RimPacts | A faction shown as "Faction absorbed" can come back: the letter goes out at once, but every settlement after the first moves to the winner over the following days, and the loser is only marked defeated after the last one. Conquered settlements can revolt back to their original owner unless it is defeated, so one already handed over returned to the half-absorbed faction, which then outlived its own absorption | `TryRevertConquered` skips the revolt while the original owner still has pending absorb plans |
| RimPacts + vanilla gatherings | RimPacts makes the first spawned colonist the host of its expo party, whatever that pawn is doing; a host whose schedule forbids recreation (or who is asleep, starving, bleeding ...) leaves at once and vanilla ends the party: "The party has been called off." moments after it starts, with no reason | The host is picked by vanilla's own organizer rules (RimPacts' pick only when nobody qualifies), and the host of any party stays until it ends even when its schedule turns to work or sleep (it still leaves when asleep, starving, bleeding ...), and every gathering's called-off message names the reason ("...: the host X is downed.") |
| RimPacts | Personal tweak, not a fix: letter titles are in sentence case and filled with lowercase def labels ("Treaty expiring: Trade agreement", "World economy: recession", "Defense plea: ...") | In English, a letter whose title matches one of RimPacts' `Rpt_Letter*Label` templates gets title case ("Treaty Expiring: Trade Agreement", "World Economy: Recession"); short joining words stay lowercase except after a colon or dash. Def labels themselves are untouched, so sentences keep their lowercase |
| Rimsenal Faction Pack - Spacer | Second style comp on the mech commander helmet, Dresser, EndTable and FloodLight (duplicate load IDs) | Those conditionals skipped, for that mod only |
| Rimsenal - Security Pack (with LocalTweaks) | Vanilla `PlaceWorker_ShowTurretRadius` only looks for a `Verb_Shoot` / `Verb_Spray` verb and null-refs every frame while placing a turret whose gun has neither (incinerator and graser turrets use `Verb_ShootBeam`) | Prefix on `PlaceWorker_ShowTurretRadius.AllowsPlacing`: such guns draw the range / min range ring of their first ranged verb; LocalTweaks adds the placeworker to those turrets |
| Ritual Size Attenuation + VFE Empire | VFE Empire's art exhibit, grand ball and parade are rituals without an Ideology precept; Ritual Size Attenuation reads every ritual's quality comp from its precept, so its participant count / quality postfixes throw a `NullReferenceException` inside the outcome. The art exhibit then never ends: `StopExhibit` dies after marking the exhibit finished but before its "CeremonyFinished" memo, so the nobles and colonists stay in the gallery, kept awake, until they starve | Prefix on `RitualExtendedDataRef.Comp`: a data ref whose ritual has no precept (and is not a bestowing) or no comp index answers null, so Attenuation skips that ritual and the vanilla quality applies |
| Sensible Bed Ownership | `OwnedBed` rescans every colonist bed on every call; UI and mods call it 200k-520k times a minute (Circinus: 2.3-5.1% of frame, single calls up to 79 ms with 150 beds) | Remembers SBO's answer per pawn while nothing it depends on changed (assign/unassign/claim methods, SBO's bed list, preferred bed, map, bed still owned; max 250 ticks); every 64th hit is re-checked against SBO and a mismatch is logged. Works from the parallel render threads too (Capture Expansion asks it for every pawn lying in a bed: 95 sleeping animals halved FPS), and SBO's bed list scan runs under a lock (it threw "Collection was modified" in pawn rendering) |
| Sensible Bed Ownership | Reservation prefix null-refs on just-despawned beds | Skipped for beds with no map |
| Sensible Bed Ownership | "Exception while recalculating WantToSleepWithSpouseOrLover thought state" (NullReferenceException in `GetMainBed`), about 10 a session: when no assigned bed is on the pawn's map, it picks among all assigned beds by `b.Map.IsPlayerHome` and `b.Map.uniqueID`, and an assigned bed can be on no map at all (gravship in flight, stored, minified) | Those lambdas treat a bed with no map as not at home and as the last choice |
| Show Mod Updates + UI Rethemed - Main Bar | Visual tweak: the "N updated mods" button in the themed Menu has no icon (UIR only knows the vanilla options) | Matches Show Mod Updates' label in the active language (also with its "RimWorld updated" line after a game update, or that line alone) and gives it UIR's `arrow-clockwise` icon |
| Skilled Refueling | `GetRefuelDuration` caches one iterator field from whichever refuel job runs first, so the other job type (normal or `atomicFueling`, e.g. Rimsenal - Security Pack) throws `ArgumentException`; atomic refuels also ignore refuel speed | Prefix refreshes the cache for the current job; atomic refuels take 240 ticks divided by the pawn's refuel speed |
| Smarter Visitors + Hospitality | Its prefix on `Trigger_TicksPassed.ActivateOn` runs for every ticks trigger of a visitor lord, not only Hospitality's stay timer: also vanilla's 1200-tick `Trigger_TicksPassedWithoutHarm` that sends a group back to travelling after a fight. With "Visitors will not leave if needing medical rest" on, a hurt visitor pushes that deadline out 2500 ticks every time, so a group hurt while leaving stands at its defend point eating and sleeping forever | The prefix only handles `Trigger_TicksPassedAndOkayToLeave`; every other ticks trigger runs vanilla |
| Smart Medicine - Continued | Personal tweak, not a fix: the Stock Up picker only lists drugs, medicine and the vanilla hemogen pack, so VRE Sanguophage's animal, corpse and sanguophage hemogen packs and carried meals (plain Foods) cannot be stocked without the dev-only "Stock ANYTHING" list | Postfix on the picker's filter lambda (`Dialog_StockUp.<DoWindowContents>b__10_0`) also admits `VRE_HemogenPack_Animal`, `VRE_HemogenPack_Corpse`, `VRE_HemogenPack_Sanguophage`, `MealSurvivalPack` and `MealNutrientPackaged` (Rimsenal Spacer) |
| Smart Medicine - Continued | Stocked food-type items such as hemogen packs (preferability `DesperateOnly`) are dropped by the idle `JobGiver_DropUnusedInventory` in the Home area once the pawn has not used raw inventory food for 150000 ticks, then fetched again: its keep check only covers the drug loop | Prefix on `JobGiver_DropUnusedInventory.Drop` keeps anything the pawn is stocking up on |
| Smart Medicine - Continued | "Use nearby medicine (if same quality)" compares the ground medicine's pathfinding cost (walking ticks, about 10-13 a tile, from the medicine to the doctor plus to the patient) against the doctor-patient distance in tiles + 2 x the setting, so "6 spaces" meant about one tile: doctors kept using the patient's Stock Up medicine with a full medicine cabinet 4 tiles from the bed, and the patient then fetched a new one | Transpiler on `FindBestMedicine.Find` (with a prefix remembering doctor and patient): that one check uses the Manhattan distance in tiles from the medicine to the doctor plus to the patient; sorting and the time-until-death limit keep Smart Medicine's path cost |
| Stem's VWE Industrial Expansion, Fortification Industrial - Citadel | "No Verse.SoundDef named ..." load warnings and silent weapons: `Interact_Pistol` (VFE Settlers only), `VWEE_Shot_SawnOffRepeater` and `VFE_Shot_InfernoSpewer` (defined nowhere) | XML: pistols use `Interact_Autopistol`, the sawn-off repeater `Shot_BoltActionRifle`, the flame bunker `InfernoCannon_Fire` (`1.7/Patches/MissingSoundDefs.xml`) |
| Too Many Mods | 22 lowercase `Defs/thingDef[` xpaths (Rimsenal items) | Case fixed |
| Too Many Mods + Vehicle Framework 1.7 | TMM's `VFEM2_VF.dll` (aerial vehicles and the VFE Medieval 2 Merchant Guild) was built for Vehicle Framework 1.6: its `PatchAll` throws at startup ("token resolves to an open generic type"), a red error on every launch, and both patches are lost | TMM's static constructor is skipped and the same rules are applied against the 1.7 API: a guild can only be entered while it stands still (`AerialVehicleCompatibility.RegisterWorldObjectType`), and targeting one offers "form caravan here" on a passable tile (postfix on `LaunchProtocol.GetArrivalOptions`). Lives in its own DLL (`Compat/Harmony3StrictCompat.Vehicles.dll`, `Source/Vehicles`) |
| Triumphant Research | Blank research-complete window with a per-frame index error | Shows the project's full unlock list |
| TD Enhancement Pack - Continued | Personal tweak, not a fix: the "Deteriorating" alert leaves out rotting corpses but lists every fresh one, so raid dead bury the real items | Postfix on `Alert_Deterioration.DeterioratingThings`: no corpses in the alert |
| UI Rethemed - Animals | Hovering an animal row in an unfolded group also lights the row at the same position in every other unfolded group (hover fades are keyed by the row's rect, and each group body draws its rows in its own local coordinates) | Prefix on `Motion.Hover`: its fade key uses the rect's screen position |
| UI Rethemed - Colonist Card + TD Enhancement Pack | Personal tweak, not a fix: TD's skill gain arrows only draw on the vanilla skill rows in the Bio tab | Postfix on `Glance.DrawSkill` runs TD's own `LabelLearning` at the right end of each card skill bar (beside the level number, or at the bar end with UIR's "level on the left"), so its up/down arrow settings and fade still apply |
| UI Rethemed - Inspector Tabs | Hover card rows with a value have a fixed height, so a long label (Vanilla Social Interactions Expanded's opinion thoughts are full sentences) overlaps its own value and the row below | A row that does not fit on one line wraps its label beside the value and grows; other rows keep UIR's drawing |
| UI Rethemed - Inspector Tabs | Gear tab vacuum resistance row flickers every rebuild between "Vacuum resist... 9..." and the full label and value, and its bar restarts its fill animation: the gauge measures its text widths in whatever font the last drawer left (its other gauges force Small first) but draws in Small | Forces Small around `GearTab.RefreshVacuumGauge`, restoring the previous font after; personal tweak on top: the row is relabelled "Vacuum res." so the bar keeps some width |
| UI Rethemed - Inspector Tabs + Full Armor - Hands and Feet | Gloves and boots never appear in the doll's Hands/Feet cells and hover as "Headgear": Full Armor moves every gloves/boots def to the Overhead layer (its `moveGlovesHeadgearLayer`), and UIR classifies by layer before body part, so they land in Extra | `GearTab.VanillaSlotFor`: an Overhead/EyeCover item covering hands or feet and no head, torso or leg group goes to the Hands/Feet cell |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the Gear tab overview orders its cards loadout, mass, stock-up, comfy temperature, combat | Transpiler on `GearTab.DrawStatsPanel` moves the combat card in front of the temperature card and the stock-up cards to the end: mass, combat, temperature, stock-up |
| UI Rethemed - Inspector Tabs (+ Packs Are Not Belts) | Personal tweak, not a fix: empty doll slot captions "Mechanitor", "Exoskeleton" and "Ammunition" (Tiny font) are too wide for the cell and wrap onto a second line | `GearTab.SlotLabel` returns "Mech.", "Exo." and "Ammo" for those layers |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the gear hover card subtitle ("Masterwork · Hardleather") is drawn whole in the quality colour, so the material looks quality-coloured too | Postfix on `GearTab.BuildThingCard` tags the quality word with the accent and the rest (material, stack count) with the default subtitle colour |
| UI Rethemed - Main Bar (+ Heat Map) | Personal tweak, not a fix: the temperature readout only shows a hover card, and its face swallows every click, so it can no longer toggle the temperature overlay the way Heat Map's outdoor thermometer did | Left- or right-click on the temperature readout (text or tile) toggles the vanilla temperature overlay (`showTemperatureOverlay`, the flag Heat Map recolours and its key binding flips), with the checkbox on/off sounds |
| UI Rethemed - Main Bar + TD Enhancement Pack | TD's "Shift-click the time controls to stop the forced slowdown" only patches the vanilla time buttons, which UIR hides, so the forced 1x speed during danger could not be cut short | With TD's setting on: Shift-click on the speed tile ends the forced slowdown while one runs (instead of opening the flyout, with the clock sound of the current speed); Shift-click on a speed in the flyout ends it and picks that speed. Combat can signal a new slowdown, as with TD on vanilla controls |
| UI Rethemed - Inspector Tabs + Auto Extract Genes | AEG draws its "Auto-Extract Genes" checkbox only in the vanilla Health tab, which UIR replaces, so the setting had no switch in the themed tab | Adds it as a switch in the Health tab's Care section (UIR `CareSwitchDef`, DNA icon, AEG's own label and tooltip), shown for the same pawns AEG covers (colonists, slaves, prisoners, colony ghouls), not for animals |
| Colony Manager Redux + MSS Tweaks and Fun | CMR re-finishes its starting research (managing spot/desk) in every map's `FinalizeInit` on every load, without an `IsFinished` check. Each call sends vanilla's "ResearchCompleted" signal, which MSS counts toward "Reformation points for tech levels", so reloading a save gave a free ideology point | CMR's `FinishProject` call skips projects that are already finished |
| Flourish - Combat Animations + Muzzle Flash | Both mods drew a muzzle flash on every shot, so guns covered by Muzzle Flash (vanilla and its add-on packs) flashed twice | Flourish skips its own bloom and flash for guns that carry Muzzle Flash's `MuzzleFlashProps`; every other gun keeps Flourish's flash |
| Leo - Events + Biomes! Caverns (personal tweak) | The "unexpected mineral seams" cave always used the vanilla Underground biome | Each new cave gets a random Caverns biome (Fungal Forest, Crystalline Caverns or Earthen Depths) with its terrain, plants and wildlife, plus Caverns' stalagmite and crystal gen steps. Lava cells get their rock roof back (vanilla terrain gen strips it, leaving the cave "Unroofed"). Existing caves keep their biome |
| Vanilla Expanded Framework (PipeSystem, 1.7 build) | Selecting a minified pipe consumer (e.g. a gas wall lamp from Vanilla Helixien Gas Expanded) throws a `NullReferenceException` every frame: `CompResourceTrader.CompInspectStringExtra` reads `PipeNet.def`, and `PipeNet` is null off the map | Prefix: an unspawned building reports no pipe line |
| Vanilla Expanded pipe networks (hemogen, chemfuel, ...) + Hauler's Dream | Taps filled far past their set max (a tap is a one-cell shelf, and Hauler's Dream "haul to stack" sends items to its partial stack instead of the drain); a drain whose tanks are full kept collecting hauled items | A tap accepts only its own output already on its cell, up to its max (a bigger stack is hauled off to the drain). Works for linked taps too, including vanilla's merge-stacks job (it moved one linked tap's output onto the other). A drain takes hauled items once its network has room for the stack or at least 50 items, and pawns carry only what the tanks can still absorb; while the tanks are full, what sits on the drain is hauled off to ordinary storage (even lower priority), which feeds the drain again once room frees up |
| VFE Farming | The scarecrow threw "Exception ticking VFE_Scarecrow" (NullReferenceException in CellFinderLoose.GetFleeDestAnimal) when a wild animal held inside a building or container was in its radius, and stopped scaring the rest | The scarecrow only considers animals spawned on the map |
| Vanilla wild animal spawner on pocket maps | A cave with a Caverns biome (which has wildlife, unlike vanilla Underground) threw "Index was out of range" in WildAnimalSpawner.CommonalityOfAnimalNow: it reads the world tile (pollution, coast, mutators), which pocket maps lack | On pocket maps animals are weighted by the biome alone |
| Smart Deconstruct + Odyssey vein mining | When a mine job would collapse a thick roof, Smart Deconstruct cancels it and removes the Mine designation, but ore marked with the vein designation (MineVein) kept it, so pawns looped: "started 10 jobs in one tick" and a "cannot mine" message every tick | Its designation lookup falls back to MineVein, so the order is removed like a normal one |
| Nice Research Tab (vanilla alerts) | Clicking "Need research project" (or the anomaly one) cast the research window to the vanilla type and threw InvalidCastException; UI Rethemed - Notifications then drew alerts over the gizmos until restart | With a replaced research window the alert just opens the research tab |
| Colony Groups | A task force (e.g. for a second map) and a colony group could share a load ID ("TacticalGroups.ColonyGroup1"), so loading logged "An item with the same key has already been added" | Task forces get their own load ID; nothing references these groups, so saves are unaffected |
| Vanilla rituals (seen with VFE Tribals) | A finished ritual obligation stored twice in completedObligations logged "An item with the same key has already been added. Key: RitualObligation_30" on every load | Each finished obligation is saved once (the existing duplicate is gone after the next save) |
| UI Rethemed - Notifications | Visual tweak: the game's transient top-left messages stay plain text outside UIR's notification column (its own `MessagesDoGUI` patch only shifts them sideways) | While a game is loaded and the column is bottom-right, middle-right (UIR's default since 2026-11-13) or top-right, draws each live message as a UIR `StatusRow` (the alert-row widget, UIR's own kind colours/icons) next to the alerts, newest nearest them (bottom-right: stacked above; middle-right and top-right: hanging below); left-click jumps / opens the quest, right-click dismisses with UIR's card click sound, hover highlights and shows UIR's own hover card beside the column at once, as its alerts do (header naming the message type, rule, full text) in the row's colour; messages live 30 s instead of 13 and a hovered one restarts its timer; with UIR's section headings on, the rows (sharing UIR's column width: their own wanted width, worked out as UIR's sections do, widens the whole column through a `HudColumn.Shared` postfix) sit under their own folding "Messages" heading (UIR's `SectionHeading`, list icon, count, red/yellow when a bad/warning message is live; folded state saved with UIR's other sections; UIR's heading width counts the "Messages" name so it is not cut off; no "new" count while folded, which UIR's own sections now show) |
| UI Rethemed - Notifications + Satellite Minimap | Personal tweak, not a fix: with the column set to top-right it starts 12 px from the top, under the minimap window, which covers it | While the minimap window is open, the top-right column starts 10 px below its bottom edge (assumes the minimap sits in the top-right corner) |
| UI Rethemed - Notifications (days of food) | The "Days of food" row re-counted every second and rebuilt its hover card, so the tooltip restarted every second and could only be read while paused | The row re-counts every 5 seconds, and not while it is hovered, so the tooltip holds still |
| UI Rethemed - Colonist Card (item info rows) | The meal "Ingredients" row ran off its left edge, cut mid-word, and its tooltip never showed: the rows are drawn outside the inspect window's own pass, so Mouse.IsOver (hover highlight, TipRegion) always said blocked | The row drops " (* only stacks with similar meals)" so more fits; hover uses the plain mouse-in-rect test, and the row's tooltip (the whole line, note included) is entered straight into the game's tooltips |
| UI Rethemed - Architect (+ Vanilla Plants Expanded) | Personal tweak, not a fix: Tilled soil is the only buildable in the Orders category, so UIR gives it a one-card "Orders" group and moves Cancel, Mine, Haul and the other orders to its side column (it does that for any category with something to build) | Postfix on `ArchitectData.ShownApart`: the Orders category's orders stay in the grid, in one group with Tilled soil |
| UI Rethemed - Colonist Card (+ Useful Marks) | A guilty colonist's guilty icon in the inspect pane header sits on top of another header button (Useful Marks' marker): the card moves the pane's buttons into its own slots, but the icon is a plain texture and stays at vanilla's spot | Transpiler on vanilla `MainTabWindow_Inspect.DoInspectPaneButtons` and Useful Marks' copy of it: while the card header draws, the guilty icon and its tooltip take the next header slot |
| UI Rethemed - Colonist Card | Visual tweak: the status pill reads just "Inspired" for every inspiration | The pill names the pawn's inspiration ("Inspired creativity", "Frenzy: shoot") through the effect's label override UIR already has; the hover card keeps the full line with time left |
| UI Rethemed - Quests + Quality Colors (Continued) | Visual tweak: quest reward tiles and their hover cards show item names without the quality colour (the labels reach the tiles untagged, and the hover card strips tags from its title). The market value pill beside the name takes half the tile and cuts the name short | Postfix on `RewardTiles.Of` finds each tile's item through its click closure, wraps the label in the item's Quality Colors colour and moves the market value to the line under the name (a stack count on that line is dimmed); such a label is drawn whole in its colour (prefix on `Theme.LabelTruncated`) and the hover card title gets the colour back after the strip (prefixes on `HoverCard.Region` / `ContentRegion` / `Register`). The value line also works without Quality Colors |
| Unburned + VEF | Drafted pawns holding reach melee weapons (halberd, pike, Mono lance) only auto-attack adjacent enemies: Unburned's prefix copies `JobDriver_Wait.CheckForAutoAttack` and skips the original, so VEF's reach transpiler on the original never runs | Transpiler on Unburned's prefix applies VEF's own `IsVanillaMeleeAttack` test in its copy |
| UI Rethemed - Ideology | Its style hover card is always 340 px wide with 5 icon columns, so a style that restyles about 95 things makes a card about 1600 px high and most of it runs off the screen | The card widens in multiples of 340 px (up to 4x, never wider than the screen) until it fits, and the column counts scale with the width so icons keep their size (postfix on `StyleTip.Content.Width`, transpilers on `CellWidth`, `SectionHeight`, `Draw`) |
| Useful Marks | Its two mark widgets in the rename dialog (label-in-world checkbox, colour square) sit at a fixed offset from the dialog bottom: beside the Title row when renaming a colonist, but on top of the last-name "Suggested..." button in the newborn naming dialog, which has no Title row | The widgets are only drawn when the dialog's bottom row is the Title row |
| Useful Marks | Its inspect pane prefix redraws the info card button for any single selected thing but only skips vanilla for pawns, so items and buildings get a second info card button (stacked on the first in vanilla, a second "i" in UI Rethemed's card header) | Its prefix only runs when the selection is a pawn |
| Useful Marks | Personal addition, not a fix: no mark condition reads guilt, so a guilty prisoner or colonist could only be marked by hand, and that mark outlives the guilt | New "Considered guilty" condition in the mark editor (after "unwaveringly loyal"). It lives in its own DLL (`Compat/`), which the main DLL loads at startup when Useful Marks is active. Marks using it are stored under the last condition index: without this mod, or if Useful Marks adds a condition of its own, such a mark breaks or changes meaning |
| Useful Marks | Personal addition, not a fix: "Specific hediffs" with Missing body part marks every pawn with a bionic arm, leg or similar, because vanilla gives each child part of a replaced part (hand, fingers, foot, toes) a hidden MissingBodyPart hediff | New "Missing body part" condition in the mark editor's health submenu (after "Specific hediffs"). It only counts missing parts with no added part on them or on any part above them, the losses the health tab shows: a lost eye, or an arm with no prosthetic. Same DLL and same index caveat as "Considered guilty"; its index comes right after that one |
| Vanilla | Pawns deep-saved in quests are built before the world exists (MechEnergy null-ref) | No needs while no world exists; saved needs load right after |
| Vanilla | Shuttle passengers walk off and the shuttle never leaves: `CompShuttle.CompTick` evicts (every 600 ticks) any pawn inside whose `despawnedTick` is older than 60000 and drops it from its lord, but that field is the pawn's last map exit, not its boarding time. Bestowing-ceremony escorts are redressed world pawns who visited years earlier, so on the first check after landing some of them were thrown out of the escort lord (the loop also removes while indexing, so every other pawn), walked off as lordless non-colonists, and the shuttle then waited forever for them after the ceremony | Postfix on `CompTransporter.Notify_ThingAdded` sets a boarding pawn's `despawnedTick` to now, so the one-day eviction counts time in the shuttle; `CompShuttle.CompTick` is fingerprinted (game 1.7.5000) |
| Vanilla | Personal tweak, not a fix: install and implant-removal operations show a grey placeholder square in the operations menu (no `uiIconThing`, no product) | Fills the icon once at startup for surgery recipes without one: the implant item they consume, else the item the hediff drops |
| Vanilla | Personal tweak, not a fix: a click on a plant pot or hydroponics basin selects the plant first, the grower only on a second click (only storage buildings come before what is on them) | Postfix on `Selector.SelectUnderMouse`'s sort key: any `Building_PlantGrower` sorts first, so the first click selects the grower and the second the plant |
| Vanilla + GUI Performance Patch + VEF abilities | Small (shrunk) psycast/ability buttons in the top row show no tooltip: vanilla records the gizmo grid height with `Mathf.Min`, leaving small-button rows above the big ones out; GUI Performance Patch reuses last frame's buttons while the mouse is outside that height, and VEF's ability button adds its label to the description on every small draw, so the tooltip text changed every frame and its delay never ran out | Transpiler on `GizmoGridDrawer.DrawGizmoGrid`: that height uses `Mathf.Max`, so it covers every row |
| Vanilla (seen with RimPacts defend waves) | `PawnRelationWorker_Sibling.CreateRelation` creates the existing sibling's missing parents, then reads `GetMother().story`; when there is still no mother it throws, pawn generation rethrows and the whole raid group fails. A RimPacts defend wave retried every few seconds (20 s freezes, the raid never arrived) | Finalizer: that exception is swallowed (the new pawn just gets no sibling) and logged once per pair with the mother/father details |
| Vanilla (found through Modern Needs Tab) | `MinifiedThing.LabelNoCount` null-refs once its contents were installed, so the haul-to-frame job report throws; Modern Needs Tab then turns its job-history capture off for the session | An empty minified thing uses its def label |
| Vanilla + VFE Pirates (Junkers) | Quest says "raid of roughly this size:" followed by nothing, then the raid comes anyway. `PawnGroupKindWorker_Normal.CanGenerateFrom` checks options against the full point total, but pawn picking caps each pawn at the faction's `maxPawnCostPerTotalPointsCurve`, so the melee-only Junker group (Hussars, 600 each) passes below ~5800 points and yields no pawns | Postfix on `CanGenerateFrom` also rejects a normal group when no option fits under that cap, so another group is rolled |
| Vanilla / unknown | Trade caravan whose trader has a non-trader pawn kind: dismiss does nothing, caravan never exits | A guard holding a trader kind counts as the Trader |
| Vanilla / unknown | Trade ship slave also saved as a world pawn loads twice | World copy dropped after load |
| Vanilla | Trade caravans and visitor groups arrive and leave at once "because of dangerous temperature" at a comfortable temperature: hypothermia/heatstroke stays frozen on a mothballed world pawn (mothballed pawns only age) and the 15% leave trigger fires when that pawn is reused, even a year later | While a world pawn is mothballed, its hypothermia, heatstroke and insect hypothermic slowdown recover at vanilla's off-map rate; caravan members (never mothballed) are unchanged |
| Vanilla Quests Expanded - Ancients | Personal tweak, not a fix: the pneumatic capsule arrival message does not say what arrived, and the items are hauled away before you can look | Transpiler on `Building_PneumaticTubeLaunchPort.Deliver` sends the message after the contents are known and lists up to 6 kinds with total counts |
| Vanilla Social Interactions Expanded (+ UI Rethemed - Inspector Tabs) | "Cured my friend" thought shows the raw `{SECONDPAWN_nameDef} cured my {0} ...` template with "Could not resolve symbol" errors when the patient is an implied relative (sibling, cousin, grandparent): the thought checks all relations, its label only direct ones. UIR then falls back to the vanilla Social tab until restart | Label built from the same tale with the vanilla most-important relation, gendered by the patient |
| Vanilla Psycasts Expanded (Cold zone) | Personal tweak, not a fix: enemy psycasters cast nothing but Cold zone, because VEF hands NPCs the longest-range autocastable ability when the AI asks for an attack verb without a target, and Cold zone (range 62.9) is the longest; several of them stack zones on one spot, and the zone's additive glow, drawn above pawns, turns into a white-out | `VPE_ColdZone` gets `chance` 0, so NPCs no longer autocast it (colonists can still cast it by hand; only its autocast toggle goes) and fall back to the other frost casts. Its `VPE_ColdGlow` fleck gets half alpha and is drawn on the ground (`MoteLow`) under buildings, items and pawns (`1.7/Patches/VPE_ColdZone.xml`) |
| Vanilla Races Expanded - Sanguophage | Animalfeeders (strigoi) never drink animal hemogen packs on their own, even with packs in their inventory: VRE only added an auto-drink job for corpse packs, vanilla only looks for regular packs | When vanilla finds no prisoner or regular pack, an animalfeeder drinks animal packs the vanilla way (carried, inventory, then nearest on the map) up to max hemogen |
| VEF | Verb range factor mutates the shared verb def and can stick (29.85 instead of 19.9) | Factor applied to the result instead |
| VEF (MVCF) | Multi-verb weapons get 100% accuracy and 1 s cooldown; duplicate stat rows; "Range 1" | Unset verb stats fall back to the weapon's own; duplicate rows hidden; Range row rebuilt |
| VFE Tribals + UI Rethemed - Factions | Visual tweak: the Cornerstones button keeps the vanilla button look in the themed Factions tab (UIR leaves room for it in the title bar but does not restyle it) | While the tab is themed, draws it with UIR's own action button (blue, campfire icon), sized to its label, and widens that title bar gap to fit; VFE's vanilla button otherwise |
| VTE Variations + Reel's Expanded Storage | "Choose graphic" / "Randomize graphic" change nothing on furniture that is also an Adaptive Storage building (wardrobe, royal dresser, tool cabinet, ...), which draw their GraphicsDef texture (base graphic hidden) and cache it, while VEF only swaps the base graphic | For Adaptive Storage defs with VEF's random-graphic comp, a GraphicsDef layer whose texture is one of the variants draws the chosen variant; picking one redraws the building |
| (tooling, this mod) | Personal tooling, not a fix: the game keeps only `Player.log` and `Player-prev.log`, so a problem from two runs ago (a bill dropped on load) could not be traced | At startup, copies the previous run's `Player-prev.log` and Hauler's Dream debug logs into `Logs/Archive`, named by the time that run ended (`Player-2026-11-17_145212.log`), keeping the newest 30 of each |

Companion XML tweaks: [LocalTweaks](https://github.com/hugalafutro/LocalTweaks).

Build: `Tools/build.ps1` (compiles `Source/*.cs` against the game's DLLs into `1.7/Assemblies`).

<!-- MODLIST:START -->
## Active mod list

755 mods in load order, RimWorld 1.7.5011 rev24. Generated from ModsConfig.xml by `Tools/modlist.py`.

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
| 22 | [Vehicle Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404) | `smashphil.vehicleframework` |
| 23 | [[LFS] UI Rethemed - Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3832843406) | `lazyfridaystudio.uirethemed.core` |
| 24 | [LunarFramework Harmony 3 兼容修复](https://steamcommunity.com/sharedfiles/filedetails/?id=3841353488) | `ak.lunar.framework.harmony3fix` |
| 25 | [1trickPwnyta's Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2764625248) | `ideologypatch.1trickpwnyta` |
| 26 | [[1.6] Do Not Avoid Fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3303359387) | `antihydrogen.donotavoidfire` |
| 27 | [[CAP] Thunder Snow](https://steamcommunity.com/sharedfiles/filedetails/?id=3590107592) | `cap.thundersnow` |
| 28 | [[DG]Lovly Animal Cuddling](https://steamcommunity.com/sharedfiles/filedetails/?id=3823442009) | `local.animalcuddling` |
| 29 | [[DR] Dream's Damage Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3789796309) | `mjcg.drdamageindicators` |
| 30 | [[DR] Where's My Treasure](https://steamcommunity.com/sharedfiles/filedetails/?id=3559591864) | `mjcg.wheresmytreasure` |
| 31 | [[LFS] UI Rethemed - Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=3831602102) | `lazyfridaystudio.uirethemed.animals` |
| 32 | [[LFS] UI Rethemed - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=3830054994) | `lazyfridaystudio.uirethemed.architect` |
| 33 | [[LFS] UI Rethemed - Colonist Card](https://steamcommunity.com/sharedfiles/filedetails/?id=3828206639) | `lazyfridaystudio.uirethemed.colonistcard` |
| 34 | [[LFS] UI Rethemed - History](https://steamcommunity.com/sharedfiles/filedetails/?id=3830774158) | `lazyfridaystudio.uirethemed.history` |
| 35 | [[LFS] UI Rethemed - Inspector Tabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3825029855) | `lazyfridaystudio.uirethemed.inspectortabs` |
| 36 | [[LFS] UI Rethemed - Main Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3826865706) | `lazyfridaystudio.uirethemed.mainbar` |
| 37 | [[LFS] UI Rethemed - Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3827650605) | `lazyfridaystudio.uirethemed.notifications` |
| 38 | [[LFS] UI Rethemed - Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3829391880) | `lazyfridaystudio.uirethemed.quests` |
| 39 | [[LFS] UI Rethemed - Wildlife](https://steamcommunity.com/sharedfiles/filedetails/?id=3828731834) | `lazyfridaystudio.uirethemed.wildlife` |
| 40 | [[LTO] Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=2345493945) | `derekbickley.ltocolonygroupsfinal` |
| 41 | [[NL] Facial Animation - WIP](https://steamcommunity.com/sharedfiles/filedetails/?id=1635901197) | `nals.facialanimation` |
| 42 | [Genetic Heads Framework for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3498759997) | `sd.fa.geneticheadsframework` |
| 43 | [[Og] Automatic Power Switch](https://steamcommunity.com/sharedfiles/filedetails/?id=3031634496) | `og.automaticpowerswitch` |
| 44 | [[Og] Repair Your Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3513376486) | `og.repair.your.gear` |
| 45 | [[sbz] Bookcase](https://steamcommunity.com/sharedfiles/filedetails/?id=3789304104) | `sbz.bookcase` |
| 46 | [[sbz] Fridge](https://steamcommunity.com/sharedfiles/filedetails/?id=3486264784) | `sbz.neatstoragefridge` |
| 47 | [[sbz] Gravship Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3537905298) | `sbz.gravshipstorage` |
| 48 | [[sbz] Neat Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3416243474) | `sbz.neatstorage` |
| 49 | [[sbz] Workbench Shelf](https://steamcommunity.com/sharedfiles/filedetails/?id=3484671309) | `sbz.neatstorageworkbenchshelf` |
| 50 | [[UR] Better Filter for Storage, Clothes and Food Restrictions](https://steamcommunity.com/sharedfiles/filedetails/?id=3753101034) | `uranv.betterfilter` |
| 51 | [[XND] Memorable Auroras (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2270039395) | `mlie.xndmemorableauroras` |
| 52 | [[XND] Visible Pants](https://steamcommunity.com/sharedfiles/filedetails/?id=2264108215) | `xeonovadan.visiblepants` |
| 53 | [Absolute Legends: Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=2195744587) | `absolute.legends.hair` |
| 54 | [Adaptive Primitive Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3400037215) | `adaptive.primitivestorage` |
| 55 | [AdaptiveMealBill](https://steamcommunity.com/sharedfiles/filedetails/?id=3668761367) | `rabiosus.adaptivemealbill` |
| 56 | [Additional Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3692484664) | `ferny.additionalherobackgrounds` |
| 57 | [Advanced Battle Cloak](https://steamcommunity.com/sharedfiles/filedetails/?id=3598135811) | `darjeeling.battlecloak` |
| 58 | [AFU男士发型_Men's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2072106893) | `jintuzilamian.hairstyle.afumenshairstyles` |
| 59 | [Against all odds](https://steamcommunity.com/sharedfiles/filedetails/?id=3552336221) | `literallyfree.colonistreturnbuff` |
| 60 | [Alfred Ancient - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3579763743) | `ferny.alfredancient` |
| 61 | [All Ideoligions Are Fluid](https://steamcommunity.com/sharedfiles/filedetails/?id=2872901762) | `superniquito.allideoligionsarefluid` |
| 62 | [Allies Fight the Fleshmass Heart](https://steamcommunity.com/sharedfiles/filedetails/?id=3772519386) | `lg.fleshmassalliedtarget` |
| 63 | [Almost There! Fork](https://steamcommunity.com/sharedfiles/filedetails/?id=3515165298) | `duz.almosttherefork` |
| 64 | [Alpha Armoury](https://steamcommunity.com/sharedfiles/filedetails/?id=3622294464) | `sarg.alphaarmoury` |
| 65 | [Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677) | `sarg.alphabiomes` |
| 66 | [Alpha Implants](https://steamcommunity.com/sharedfiles/filedetails/?id=3275091331) | `sarg.alphaimplants` |
| 67 | [Alpha Memes](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814) | `sarg.alphamemes` |
| 68 | [Alpha Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021) | `sarg.alphaprefabs` |
| 69 | [Alpha Random](https://steamcommunity.com/sharedfiles/filedetails/?id=3642631969) | `sarg.alpharandom` |
| 70 | [Always Position Gravship on Landing](https://steamcommunity.com/sharedfiles/filedetails/?id=3738322550) | `ferny.alwayspositiongravshiponlanding` |
| 71 | [Always Start in Daytime](https://steamcommunity.com/sharedfiles/filedetails/?id=3323971974) | `ferny.alwaysstartindaytime` |
| 72 | [Amorphous style pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3382499324) | `xmb.amorphousstylepack.mo` |
| 73 | [ANDH - Animals Nuzzling Detects Horrors](https://steamcommunity.com/sharedfiles/filedetails/?id=3230195082) | `latta.petknowstrueyou` |
| 74 | [Animal Collab Project Vanilla-Style](https://steamcommunity.com/sharedfiles/filedetails/?id=2208467668) | `acpteam.acpvanillastyle` |
| 75 | [Animals Forage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667046888) | `mlie.animalsforage` |
| 76 | [Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1195427067) | `com.bymarcin.architecticons` |
| 77 | [Armors For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3787547731) | `bamba.allbambaarmors` |
| 78 | [Asteroid Mineral Scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3536585361) | `broms.asteroidmineralscanner` |
| 79 | [Auto-assign Inventories on Caravan arrival](https://steamcommunity.com/sharedfiles/filedetails/?id=2882784350) | `syrus.autoassigninventorycaravan` |
| 80 | [Autoclose Event Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3226920093) | `dingo.autocloseeventnotifications` |
| 81 | [AutoExtractGenes](https://steamcommunity.com/sharedfiles/filedetails/?id=2882834449) | `nibato.autoextractgenes` |
| 82 | [Automatic Foraging](https://steamcommunity.com/sharedfiles/filedetails/?id=3687750532) | `i.automaticforaging` |
| 83 | [Automatic Night Owl](https://steamcommunity.com/sharedfiles/filedetails/?id=2056012179) | `ben.automaticnightowl` |
| 84 | [Barbs And Moats](https://steamcommunity.com/sharedfiles/filedetails/?id=3601561731) | `sgtbruce.barbsandmoats` |
| 85 | [Basic Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3600443734) | `stefanlog.basictraits` |
| 86 | [Battery Capacity in Stat View](https://steamcommunity.com/sharedfiles/filedetails/?id=3651439731) | `linnun.batterycapacity` |
| 87 | [Bed Rest For Food Poisoning](https://steamcommunity.com/sharedfiles/filedetails/?id=2567901366) | `bedrestforfoodpoisoning.1trickponyta` |
| 88 | [Better Abandoned Settlements](https://steamcommunity.com/sharedfiles/filedetails/?id=3826665236) | `edybtt.betterabandonedsettlements` |
| 89 | [Better Beggars (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3006899215) | `mlie.betterbeggars` |
| 90 | [Better Context Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3696948139) | `mrk.bettercontextmenu` |
| 91 | [Better Distress Call](https://steamcommunity.com/sharedfiles/filedetails/?id=3305489753) | `nalzurin.betterdistresscall` |
| 92 | [Better Gene Inheritance](https://steamcommunity.com/sharedfiles/filedetails/?id=3046776238) | `redmattis.bettergeneinheritance` |
| 93 | [Better ground-penetrating scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3203274131) | `kikohi.bettergroundpenetratingscanner` |
| 94 | [Better Growth Moments](https://steamcommunity.com/sharedfiles/filedetails/?id=3642805464) | `arkymn.bettergrowthmoments` |
| 95 | [Better Linkable Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3577372705) | `ganja.betterlinkablecolors` |
| 96 | [Better Mechanoid Sorting](https://steamcommunity.com/sharedfiles/filedetails/?id=3730840942) | `redstonepanda.bettermechrank` |
| 97 | [Better Mod Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3636406008) | `mumei.bettermodmenu` |
| 98 | [Better Records Tab (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2359778562) | `mlie.betterrecordstab` |
| 99 | [Better Scroll Bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3579237751) | `dillyo.betterscrollbars` |
| 100 | [Better Traders Guild](https://steamcommunity.com/sharedfiles/filedetails/?id=3684587591) | `shunter.bettertradersguild` |
| 101 | [Better Trees](https://steamcommunity.com/sharedfiles/filedetails/?id=3539609975) | `chaoticenrico.bettertrees` |
| 102 | [Better Workbench Management - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819555998) | `falconne.bwm` |
| 103 | [Big Bed Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=3201556342) | `amch.bigbedbuff` |
| 104 | [Biosculpter Pod and Neural Supercharger Resize](https://steamcommunity.com/sharedfiles/filedetails/?id=2710513531) | `syrus.bpansresize` |
| 105 | [Birth Quality Lifespan Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3654799710) | `saltgin.birthqualitylifespanfix` |
| 106 | [Blindsight - Unified](https://steamcommunity.com/sharedfiles/filedetails/?id=3248249029) | `spencezilla.blindsightunified` |
| 107 | [Blindsight suffers no Unnatural Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=3258096951) | `emitbreaker.blindnessunnaturaldarkness` |
| 108 | [Block Unwanted Minutiae (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3278213153) | `mlie.blockunwantedminutiae` |
| 109 | [Blueprints - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819556044) | `nova.blueprints.waylith17` |
| 110 | [BuilderTweaksReplica](https://steamcommunity.com/sharedfiles/filedetails/?id=3521096349) | `rabiosus.buildertweaksreplica` |
| 111 | [Caravan Mood Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=2680751877) | `syrus.caravanmoodbuff` |
| 112 | [Change map edge limit](https://steamcommunity.com/sharedfiles/filedetails/?id=1546494565) | `kapitanoczywisty.changemapedge` |
| 113 | [Chester Choose - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3761449697) | `ferny.chesterchoose` |
| 114 | [Childhood Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3000770956) | `zylle.childboodbackstories` |
| 115 | [Choose Where To Land](https://steamcommunity.com/sharedfiles/filedetails/?id=3537970831) | `kearril.choosewheretoland` |
| 116 | [Chronos Pointer - Schedule Menu Enhancement](https://steamcommunity.com/sharedfiles/filedetails/?id=3408065390) | `coolnether123.chronospointer` |
| 117 | [Circinus Dev Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3771602203) | `astryl.circinusdevtools` |
| 118 | [Cleaning Area Relaunched](https://steamcommunity.com/sharedfiles/filedetails/?id=3586697885) | `cerule.cleaningarearelaunched` |
| 119 | [Colored deep resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3203269467) | `kikohi.coloreddeepresources` |
| 120 | [Common Sense](https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193) | `avilmask.commonsense` |
| 121 | [Compositable Loadouts](https://steamcommunity.com/sharedfiles/filedetails/?id=2679126859) | `wiri.compositableloadouts` |
| 122 | [Consciousness Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2913127696) | `vex.consciousness` |
| 123 | [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079) | `phoenix.continuebutton` |
| 124 | [Cook for Yourself](https://steamcommunity.com/sharedfiles/filedetails/?id=3766233150) | `lordfelix.cookforyourself` |
| 125 | [Corpse Description Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3639910228) | `ferny.corpsedescriptionfix` |
| 126 | [Corpse info](https://steamcommunity.com/sharedfiles/filedetails/?id=2913588675) | `arl85.corpseinfo` |
| 127 | [D12 Dungeon and Night Hairs HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3620959277) | `d12caedro.dynhairshd` |
| 128 | [Dark Psychic Rituals: The Following](https://steamcommunity.com/sharedfiles/filedetails/?id=3596468709) | `dprtf.darkpsychicrituals.sentinel` |
| 129 | [Deathrest Pauses Biological Age](https://steamcommunity.com/sharedfiles/filedetails/?id=2940569974) | `crykosity.deathrest` |
| 130 | [Default Ingredient Radius](https://steamcommunity.com/sharedfiles/filedetails/?id=2909706985) | `mlie.defaultingredientradius` |
| 131 | [Deselect Zone](https://steamcommunity.com/sharedfiles/filedetails/?id=3328706359) | `cheesex.deselectzone` |
| 132 | [Designations Tooltip](https://steamcommunity.com/sharedfiles/filedetails/?id=3743959668) | `cheatereater.designationstooltip` |
| 133 | [Designator Shapes](https://steamcommunity.com/sharedfiles/filedetails/?id=1235181370) | `merthsoft.designatorshapes` |
| 134 | [Det's Xenotypes - Avaloi](https://steamcommunity.com/sharedfiles/filedetails/?id=3199692643) | `det.avaloi` |
| 135 | [Det's Xenotypes - Brawnum](https://steamcommunity.com/sharedfiles/filedetails/?id=3429572581) | `det.brawnum` |
| 136 | [Det's Xenotypes - Boglegs](https://steamcommunity.com/sharedfiles/filedetails/?id=3146564944) | `det.boglegs` |
| 137 | [Det's Xenotypes - Buzzers](https://steamcommunity.com/sharedfiles/filedetails/?id=3545293786) | `det.buzzers` |
| 138 | [Det's Xenotypes - Half-foot](https://steamcommunity.com/sharedfiles/filedetails/?id=3530817307) | `det.halffoot` |
| 139 | [Det's Xenotypes - Keshig](https://steamcommunity.com/sharedfiles/filedetails/?id=3376864722) | `det.keshig` |
| 140 | [Det's Xenotypes - Stoneborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2888722722) | `det.stoneborn` |
| 141 | [Det's Xenotypes - Venators](https://steamcommunity.com/sharedfiles/filedetails/?id=3140248688) | `det.venators` |
| 142 | [Dignified Empire Names](https://steamcommunity.com/sharedfiles/filedetails/?id=3530589531) | `goosechoosesviolence.empirenames` |
| 143 | [Dlc collaboration - Void universe](https://steamcommunity.com/sharedfiles/filedetails/?id=3587277884) | `hailuan.voiduniverse` |
| 144 | [Don't Mix Cosmetic Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3621282829) | `wara.dontmixcosmeticgenes` |
| 145 | [Don't Say 100% Unless You Mean It!](https://steamcommunity.com/sharedfiles/filedetails/?id=2894636032) | `kathanon.donemeansdone` |
| 146 | [DragSelect](https://steamcommunity.com/sharedfiles/filedetails/?id=2599942235) | `telardo.dragselect` |
| 147 | [Dress for the Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607724833) | `mlie.dressfortheweather` |
| 148 | [Dubs Apparel Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2296697286) | `dubwise.dubsappareltweaks` |
| 149 | [Dune Style Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2993301551) | `dsp.halituisamaricanous` |
| 150 | [Dynamic Carrying Capacity (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2967228692) | `mlie.dynamiccarryingcapacity` |
| 151 | [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506) | `zeracronius.dynamictradeinterface` |
| 152 | [ED-Embrasures (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3277482616) | `ganja.ed.embrasures` |
| 153 | [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092) | `mnmr.eprimereadouts` |
| 154 | [Erin Eccentric](https://steamcommunity.com/sharedfiles/filedetails/?id=2967829336) | `erin.storyteller` |
| 155 | [Erin's Baldur's Gate 3 Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3069933015) | `erin.bg3.hair` |
| 156 | [Erin's Hairstyles - Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=2361911135) | `erin.hairredux` |
| 157 | [Erin's Hairstyles 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2849477421) | `erin.hair2` |
| 158 | [Evolving Enemy Strongholds](https://steamcommunity.com/sharedfiles/filedetails/?id=3738072776) | `wowgag.evolvingenemystrongholds` |
| 159 | [Expressive Appendages](https://steamcommunity.com/sharedfiles/filedetails/?id=3830976421) | `remi.expressiveappendages` |
| 160 | [Extra Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=1926923167) | `zylle.extraalerts` |
| 161 | [Faction Raid Cooldown (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3547098393) | `mlie.factionraidcooldown` |
| 162 | [Features Gap Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3753339482) | `ferny.featuresgapfix` |
| 163 | [Fire At Will Gizmo - Evolving Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3770020686) | `zohryu.fireatwillgizmo.evolving` |
| 164 | [FirmamentFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3781872865) | `dorbo.firmamentfx` |
| 165 | [Fix Styled Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2957953663) | `kathanon.fixstyledblueprints` |
| 166 | [Float Sub-Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2864015430) | `kathanon.floatsubmenu` |
| 167 | [FMOD Errors Begone](https://steamcommunity.com/sharedfiles/filedetails/?id=3799402443) | `zzz.fmoderrorsbegone` |
| 168 | [Food Poisoning Investigator](https://steamcommunity.com/sharedfiles/filedetails/?id=3697891858) | `leo.foodpoisoninginvestigator` |
| 169 | [Forgetful Mech Resurrection](https://steamcommunity.com/sharedfiles/filedetails/?id=3766269434) | `mrhydralisk.forgetfulmechresurrection` |
| 170 | [Fortifications - Medieval - Material](https://steamcommunity.com/sharedfiles/filedetails/?id=2501487160) | `aoba.fortress.medieval.material` |
| 171 | [Fortifications - Neolithic](https://steamcommunity.com/sharedfiles/filedetails/?id=2385960678) | `aoba.fortress.neolithic` |
| 172 | [Friendly Fire Tweaks (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3591533573) | `zal.friendlyfiretweaks` |
| 173 | [Full Armor - Hands and Feet [1.6]](https://steamcommunity.com/sharedfiles/filedetails/?id=3530977354) | `stevezero.fullarmorhandsfeet` |
| 174 | [Gene Bank Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3265067908) | `leoltron.genebankindicators` |
| 175 | [Ghouls Prefer Twisted Meat](https://steamcommunity.com/sharedfiles/filedetails/?id=3230171961) | `doug.ghoulsprefertwistedmeat` |
| 176 | [Giddy-Up 2 - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861) | `memegoddess.giddyup` |
| 177 | [Glasma's Refugee Expectations](https://steamcommunity.com/sharedfiles/filedetails/?id=3785120443) | `glasma.refugeeexpectations` |
| 178 | [Go Explore!](https://steamcommunity.com/sharedfiles/filedetails/?id=1814100216) | `albion.goexplore` |
| 179 | [Gojis-Misc-Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3775114861) | `goji.gojismiscgenes` |
| 180 | [Goodwill Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=3695225114) | `bluebird.goodwillpreview` |
| 181 | [Gore Perfected](https://steamcommunity.com/sharedfiles/filedetails/?id=3814447022) | `tiepi.goreperfected` |
| 182 | [Gravship Crew Manifest Saver](https://steamcommunity.com/sharedfiles/filedetails/?id=3641927599) | `lornath.gravshipcrewmanifestsaver` |
| 183 | [Grenade Fix - Rearmed](https://steamcommunity.com/sharedfiles/filedetails/?id=797360572) | `dingo.grenadefixrearmed` |
| 184 | [Guns For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3782391721) | `bambaryla.allbambaguns` |
| 185 | [Hard Times: Hair and Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3092175321) | `botchjob.hthair` |
| 186 | [Hauler's Dream](https://steamcommunity.com/sharedfiles/filedetails/?id=3742459652) | `giwaffed.haulersdream` |
| 187 | [Heat Map (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2552838384) | `syrus.heatmap` |
| 188 | [Hive Protection](https://steamcommunity.com/sharedfiles/filedetails/?id=3536627722) | `jaxe.hiveprotection` |
| 189 | [Hunt for Me](https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720) | `arandomkiwi.huntforme` |
| 190 | [I Clearly Have Enough! (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2023661266) | `mlie.iclearlyhaveenough` |
| 191 | [I'm Off (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3251510002) | `zal.imoff` |
| 192 | [I'm Trying To Save You! (Pawn Dressing Rework)](https://steamcommunity.com/sharedfiles/filedetails/?id=3534863993) | `s4.imtryingtosaveyou` |
| 193 | [Ignore Idle Guests](https://steamcommunity.com/sharedfiles/filedetails/?id=2515048728) | `mlie.ignoreidleguests` |
| 194 | [ilyvion's Laboratory](https://steamcommunity.com/sharedfiles/filedetails/?id=3296362231) | `ilyvion.laboratory` |
| 195 | [Infestations Spawn in Darkness 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3551839891) | `koth.isid` |
| 196 | [Injured Carry](https://steamcommunity.com/sharedfiles/filedetails/?id=2413690575) | `haecriver.injuredcarry` |
| 197 | [Inspired Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2851390190) | `lm.inspiredconversion` |
| 198 | [Instant Caravan](https://steamcommunity.com/sharedfiles/filedetails/?id=3165744287) | `ferny.easycaravanformation` |
| 199 | [Integrated Creep Joiners](https://steamcommunity.com/sharedfiles/filedetails/?id=3233429182) | `grillmaster.integratedcreepjoiners` |
| 200 | [Interaction Bubbles](https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345) | `jaxe.bubbles` |
| 201 | [Ion Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532877485) | `zal.ionweaponry` |
| 202 | [IVF Equality](https://steamcommunity.com/sharedfiles/filedetails/?id=2922603310) | `kathanon.ivfequality` |
| 203 | [Just Ignore Me Passing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503627342) | `mlie.justignoremepassing` |
| 204 | [Just Put It Over There](https://steamcommunity.com/sharedfiles/filedetails/?id=2856471776) | `mlie.justputitoverthere` |
| 205 | [KanbanStockpileContinue](https://steamcommunity.com/sharedfiles/filedetails/?id=3776851378) | `igora.kanbanstockpilecontinue` |
| 206 | [Keep Converting](https://steamcommunity.com/sharedfiles/filedetails/?id=3461478214) | `linnun.keepconverting` |
| 207 | [Keyz Misc Resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3355560776) | `keyz182.keyzmiscresources` |
| 208 | [Keyz' Allow Utilities](https://steamcommunity.com/sharedfiles/filedetails/?id=3524716849) | `keyz182.keyzallowutilities` |
| 209 | [Kidnapped Pawns Die Less](https://steamcommunity.com/sharedfiles/filedetails/?id=3308076464) | `conit.kpdl` |
| 210 | [Knick Knacks - Let your colonists decorate!](https://steamcommunity.com/sharedfiles/filedetails/?id=3595196942) | `vaguelysexual.modpackageid.goeshere` |
| 211 | [Large Faction Bases (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3257781909) | `mlie.largefactionbases` |
| 212 | [Last Visited (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3507014727) | `zal.lastvisited` |
| 213 | [Leo - Events](https://steamcommunity.com/sharedfiles/filedetails/?id=3730168769) | `leo.eventsexpanded` |
| 214 | [Less Stupid Romance Attempt (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014603702) | `mlie.lessstupidromanceattempt` |
| 215 | [Letter Permanent Injury](https://steamcommunity.com/sharedfiles/filedetails/?id=2592535960) | `buggy.rimworld.letterpermanentinjury` |
| 216 | [Level Up!](https://steamcommunity.com/sharedfiles/filedetails/?id=1701592470) | `krafs.levelup` |
| 217 | [Linkables Info Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3212476920) | `deadmano.linkablesinfoexpanded` |
| 218 | [Little Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=3331281387) | `tyrannidae.littlecritters` |
| 219 | [Living Bonds](https://steamcommunity.com/sharedfiles/filedetails/?id=3708989445) | `bsandyh.livingbonds` |
| 220 | [Load in Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379) | `ferny.loadinbackground` |
| 221 | [Mark of Manhood](https://steamcommunity.com/sharedfiles/filedetails/?id=3646943505) | `maux36.markofmanhood` |
| 222 | [Mech Recharger Standby Mode](https://steamcommunity.com/sharedfiles/filedetails/?id=2879340506) | `heremeus.mechrechargerstandby` |
| 223 | [Mechanoids dont beat fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3252095758) | `fxz.mechbeatfire` |
| 224 | [Megafauna](https://steamcommunity.com/sharedfiles/filedetails/?id=1055485938) | `spino.megafauna` |
| 225 | [Melee Weapons For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3726524693) | `bamba.allbambamelee.tiered` |
| 226 | [Milky Way](https://steamcommunity.com/sharedfiles/filedetails/?id=3773448562) | `andromeda.milkyway` |
| 227 | [Mine Sight](https://steamcommunity.com/sharedfiles/filedetails/?id=3769804600) | `rabiosus.minesight` |
| 228 | [Mo'Events (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2035143365) | `mlie.moevents` |
| 229 | [Mojave Mask - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3781130446) | `ferny.mojavemask` |
| 230 | [Mono Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2997812588) | `cat.monopack` |
| 231 | [More Childhood Nicknames](https://steamcommunity.com/sharedfiles/filedetails/?id=3688519717) | `river.childhoodnames` |
| 232 | [More Persona Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2863308112) | `arquebus.morepersonatraits` |
| 233 | [More Random Seeds](https://steamcommunity.com/sharedfiles/filedetails/?id=1501463043) | `jaxe.morerandomseeds` |
| 234 | [More Slavery Stuff](https://steamcommunity.com/sharedfiles/filedetails/?id=2896845138) | `garryflowers.moreslaverystuff` |
| 235 | [More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1931453053) | `zylle.morevanillabiomes` |
| 236 | [MutatorWorldIcons](https://steamcommunity.com/sharedfiles/filedetails/?id=3551904193) | `snobi.mutatorworldicons` |
| 237 | [Name Your Entities](https://steamcommunity.com/sharedfiles/filedetails/?id=3229905841) | `ferny.nameyourentities` |
| 238 | [Nice Short Female Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3316363751) | `andromeda.nicehairs` |
| 239 | [No Backer Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669069738) | `nobackerpawns.nobackerpawns` |
| 240 | [No Disabled Factions In Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=2892125637) | `kathanon.nodisabledfactions` |
| 241 | [No Hemogen Farm Medical Alert](https://steamcommunity.com/sharedfiles/filedetails/?id=2887053053) | `dark.nohemogenfarmmedicalalert` |
| 242 | [No Infestation On Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=3661608882) | `ialways1ose.noinfestationonbuildings` |
| 243 | [No Job Authors](https://steamcommunity.com/sharedfiles/filedetails/?id=2009825774) | `doug.nojobauthors` |
| 244 | [No Modlist on Loading](https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405) | `ferny.nomodlistonloading` |
| 245 | [No Summon Mech Threat Gizmo](https://steamcommunity.com/sharedfiles/filedetails/?id=2889317343) | `canon.nomechsummonergizmo` |
| 246 | [No Terrain Edge Fade (Hard Floors)](https://steamcommunity.com/sharedfiles/filedetails/?id=3753895045) | `fauxzyi.noterrainedgefade` |
| 247 | [No Version Warning](https://steamcommunity.com/sharedfiles/filedetails/?id=2599504692) | `mlie.noversionwarning` |
| 248 | [Nomad Friendly Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3616175831) | `jaeger972.nomadfreindlyquests` |
| 249 | [Non uno Pinata (don't drop items)](https://steamcommunity.com/sharedfiles/filedetails/?id=1778821244) | `avilmask.nonunopinata` |
| 250 | [Not My Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2870045856) | `vesper.notmyfault` |
| 251 | [Not Overhead Meal](https://steamcommunity.com/sharedfiles/filedetails/?id=2986525461) | `overheadmeal.patch` |
| 252 | [Now Playing](https://steamcommunity.com/sharedfiles/filedetails/?id=3754684545) | `dorbo.nowplaying` |
| 253 | [OgreStack](https://steamcommunity.com/sharedfiles/filedetails/?id=1447140290) | `ogre.ogrestack` |
| 254 | [Packs Are Not Belts](https://steamcommunity.com/sharedfiles/filedetails/?id=2509511179) | `turnovus.submod.backpacksarenotbelts` |
| 255 | [Pawn Editor Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113) | `segaswolf.pawneditor.fork` |
| 256 | [Pawn Name Variety (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3548290568) | `mlie.pawnnamevariety` |
| 257 | [Peer Pressure (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3605155621) | `densevoid.peerpressure` |
| 258 | [Persona Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3766728761) | `shunter.personaweaponsunbound` |
| 259 | [Perspective: Eaves (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3346964576) | `mlie.perspectiveeaves` |
| 260 | [Phase Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) | `det.phaseweaponry` |
| 261 | [Pillar Planner](https://steamcommunity.com/sharedfiles/filedetails/?id=3768069893) | `astryl.pillarplanner` |
| 262 | [Planning Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2877392159) | `scherub.planningextended` |
| 263 | [Plasma Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3070942578) | `cat.plasmapack` |
| 264 | [Playwright](https://steamcommunity.com/sharedfiles/filedetails/?id=3753005413) | `rokk.playwright` |
| 265 | [Pollution Spread](https://steamcommunity.com/sharedfiles/filedetails/?id=3692324370) | `turkler.pollutingfactions` |
| 266 | [Positive Connections](https://steamcommunity.com/sharedfiles/filedetails/?id=3003882069) | `cem.positiveconnections` |
| 267 | [Power Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=899714534) | `erdelf.powerindicators` |
| 268 | [Pregnancy Attitude](https://steamcommunity.com/sharedfiles/filedetails/?id=3530918349) | `carriedactyl.pregnancyattitude` |
| 269 | [Prioritized Kill Dev Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3533940586) | `prioritizedkilltool.programmerlily.com` |
| 270 | [Prisoner Realism](https://steamcommunity.com/sharedfiles/filedetails/?id=3760196312) | `legator.prisonerrealism` |
| 271 | [Progression: Soundtrack](https://steamcommunity.com/sharedfiles/filedetails/?id=3620726714) | `ferny.progressionsoundtrack` |
| 272 | [Proper Slave Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3770087153) | `kuchiki.properslavegear` |
| 273 | [Prosthetic No Missing Body Parts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2739055353) | `mlie.prostheticnomissingbodyparts` |
| 274 | [Pulse Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3077879089) | `detvisor.pulseweaponry` |
| 275 | [Pumpkin Hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=3783859385) | `pumpkin.pumpkinhairs2` |
| 276 | [Punch Button](https://steamcommunity.com/sharedfiles/filedetails/?id=2651392585) | `fuu.punchattack` |
| 277 | [Quality Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3513846773) | `dawnsglow.qualcolor` |
| 278 | [QualityBuilder Unofficial 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3512466087) | `hatti.qualitybuilder` |
| 279 | [Quest Generation Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3625994394) | `jaeger972.questgenerationtweaks` |
| 280 | [Questing Meme](https://steamcommunity.com/sharedfiles/filedetails/?id=2826539854) | `sirmashedpotato.questingmeme` |
| 281 | [Quick Stockpile Creation](https://steamcommunity.com/sharedfiles/filedetails/?id=1742151109) | `longwater.smartstockpilecreation` |
| 282 | [Raider Approach Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781897112) | `oldmanyoung.raiderapproachlagfix` |
| 283 | [Range Finder](https://steamcommunity.com/sharedfiles/filedetails/?id=1332119637) | `brrainz.rangefinder` |
| 284 | [Readable Numbers](https://steamcommunity.com/sharedfiles/filedetails/?id=3762441264) | `7h0m4s.rimworldreadablenumbers` |
| 285 | [Real Faction Guest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2886929245) | `mlie.realfactionguest` |
| 286 | [Relevant Stats In Description](https://steamcommunity.com/sharedfiles/filedetails/?id=2692669482) | `mlie.relevantstatsindescription` |
| 287 | [Remove Colonies Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=3745014294) | `ferny.removecolonieslimit` |
| 288 | [Remove Tutorial Button](https://steamcommunity.com/sharedfiles/filedetails/?id=781505663) | `merthsoft.removetutorialbutton` |
| 289 | [Replace Stuff - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526354009) | `memegoddess.replacestuff` |
| 290 | [ReplaceLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3417393194) | `ferny.replacelib` |
| 291 | [RescueeJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177170403) | `ogliss.rescueejoinsplus` |
| 292 | [Research-Based Raids Difficulty](https://steamcommunity.com/sharedfiles/filedetails/?id=3679008124) | `leo.researchbasedraidsdifficulty` |
| 293 | [Resource Delivery Helper](https://steamcommunity.com/sharedfiles/filedetails/?id=3696859196) | `ferny.resourcedeliveryhelper` |
| 294 | [Restrict Undrawn Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3629753146) | `ferny.restrictundrawnapparel` |
| 295 | [Reverse Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=858744731) | `brrainz.reversecommands` |
| 296 | [Right-Click Gizmo Indicator](https://steamcommunity.com/sharedfiles/filedetails/?id=3750529401) | `cheatereater.rightclickgizmoindicator` |
| 297 | [RimPacts - Diplomacy Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3762723122) | `wowgag.rimpacts` |
| 298 | [Rimpsyche - Personality Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3535112473) | `maux36.rimpsyche` |
| 299 | [RimSaves](https://steamcommunity.com/sharedfiles/filedetails/?id=1713367505) | `arandomkiwi.rimsaves` |
| 300 | [Rimsenal - Augmented Vanilla Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=849231601) | `rimsenal.evp` |
| 301 | [Rimsenal - Security Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=736139206) | `rimsenal.security` |
| 302 | [Rimsenal - Storytellers Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725952322) | `rimsenal.storyteller` |
| 303 | [Rimsenal Faction Pack - Federation](https://steamcommunity.com/sharedfiles/filedetails/?id=736172213) | `rimsenal.federation` |
| 304 | [Rimsenal Faction Pack - Feral](https://steamcommunity.com/sharedfiles/filedetails/?id=736207111) | `rimsenal.feral` |
| 305 | [Rimsenal Faction Pack - Spacer](https://steamcommunity.com/sharedfiles/filedetails/?id=3086137468) | `rimsenal.spacer` |
| 306 | [Rimsenal Style Pack - Techist](https://steamcommunity.com/sharedfiles/filedetails/?id=2661828028) | `rimsenal.techist` |
| 307 | [Rimsenal Style Pack - Urbworld](https://steamcommunity.com/sharedfiles/filedetails/?id=2908039338) | `rimsenal.urb` |
| 308 | [Rimsenal Xenotype Pack - Askbarn](https://steamcommunity.com/sharedfiles/filedetails/?id=2918812858) | `rimsenal.askbarn` |
| 309 | [Rimsenal Xenotype Pack - Harana](https://steamcommunity.com/sharedfiles/filedetails/?id=3228637524) | `rimsenal.harana` |
| 310 | [Rimsenal Xenotype Pack - Zohar](https://steamcommunity.com/sharedfiles/filedetails/?id=3504125251) | `rimsenal.zohar` |
| 311 | [Rimsenal: Murder diversified](https://steamcommunity.com/sharedfiles/filedetails/?id=725947920) | `rimsenal.core` |
| 312 | [RimTraits - General Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2206957172) | `sierra.rt.generaltraits` |
| 313 | [Ritual Size Attenuation](https://steamcommunity.com/sharedfiles/filedetails/?id=3262033797) | `delmaintweaks.ritualsizeattenuation` |
| 314 | [Romina Ready - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3736785369) | `ferny.rominaready` |
| 315 | [Roo's HD Hairstyles (continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3559870636) | `rooboid.hdhair.continued` |
| 316 | [Royalty Room Info (Satisfied Title and Upgrade Requirements)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600393808) | `jaeger972.royaltyroominfo` |
| 317 | [RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=728314182) | `ratys.rtfuse` |
| 318 | [Run and Hide (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2022480344) | `mlie.runandhide` |
| 319 | [Rusty's Foliage Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=3646973576) | `rustys.foliage.hairs` |
| 320 | [Satellite Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=3793766543) | `astryl.satelliteminimap` |
| 321 | [Save File Compression](https://steamcommunity.com/sharedfiles/filedetails/?id=3562729067) | `amch.savefilecompression` |
| 322 | [Save Storyteller Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3744418945) | `vc.savestorytellersettings` |
| 323 | [Scrollable Inspect Panes](https://steamcommunity.com/sharedfiles/filedetails/?id=2889151469) | `kathanon.scrollableinspectpanes` |
| 324 | [Self Dyeing](https://steamcommunity.com/sharedfiles/filedetails/?id=2562859859) | `avilmask.selfdyeing` |
| 325 | [Sensible Bed Ownership](https://steamcommunity.com/sharedfiles/filedetails/?id=3328702448) | `sensiblebedownership.1trickpwnyta` |
| 326 | [Settlement Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=2240524945) | `dninemfive.settlementdescription` |
| 327 | [Shadow Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3797651319) | `antisodium.shadowfix` |
| 328 | [Show Buildable Material Count](https://steamcommunity.com/sharedfiles/filedetails/?id=3245101540) | `bp.showbuildablematerialcount` |
| 329 | [Show Known Techprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2920370783) | `spacemoth.showknowntechprints` |
| 330 | [Show Me Your Hands](https://steamcommunity.com/sharedfiles/filedetails/?id=2475965842) | `mlie.showmeyourhands` |
| 331 | [Show Mod Updates](https://steamcommunity.com/sharedfiles/filedetails/?id=3218690663) | `mlie.showmodupdates` |
| 332 | [Show Possible Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=3338920230) | `mlie.showpossiblematerials` |
| 333 | [Show Weapon Tallies](https://steamcommunity.com/sharedfiles/filedetails/?id=2901520677) | `kathanon.showweapontallies` |
| 334 | [Show work site faction relations](https://steamcommunity.com/sharedfiles/filedetails/?id=3487185451) | `riebi.showfactionrelations` |
| 335 | [Simple Leadership](https://steamcommunity.com/sharedfiles/filedetails/?id=3668307448) | `pb3n.simpleleadership` |
| 336 | [Simple sidearms](https://steamcommunity.com/sharedfiles/filedetails/?id=927155256) | `petetimessix.simplesidearms` |
| 337 | [SimpleCameraSetting](https://steamcommunity.com/sharedfiles/filedetails/?id=3232415388) | `ray1203.simplecamerasetting` |
| 338 | [Sin's Softer Hair Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=3790361865) | `sinner.softerhaircolors` |
| 339 | [Skilled Refueling](https://steamcommunity.com/sharedfiles/filedetails/?id=2873628478) | `mrhydralisk.skilledrefueling` |
| 340 | [Skilled Slaughtering](https://steamcommunity.com/sharedfiles/filedetails/?id=2873284405) | `mrhydralisk.skilledslaughtering` |
| 341 | [Smart Medicine - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526251680) | `memegoddess.smartmedicine` |
| 342 | [Smart Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=1504723424) | `sarg.smartspeed` |
| 343 | [Smarter Construction](https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773) | `dhultgren.smarterconstruction` |
| 344 | [Smarter Deconstruction and Mining (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3261302741) | `mlie.smarterdeconstructionandmining` |
| 345 | [Smarter Visitors](https://steamcommunity.com/sharedfiles/filedetails/?id=3012491521) | `mlie.smartervisitors` |
| 346 | [Sovs hairs and beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3368700799) | `sov.hairbeardandstuff` |
| 347 | [Spacer Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3247891820) | `det.spacerarsenal` |
| 348 | [Sparkling Worlds - Full Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=1123043922) | `albion.sparklingworlds.full` |
| 349 | [Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3386164757) | `cat.stellicarsenal` |
| 350 | [Stop, Drop, And Roll! [BAL]](https://steamcommunity.com/sharedfiles/filedetails/?id=2362707956) | `balistafreak.stopdropandroll` |
| 351 | [Storage Stats in Description](https://steamcommunity.com/sharedfiles/filedetails/?id=3740185419) | `ferny.storagestatsindescription` |
| 352 | [Style Change Anytime](https://steamcommunity.com/sharedfiles/filedetails/?id=2880145066) | `dra.stylechangeanytime` |
| 353 | [SurgeryImproved](https://steamcommunity.com/sharedfiles/filedetails/?id=3233032071) | `darkshadow44.surgeryimproved` |
| 354 | [Tabula Rasa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) | `neronix17.toolbox` |
| 355 | [TD Enhancement Pack - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3525414162) | `memegoddess.tdpack` |
| 356 | [TDS Bug Fixes - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3529433984) | `memegoddess.tdsbugfixes` |
| 357 | [Text Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3773691016) | `ferny.texttool` |
| 358 | [The Sims Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3604588393) | `goji.thesimstraits` |
| 359 | [The Vanity Project - Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3143138787) | `thevanityproject.beards` |
| 360 | [Toggleable Shields](https://steamcommunity.com/sharedfiles/filedetails/?id=3261317430) | `owlchemist.toggleableshields` |
| 361 | [Tooltipped Long Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2303452428) | `reiquard.tooltippedlongnames` |
| 362 | [Trade Ships Drop Spot](https://steamcommunity.com/sharedfiles/filedetails/?id=1969732297) | `smashphil.dropspot` |
| 363 | [Trader Show Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3007612546) | `nachotoast.tradershowxenotype` |
| 364 | [Trait Rarity Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=1751884355) | `carnysenpai.traitraritycolors` |
| 365 | [Useful Marks](https://steamcommunity.com/sharedfiles/filedetails/?id=3506573327) | `andromeda.usefulmarks` |
| 366 | [Tribal Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=2879649038) | `shenanigans.tribalbackstories1.4` |
| 367 | [Triumphant Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3647618250) | `ferny.triumphantresearch` |
| 368 | [True Non-Senescence](https://steamcommunity.com/sharedfiles/filedetails/?id=3547377353) | `lunardawn.truenonsenescence` |
| 369 | [Ugly Together](https://steamcommunity.com/sharedfiles/filedetails/?id=2884233923) | `uglytogether.leeh` |
| 370 | [Ultimate Colonist Name Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3498692639) | `hol.ultcolnames` |
| 371 | [Unburned](https://steamcommunity.com/sharedfiles/filedetails/?id=3538336699) | `andromeda.unburned` |
| 372 | [Undraft After Tucking](https://steamcommunity.com/sharedfiles/filedetails/?id=2157495459) | `madarauchiha.undraftaftertucking` |
| 373 | [Unique Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3707263149) | `shunter.uniqueweaponsunbound` |
| 374 | [Universal Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3540066516) | `trigger.universalblueprints` |
| 375 | [Use Bedrolls - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3534169351) | `memegoddess.usebedrolls` |
| 376 | [Useless Hair (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3044216509) | `zal.uselesshair` |
| 377 | [Vanilla Animals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2871933948) | `vanillaexpanded.vanillaanimalsexpanded` |
| 378 | [Vanilla Animals Expanded — Waste Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=2962126499) | `vanillaexpanded.vaewaste` |
| 379 | [Vanilla Anomaly Expanded - Insanity](https://steamcommunity.com/sharedfiles/filedetails/?id=3342412435) | `vanillaexpanded.vanomalyeinsanity` |
| 380 | [Vanilla Apparel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814987817) | `vanillaexpanded.vappe` |
| 381 | [Vanilla Apparel Expanded — Accessories](https://steamcommunity.com/sharedfiles/filedetails/?id=2521176396) | `vanillaexpanded.vaeaccessories` |
| 382 | [Vanilla Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3273371966) | `det.vanillaarsenal` |
| 383 | [Vanilla Backstories Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2861806869) | `vanillaexpanded.vanillabackstoriesexpanded` |
| 384 | [Vanilla Events Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742) | `vanillaexpanded.vee` |
| 385 | [Vanilla Factions Expanded - Empire](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380) | `oskarpotocki.vfe.empire` |
| 386 | [Vanilla Factions Expanded - Insectoids 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431) | `oskarpotocki.vfe.insectoid2` |
| 387 | [Vanilla Factions Expanded - Medieval 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874) | `oskarpotocki.vfe.medieval2` |
| 388 | [Vanilla Factions Expanded - Settlers](https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119) | `oskarpotocki.vanillafactionsexpanded.settlersmodule` |
| 389 | [Vanilla Factions Expanded - Tribals](https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283) | `oskarpotocki.vfe.tribals` |
| 390 | [Vanilla Furniture Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143) | `vanillaexpanded.vfecore` |
| 391 | [Vanilla Furniture Expanded - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=2608762624) | `vanillaexpanded.vfearchitect` |
| 392 | [Vanilla Furniture Expanded - Art](https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023) | `vanillaexpanded.vfeart` |
| 393 | [Vanilla Furniture Expanded - Factory](https://steamcommunity.com/sharedfiles/filedetails/?id=3686924415) | `vanillaexpanded.vfefactory` |
| 394 | [Vanilla Furniture Expanded - Farming](https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779) | `vanillaexpanded.vfefarming` |
| 395 | [Vanilla Furniture Expanded - Medical Module](https://steamcommunity.com/sharedfiles/filedetails/?id=1718191613) | `vanillaexpanded.vfemedical` |
| 396 | [Vanilla Furniture Expanded - Power](https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477) | `vanillaexpanded.vfepower` |
| 397 | [Vanilla Furniture Expanded - Production](https://steamcommunity.com/sharedfiles/filedetails/?id=1880253632) | `vanillaexpanded.vfeproduction` |
| 398 | [Vanilla Furniture Expanded - Props and Decor](https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149) | `vanillaexpanded.vfepropsanddecor` |
| 399 | [Vanilla Furniture Expanded - Security](https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007) | `vanillaexpanded.vfesecurity` |
| 400 | [Vanilla Furniture Expanded - Spacer Module](https://steamcommunity.com/sharedfiles/filedetails/?id=2028381079) | `vanillaexpanded.vfespacer` |
| 401 | [Vanilla Gravship Expanded - Chapter 1](https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606) | `vanillaexpanded.gravship` |
| 402 | [Vanilla Hair Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256) | `vanillaexpanded.vhe` |
| 403 | [Vanilla Helixien Gas Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2877699803) | `vanillaexpanded.helixiengas` |
| 404 | [Vanilla Ideology Expanded - Hats and Rags](https://steamcommunity.com/sharedfiles/filedetails/?id=2567387768) | `vanillaexpanded.viehar` |
| 405 | [Vanilla Ideology Expanded - Icons and Symbols](https://steamcommunity.com/sharedfiles/filedetails/?id=2552609458) | `vanillaexpanded.ideo.iconsandsymbols` |
| 406 | [Vanilla Ideology Expanded - Relics and Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2564895018) | `vanillaexpanded.ideo.relicsandartifacts` |
| 407 | [Vanilla Ideology Expanded - Sophian Style](https://steamcommunity.com/sharedfiles/filedetails/?id=3194606539) | `vanillaexpanded.ideo.sophianstyle` |
| 408 | [Vanilla Landmarks Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229) | `vanillaexpanded.vexploratione` |
| 409 | [Vanilla Loot Beams](https://steamcommunity.com/sharedfiles/filedetails/?id=3746929144) | `astryl.vanillalootbeams` |
| 410 | [Vanilla Nutrient Paste Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763) | `vanillaexpanded.vnutriente` |
| 411 | [Vanilla Plants Expanded - Succulents](https://steamcommunity.com/sharedfiles/filedetails/?id=2198652536) | `vanillaexpanded.vplantsesucculents` |
| 412 | [Vanilla Quests Expanded - Ancients](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) | `vanillaquestsexpanded.ancients` |
| 413 | [Vanilla Quests Expanded - The Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3411401573) | `vanillaquestsexpanded.generator` |
| 414 | [Vanilla Races Expanded - Custom Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2917311689) | `vanillaracesexpanded.customicons` |
| 415 | [Vanilla Races Expanded - Fungoid](https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053) | `vanillaracesexpanded.fungoid` |
| 416 | [Vanilla Races Expanded - Genie](https://steamcommunity.com/sharedfiles/filedetails/?id=2901424072) | `vanillaracesexpanded.genie` |
| 417 | [Vanilla Races Expanded - Highmate](https://steamcommunity.com/sharedfiles/filedetails/?id=2995385834) | `vanillaracesexpanded.highmate` |
| 418 | [Vanilla Races Expanded - Hussar](https://steamcommunity.com/sharedfiles/filedetails/?id=2893586390) | `vanillaracesexpanded.hussar` |
| 419 | [Vanilla Races Expanded - Lycanthrope](https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100) | `vanillaracesexpanded.lycanthrope` |
| 420 | [Vanilla Races Expanded - Phytokin](https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805) | `vanillaracesexpanded.phytokin` |
| 421 | [Vanilla Races Expanded - Pigskin](https://steamcommunity.com/sharedfiles/filedetails/?id=3164798809) | `vanillaracesexpanded.pigskin` |
| 422 | [Vanilla Races Expanded - Sanguophage](https://steamcommunity.com/sharedfiles/filedetails/?id=2963116383) | `vanillaracesexpanded.sanguophage` |
| 423 | [Vanilla Races Expanded - Saurid](https://steamcommunity.com/sharedfiles/filedetails/?id=2880990495) | `vanillaracesexpanded.saurid` |
| 424 | [Vanilla Races Expanded - Starjack](https://steamcommunity.com/sharedfiles/filedetails/?id=3531912428) | `vanillaracesexpanded.starjack` |
| 425 | [Vanilla Races Expanded - Waster](https://steamcommunity.com/sharedfiles/filedetails/?id=2983471725) | `vanillaracesexpanded.waster` |
| 426 | [Vanilla Recycling Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3155781848) | `vanillaexpanded.recycling` |
| 427 | [Vanilla Skills Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3400246558) | `vanillaexpanded.skills` |
| 428 | [Vanilla Social Interactions Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083) | `vanillaexpanded.vanillasocialinteractionsexpanded` |
| 429 | [Vanilla Storytellers Expanded - Perry Persistent](https://steamcommunity.com/sharedfiles/filedetails/?id=2149702069) | `vse.perrypersistent` |
| 430 | [Vanilla Temperature Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258) | `vanillaexpanded.temperature` |
| 431 | [Vanilla Traits Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655) | `vanillaexpanded.vanillatraitsexpanded` |
| 432 | [Vanilla Weapons Expanded - Bioferrite](https://steamcommunity.com/sharedfiles/filedetails/?id=3287469135) | `vanillaexpanded.vwebf` |
| 433 | [Vanilla Weapons Expanded - Coilguns](https://steamcommunity.com/sharedfiles/filedetails/?id=2375692535) | `vanillaexpanded.vwec` |
| 434 | [Vanilla Weapons Expanded - Grenades](https://steamcommunity.com/sharedfiles/filedetails/?id=2194472657) | `vanillaexpanded.vweg` |
| 435 | [Vanilla Weapons Expanded - Makeshift](https://steamcommunity.com/sharedfiles/filedetails/?id=2419690698) | `vanillaexpanded.vwems` |
| 436 | [Vanilla Weapons Expanded - Non-Lethal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918354) | `vanillaexpanded.vwenl` |
| 437 | [Vanilla Weapons Expanded - Tribal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918552) | `vanillaexpanded.vwetb` |
| 438 | [Vanometric Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3041187151) | `hlx.vanometricgenerator` |
| 439 | [Various Space Ship Chunk (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014616487) | `mlie.variousspaceshipchunk` |
| 440 | [Venerated Xenotype Flexibility (Revisited)](https://steamcommunity.com/sharedfiles/filedetails/?id=3586153375) | `dcszar.veneratedxenotypeflexibility` |
| 441 | [VFE Pack for Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3612525913) | `cantaloupetheclown.vanillafurnitureexpandedpack` |
| 442 | [Victor Veto - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3745138223) | `ferny.victorveto` |
| 443 | [Volt Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3551721422) | `det.voltweaponry` |
| 444 | [VV - New Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=3448458106) | `vvenchov.vvnewharvest` |
| 445 | [WanderJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177016016) | `ogliss.wanderjoinsplus` |
| 446 | [We Are United](https://steamcommunity.com/sharedfiles/filedetails/?id=3030804445) | `sl4vp0wer.weareunited` |
| 447 | [Weather Duration](https://steamcommunity.com/sharedfiles/filedetails/?id=2822836257) | `kathanon.weatherduration` |
| 448 | [What's Missing?](https://steamcommunity.com/sharedfiles/filedetails/?id=3231090162) | `revolus.whatsmissing` |
| 449 | [What's That Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2258431182) | `co.uk.epicguru.whatsthatmod` |
| 450 | [Who shot my leg off?](https://steamcommunity.com/sharedfiles/filedetails/?id=3491552121) | `tixiv.whoshotmylegoff` |
| 451 | [Wololoo - Better Conversion and Recruitment](https://steamcommunity.com/sharedfiles/filedetails/?id=3108763487) | `redmattis.betterconversion` |
| 452 | [Won hair_men](https://steamcommunity.com/sharedfiles/filedetails/?id=1879950403) | `won.hair` |
| 453 | [Won hair_women](https://steamcommunity.com/sharedfiles/filedetails/?id=1886150617) | `won2.hair` |
| 454 | [Work History](https://steamcommunity.com/sharedfiles/filedetails/?id=3727287885) | `lebek.workhistory` |
| 455 | [Xenotype Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3787666264) | `ceramic.xenotypebackstories` |
| 456 | [Xenotype Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832924427) | `lakinmanheim.xenotypedescriptions` |
| 457 | [Xenotype Summary](https://steamcommunity.com/sharedfiles/filedetails/?id=3710012135) | `zhuma.xenotypesummary` |
| 458 | [Xeva's Rimhair](https://steamcommunity.com/sharedfiles/filedetails/?id=705924057) | `orion.xevarimhair` |
| 459 | [[AV] Ancient Spacer Xenos](https://steamcommunity.com/sharedfiles/filedetails/?id=3241589828) | `veltaris.xenos` |
| 460 | [[AV] Kosmian Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3774474578) | `veltaris.kosmian` |
| 461 | [[AV] Mechanoid orbital traders](https://steamcommunity.com/sharedfiles/filedetails/?id=3172198798) | `veltaris.mechcrate` |
| 462 | [[AV] Mechtech](https://steamcommunity.com/sharedfiles/filedetails/?id=3339795890) | `veltaris.mechtech` |
| 463 | [[AV] Toxic Waste Disposer](https://steamcommunity.com/sharedfiles/filedetails/?id=3440190384) | `veltaris.wastecollector` |
| 464 | [[BTD] Gravship Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3575162262) | `btd.remix.gravshipblueprints` |
| 465 | [[ETF] Butchering Now Yields Hemogen!](https://steamcommunity.com/sharedfiles/filedetails/?id=3688485013) | `echotidefish.butcheringnowyieldshemogen` |
| 466 | [[LFS] UI Rethemed - Factions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832850311) | `lazyfridaystudio.uirethemed.factions` |
| 467 | [[LFS] UI Rethemed - Ideology](https://steamcommunity.com/sharedfiles/filedetails/?id=3834172773) | `lazyfridaystudio.uirethemed.ideology` |
| 468 | [[NL] Facial Animation - Experimentals](https://steamcommunity.com/sharedfiles/filedetails/?id=2581693737) | `nals.facialanimationexperimentals` |
| 469 | [Achtung!](https://steamcommunity.com/sharedfiles/filedetails/?id=730936602) | `brrainz.achtung` |
| 470 | [AFU女士发型_Women's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2020779666) | `jintuzilamian.hairstyle.afuwomenshairstyles` |
| 471 | [Alpha Props - Parks and Gardens](https://steamcommunity.com/sharedfiles/filedetails/?id=3146268928) | `sarg.alphapropsparks` |
| 472 | [Alpha Skills](https://steamcommunity.com/sharedfiles/filedetails/?id=3448953006) | `sarg.alphaskills` |
| 473 | [Biomes! Core](https://steamcommunity.com/sharedfiles/filedetails/?id=2038000893) | `biomesteam.biomescore` |
| 474 | [Capture Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787736781) | `ferny.captureexpansion` |
| 475 | [Circinus Performance Analyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=3773680130) | `astryl.circinus` |
| 476 | [Coilguns Addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3788730287) | `aqued.coilguns` |
| 477 | [Colony Manager Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=3310027356) | `ilyvion.colonymanagerredux` |
| 478 | [Decor into Styles](https://steamcommunity.com/sharedfiles/filedetails/?id=3771249138) | `ferny.decorintostyles` |
| 479 | [Expanded Prosthetics and Organ Engineering - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=1949064302) | `vat.epoeforked` |
| 480 | [FEB's Biopunk Livestock](https://steamcommunity.com/sharedfiles/filedetails/?id=3556058978) | `feb.biopunklivestock` |
| 481 | [Fix Multi Stack Hoppers](https://steamcommunity.com/sharedfiles/filedetails/?id=3534591980) | `zokradonh.supporthoppermultistacks` |
| 482 | [Flourish - Everyday Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3810518822) | `astryl.flourisheveryday` |
| 483 | [Fortifications - Industrial](https://steamcommunity.com/sharedfiles/filedetails/?id=2561619583) | `aoba.fortress.industrial` |
| 484 | [Gerrymon's Misc Props: Dungeon](https://steamcommunity.com/sharedfiles/filedetails/?id=3772571876) | `gmmp.dungeon` |
| 485 | [Gravship Fleet](https://steamcommunity.com/sharedfiles/filedetails/?id=3787239201) | `asunib.gravshipfleet` |
| 486 | [Gravship Raids](https://steamcommunity.com/sharedfiles/filedetails/?id=3767338163) | `sk.gravshipraids` |
| 487 | [Helixien Gas Brazier](https://steamcommunity.com/sharedfiles/filedetails/?id=2770815124) | `cixwow.helixienbrazier` |
| 488 | [Helixien Gas Production](https://steamcommunity.com/sharedfiles/filedetails/?id=2829208414) | `cixwow.helixiengasproduction` |
| 489 | [Hospitality (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3509486825) | `orion.hospitality` |
| 490 | [IMPACT - Combat Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3812370282) | `astryl.impact` |
| 491 | [Improved Map Search](https://steamcommunity.com/sharedfiles/filedetails/?id=3547866455) | `kathanon.groupedmapsearch` |
| 492 | [Info Card Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=3776660810) | `kaamalauppias.infocardplus` |
| 493 | [Junkie Knick Knacks](https://steamcommunity.com/sharedfiles/filedetails/?id=3607387774) | `ucp.junkieprops` |
| 494 | [More Phytokin Xenotypes](https://steamcommunity.com/sharedfiles/filedetails/?id=3813857865) | `pumpkin.morephytokinxenotypes` |
| 495 | [Nice Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3328729902) | `andromeda.nicehealthtab` |
| 496 | [Nice Research Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3773496046) | `andromeda.niceresearchtab` |
| 497 | [Permits: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3521089734) | `smxrez.permitsreexamined` |
| 498 | [Power: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3590186274) | `smxrez.powerreexamined` |
| 499 | [Reel's Expanded Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3237638097) | `reel.expanded.storage` |
| 500 | [Rimsenal - Hair pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725949967) | `rimsenal.hair` |
| 501 | [Searchable Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2928608119) | `kathanon.searchablemenus` |
| 502 | [Show Me Your Tools - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3774821567) | `meathax.showmeyourtools` |
| 503 | [Sleek Work Schedule Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3742689571) | `squishyjellyfish.nicescheduletab` |
| 504 | [Trait and Backstory Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2873494547) | `superniquito.traiticons` |
| 505 | [Universal Blueprints Alpha Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3573901897) | `trigger.universalblueprints.alphapack` |
| 506 | [Vanilla Armour Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814988282) | `vanillaexpanded.varme` |
| 507 | [Vanilla Chemfuel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2792917473) | `vanillaexpanded.vchemfuele` |
| 508 | [Vanilla Cooking Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519) | `vanillaexpanded.vcooke` |
| 509 | [Vanilla Expanded: Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3313700572) | `ferny.vanillaexpandedherobackgrounds` |
| 510 | [Vanilla Factions Expanded - Deserters](https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377) | `oskarpotocki.vfe.deserters` |
| 511 | [Vanilla Fishing Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942) | `vanillaexpanded.vcef` |
| 512 | [Vanilla Persona Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2826922787) | `vanillaexpanded.vpersonaweaponse` |
| 513 | [Vanilla Psycasts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) | `vanillaexpanded.vpsycastse` |
| 514 | [Vanilla Skills Rexamined](https://steamcommunity.com/sharedfiles/filedetails/?id=3235834179) | `gravenwitch.vsrexamined` |
| 515 | [Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360) | `vanillaexpanded.vwe` |
| 516 | [VFE - Insectoids 2 Deconstructable Infested Ship Chunks](https://steamcommunity.com/sharedfiles/filedetails/?id=3641480952) | `decinfshipchunkspatch.vfe.insectoid2` |
| 517 | [VSIE - Rational Trait Development](https://steamcommunity.com/sharedfiles/filedetails/?id=2916405546) | `stagz.vsierationaltraitdevelopment` |
| 518 | [Zalcore Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3104631106) | `zal.prefabs` |
| 519 | [[AV] Mechanoid Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=3745387929) | `veltaris.mechstations` |
| 520 | [[BTD] Ship Pack: Community Designs](https://steamcommunity.com/sharedfiles/filedetails/?id=3832496468) | `btd.gbp.shippack.communitydesigns` |
| 521 | [[FSF] Advanced Bionics Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2006925330) | `frozensnowfox.advancedbionicsexpansion` |
| 522 | [Auto links](https://steamcommunity.com/sharedfiles/filedetails/?id=2059389912) | `automatic.autolinks` |
| 523 | [Biomes! Oasis](https://steamcommunity.com/sharedfiles/filedetails/?id=2538518381) | `biomesteam.oasis` |
| 524 | [Bradson's Main Button Icons (Forked + Expanded)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532359201) | `bs.mbifvte` |
| 525 | [Busywork](https://steamcommunity.com/sharedfiles/filedetails/?id=3775253009) | `andromeda.busywork` |
| 526 | [Cat's Boots and Gloves](https://steamcommunity.com/sharedfiles/filedetails/?id=3552813398) | `catlover366.bootsandgloves` |
| 527 | [Clean Command Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3688400757) | `leo.cleancommandbar` |
| 528 | [EPOE-Forked: Royalty DLC expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2008970276) | `vat.epoeforkedroyalty` |
| 529 | [Farming Hysteresis](https://steamcommunity.com/sharedfiles/filedetails/?id=2431191516) | `ilyvion.farminghysteresis` |
| 530 | [Fortification Industrial - Citadel](https://steamcommunity.com/sharedfiles/filedetails/?id=2887532326) | `aoba.fortress.industrial.citadel` |
| 531 | [Fortification Industrial - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=3093455596) | `aoba.fortress.frontier` |
| 532 | [Fortification Industrial -Nuclear Dawn](https://steamcommunity.com/sharedfiles/filedetails/?id=2733185331) | `aoba.fortress.industrial.nucleardawn` |
| 533 | [Genderbending Corrupted Obelisk](https://steamcommunity.com/sharedfiles/filedetails/?id=3350774891) | `abraxas.genderbending.corrupted.obelisk` |
| 534 | [Genetic Biotech for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317537) | `sd.fa.geneticheadsbiotech` |
| 535 | [Genetic Mods for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317734) | `sd.fa.geneticheadsmods` |
| 536 | [Hospitality + Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3789536584) | `fumblesneeze.guestbedgizmo` |
| 537 | [Hospitality - Invite to Stay](https://steamcommunity.com/sharedfiles/filedetails/?id=3783522506) | `gatoviejo.hospitality.invitetostay` |
| 538 | [Hospitality: Casino](https://steamcommunity.com/sharedfiles/filedetails/?id=2939292644) | `adamas.hospitalitycasino` |
| 539 | [Hospitality: Vending machines](https://steamcommunity.com/sharedfiles/filedetails/?id=3014885065) | `adamas.vendingmachines` |
| 540 | [Industrial Hardleather, Concrete and Cheese](https://steamcommunity.com/sharedfiles/filedetails/?id=3535926848) | `talias.vfehardleatherconcrete` |
| 541 | [Legendary Boss](https://steamcommunity.com/sharedfiles/filedetails/?id=3538894636) | `andromeda.legendaryboss` |
| 542 | [Modern Psycasts UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3745137736) | `astryl.modernpsycastsui` |
| 543 | [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) | `andromeda.nicebilltab` |
| 544 | [Nice Plants Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3685058533) | `andromeda.niceplantsmenu` |
| 545 | [Nicer Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3701712250) | `that0neperson.nhtprostheticsextended` |
| 546 | [ReGrowth 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569) | `regrowth.botr.core` |
| 547 | [RimQuest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2263331727) | `mlie.rimquest` |
| 548 | [Useful Marks in Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3720856279) | `truemogician.usefulmarksincolonygroups` |
| 549 | [Vanilla Fishing Expanded - Fishing Treasures AddOn](https://steamcommunity.com/sharedfiles/filedetails/?id=2468543398) | `vanillaexpanded.vcefaddon` |
| 550 | [Vanilla Psycasts Expanded - Hemosage](https://steamcommunity.com/sharedfiles/filedetails/?id=2990596478) | `vanillaexpanded.vpe.hemosage` |
| 551 | [Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=2816938779) | `vanillaexpanded.vtexe.facialanims` |
| 552 | [Vanilla Weapons Expanded - Heavy Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) | `vanillaexpanded.vwehw` |
| 553 | [Vanilla Weapons Expanded - Laser](https://steamcommunity.com/sharedfiles/filedetails/?id=1989352844) | `vanillaexpanded.vwel` |
| 554 | [Vanilla Weapons Expanded - Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=1906780517) | `vanillaexpanded.vweq` |
| 555 | [A Dog Said... Animal Prosthetics 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3238353862) | `sambucher.adogsaidanimalprosthetics2` |
| 556 | [Biomes! Polluted Lands](https://steamcommunity.com/sharedfiles/filedetails/?id=3390196656) | `biomesteam.biomespollutedlands` |
| 557 | [Bionic icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1677616980) | `automatic.bionicicons` |
| 558 | [Bionic Irises for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3828692274) | `neztech.fabioniciris` |
| 559 | [Drafted Auto-Combat](https://steamcommunity.com/sharedfiles/filedetails/?id=3546270076) | `redmattis.autocombat` |
| 560 | [Glitter Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3768428526) | `zal.glitterweaponry` |
| 561 | [Gulden Biome (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607066070) | `pphhyy.guldennew` |
| 562 | [More and Retextured Head Shapes for VTE for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3407049972) | `sd.fa.vte.moreheadshapes` |
| 563 | [Nice Bill Tab - Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721023311) | `hicon.nicebilltabexpansion` |
| 564 | [Stem's VWE Industrial Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787566245) | `stem.vweindustrialexpansion` |
| 565 | [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948) | `oskarpotocki.vfe.pirates` |
| 566 | [Vanilla Plants Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308522) | `vanillaexpanded.vplantse` |
| 567 | [Vanilla Textures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324) | `vanillaexpanded.vtexe` |
| 568 | [Food Texture Variety Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3354455179) | `goat.food.texture.variety.core` |
| 569 | [Vanilla Weapons Expanded - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918139) | `vanillaexpanded.vweft` |
| 570 | [WVC - Work Modes](https://steamcommunity.com/sharedfiles/filedetails/?id=2888380373) | `wvc.sergkart.biotech.moremechanoidsworkmodes` |
| 571 | [Altered Carbon 2: ReSleeved](https://steamcommunity.com/sharedfiles/filedetails/?id=2196278117) | `hlx.ultratechalteredcarbon` |
| 572 | [Eltex Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532192464) | `zal.eltexweaponry` |
| 573 | [More Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=2707120862) | `tidal.morevanilla.textures` |
| 574 | [Progression: Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3361425712) | `ferny.progressionstorytellers` |
| 575 | [Unique Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=3776637622) | `shunter.uniquemeleeweapons` |
| 576 | [Vanilla Plants Expanded - More Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667) | `vanillaexpanded.vplantsemore` |
| 577 | [Vanilla Plants Expanded - Mushrooms](https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281) | `vanillaexpanded.vplantsemushrooms` |
| 578 | [Vanilla Races Expanded - Insector](https://steamcommunity.com/sharedfiles/filedetails/?id=3260509684) | `vanillaracesexpanded.insector` |
| 579 | [Alpha Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856) | `sarg.alphaanimals` |
| 580 | [Better Trees: More Djeeshka Like Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3543705507) | `maal.bettertreesmod` |
| 581 | [Vanilla Gravship Expanded - Chapter 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3799737423) | `vanillaexpanded.gravship2` |
| 582 | [Vanilla Storytellers Expanded - Igor Invader (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3582195822) | `zal.vseigorinvader` |
| 583 | [VV - New Harvest: Compatibility Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3570411438) | `octodecoy.vvnewharvest.compatpatches` |
| 584 | [Alpha Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502) | `sarg.alphagenes` |
| 585 | [[FSF] Better Exploration Loot](https://steamcommunity.com/sharedfiles/filedetails/?id=3526957922) | `frozensnowfox.betterexplorationloot` |
| 586 | [Alpha Genes Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3751293981) | `lucius.alphagenesfa` |
| 587 | [Alpha Genes Integrated](https://steamcommunity.com/sharedfiles/filedetails/?id=3538704272) | `kupa.alphagenesintegrated` |
| 588 | [Keyz Reference Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3689557041) | `keyz182.keyzreferencetools` |
| 589 | [Bullet Sounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3806470380) | `senilia.bulletsounds` |
| 590 | [Cherry picker scroll bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3781285156) | `lucius.cherryscroll` |
| 591 | [Ambience Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3835975372) | `neko.ambiencecore` |
| 592 | [Thunderstorm](https://steamcommunity.com/sharedfiles/filedetails/?id=3278460897) | `neko.thunderstorm` |
| 593 | [Shortcuts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503701770) | `mlie.shortcuts` |
| 594 | [Big Little Mod Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2710382569) | `daria40k.biglittlemodpatch` |
| 595 | [Muzzle Flash](https://steamcommunity.com/sharedfiles/filedetails/?id=2917732219) | `issaczhuang.muzzleflash` |
| 596 | [Turrets For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3800286808) | `bambaryla.allbambaturrets` |
| 597 | [Trystan Traveller's Shuttle Schematics](https://steamcommunity.com/sharedfiles/filedetails/?id=3655212016) | `trouperton.trystantravellersshuttleschematics` |
| 598 | [Help me build](https://steamcommunity.com/sharedfiles/filedetails/?id=3534699220) | `andromeda.helpmebuild` |
| 599 | [Float Menu Performance Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3784467506) | `vorsel.floatmenuperformanceoptimizer` |
| 600 | [Save Your Storage and Tick Every Minute](https://steamcommunity.com/sharedfiles/filedetails/?id=3795549633) | `nanaloveyuki.saveyourstorageandtickeveryminute` |
| 601 | [Quiet Wonders](https://steamcommunity.com/sharedfiles/filedetails/?id=3822357701) | `choco.quietwonders` |
| 602 | [FirefliesTwo](https://steamcommunity.com/sharedfiles/filedetails/?id=3347026598) | `scurvyez.firefliestwo` |
| 603 | [Alpha Books](https://steamcommunity.com/sharedfiles/filedetails/?id=3403180654) | `sarg.alphabooks` |
| 604 | [Progression: Scenarios](https://steamcommunity.com/sharedfiles/filedetails/?id=3378384387) | `ferny.progressionscenarios` |
| 605 | [Tunnelers ARE deep drills - Resource Filter Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3828716806) | `stainum.tunnelersaredeepdrills.resourcefilterpatch` |
| 606 | [Muzzle Flash Editor](https://steamcommunity.com/sharedfiles/filedetails/?id=3800597725) | `dataviewer.muzzleflasheditor` |
| 607 | [SQUISHY Ranged](https://steamcommunity.com/sharedfiles/filedetails/?id=3833016652) | `local.gunrecoil` |
| 608 | [SQUISHY Melee](https://steamcommunity.com/sharedfiles/filedetails/?id=3839642536) | `local.gunrecoil.melee` |
| 609 | [Vanilla Base Generation Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3209927822) | `vanillaexpanded.basegeneration` |
| 610 | [[KCB] Liftoff Effects (이륙 이펙트)](https://steamcommunity.com/sharedfiles/filedetails/?id=3837981505) | `kimchibok.rocketpodlaunch` |
| 611 | [PAWN DEPTH ORDER](https://steamcommunity.com/sharedfiles/filedetails/?id=3839580086) | `local.pawndepth` |
| 612 | [Geological Landforms](https://steamcommunity.com/sharedfiles/filedetails/?id=2773943594) | `m00nl1ght.geologicallandforms` |
| 613 | [Map Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=2800857642) | `m00nl1ght.mappreview` |
| 614 | [Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206) | `xmb.ancienturbanruins.mo` |
| 615 | [Biomes! Caverns](https://steamcommunity.com/sharedfiles/filedetails/?id=2969748433) | `biomesteam.biomescaverns` |
| 616 | [More Prop Categories](https://steamcommunity.com/sharedfiles/filedetails/?id=3167021055) | `ferny.propscore` |
| 617 | [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) | `neronix17.tweaksgalore` |
| 618 | [Decor into Props](https://steamcommunity.com/sharedfiles/filedetails/?id=3771248988) | `ferny.decorintoprops` |
| 619 | [Vanilla Ideology Expanded - Memes and Structures](https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500) | `vanillaexpanded.vmemese` |
| 620 | [Auto-Cast Specialist Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=3459647882) | `linnun.autocastspecialistcommands` |
| 621 | [Reel's Insector Faction](https://steamcommunity.com/sharedfiles/filedetails/?id=3309022698) | `reel.insectorfaction` |
| 622 | [Tastier Vanilla Clothes](https://steamcommunity.com/sharedfiles/filedetails/?id=2808554143) | `al9000.tvc` |
| 623 | [Vanilla Vehicles Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3014906877) | `oskarpotocki.vanillavehiclesexpanded` |
| 624 | [Linkable Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3659197870) | `linnun.linkablegroups` |
| 625 | [Vanilla Vehicles Expanded - Tier 3](https://steamcommunity.com/sharedfiles/filedetails/?id=3047892432) | `oskarpotocki.vanillavehiclesexpandedtier3` |
| 626 | [Vanilla Vehicles Expanded - Upgrades](https://steamcommunity.com/sharedfiles/filedetails/?id=3302208420) | `oskarpotocki.vanillavehiclesexpandedupgrades` |
| 627 | [[LFS] UI Rethemed - Relationships](https://steamcommunity.com/sharedfiles/filedetails/?id=3840876824) | `lazyfridaystudio.uirethemed.relationships` |
| 628 | [Weather Reimagined](https://steamcommunity.com/sharedfiles/filedetails/?id=3838111458) | `evileyeguy.weatherreimagined` |
| 629 | [RimWorld: Living Waters](https://steamcommunity.com/sharedfiles/filedetails/?id=3834245888) | `patrickschwarzer.rivers` |
| 630 | [[WYD] Better Campsites](https://steamcommunity.com/sharedfiles/filedetails/?id=3546818262) | `wyr3d.bettercampsites` |
| 631 | [Art on Neolithic Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2556397158) | `dizzy.neolithicweaponart` |
| 632 | [Balanced Eclipse and Solar Flare - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=2929407615) | `skelly.neceros.balancedeclipseandsolarflare.forked` |
| 633 | [Dimorphic Neanderthals](https://steamcommunity.com/sharedfiles/filedetails/?id=3612381568) | `zoid.femneanderthals` |
| 634 | [DLG.Mutations behind](https://steamcommunity.com/sharedfiles/filedetails/?id=3637525997) | `dlg.mutation.behind` |
| 635 | [Drawn Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=2917791361) | `overpl.drawnspots` |
| 636 | [High Life Drug Cooking Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=3183941628) | `bigtortoise.drugcookingspeed` |
| 637 | [Less Rebuff](https://steamcommunity.com/sharedfiles/filedetails/?id=774543761) | `dingo.lessrebuff` |
| 638 | [More Descriptive Words and Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2180384246) | `hol.words` |
| 639 | [Cultural names: Stellic](https://steamcommunity.com/sharedfiles/filedetails/?id=3161247581) | `hawkwood.culturestellic` |
| 640 | [No Headstone Till Buried](https://steamcommunity.com/sharedfiles/filedetails/?id=2845934338) | `wanzi.noheadstone` |
| 641 | [Smaller radius for Anima Trees, Shrines and Animus Stones](https://steamcommunity.com/sharedfiles/filedetails/?id=2812513517) | `longman.smallerradiusforanimatreesandshrines` |
| 642 | [Soil Contrast](https://steamcommunity.com/sharedfiles/filedetails/?id=1561838700) | `wemd.soilcontrast` |
| 643 | [Det's Xenotypes for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3565987465) | `det.facialanimation` |
| 644 | [Harana chirps quietly](https://steamcommunity.com/sharedfiles/filedetails/?id=3264370565) | `amegakull.haranachirpsquietly` |
| 645 | [Medieval Backstories Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3170653412) | `grasstypefire.medieval.backstoriespatch` |
| 646 | [Normal Stone for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=2968173847) | `mman0385.abnormalstone` |
| 647 | [Thumper - Phercore Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3497174635) | `bb.thumper.pherocore.desc.clarify` |
| 648 | [VE Helixien Gas Space Maps Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3262619470) | `ionfrigate12345.sos2vehelixienpatch` |
| 649 | [VFE - Empire: Frugal Nobles](https://steamcommunity.com/sharedfiles/filedetails/?id=3573280041) | `cn.vfeempirelessrequirement` |
| 650 | [VTE - Proxy Heat Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3439148119) | `ewolfy.vtepatch` |
| 651 | [[NL] Facial Animation - 1.7 Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3821248395) | `local.facialanimation.compat17` |
| 652 | [Reduce Skipdoor Distortion Effects](https://steamcommunity.com/sharedfiles/filedetails/?id=3111751800) | `rabiosus.reduceskipdoordistortioneffect` |
| 653 | [Hospitality: Vending Machines (Adaptive Storage Patch)](https://steamcommunity.com/sharedfiles/filedetails/?id=3319084133) | `katsukiri.hvendingmachineadaptivepatch` |
| 654 | [ReGrowth 2 World Map Beautification for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679302) | `noxilie.regrow.wmb.alphabiomes` |
| 655 | [ReGrowth 2 World Map Beautification for More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679624) | `noxilie.regrow.wmb.morevanillabiomes` |
| 656 | [Simple Immersion - Regrowth Rimclaw Rename](https://steamcommunity.com/sharedfiles/filedetails/?id=3789160274) | `nyr.siregrowthrimclaw` |
| 657 | [Vanilla plus loot expirence for ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3536236647) | `bb.generic.loot.exp.anc.urban.ruins` |
| 658 | [VFE Pirates Warcaskets Vacuum Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3543210594) | `why.vfe.warcasketsvacuumpatch` |
| 659 | [Warcasket weaponry hyperlinks](https://steamcommunity.com/sharedfiles/filedetails/?id=2819075870) | `d0b1k.warcaskethyperlinks` |
| 660 | [Cyanobot's Plant Gen Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3218426325) | `cyanobot.plantgenpatch` |
| 661 | [Phoebe Is Just A Chill Gal \| Progression: Storytellers Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3530139996) | `koberiddle.phoebeisjustachillgal` |
| 662 | [Warcasket Weapon Quality](https://steamcommunity.com/sharedfiles/filedetails/?id=3766043780) | `danzinagri.warcasketweaponquality` |
| 663 | [Anomaly Fleshbeasts or Sarg Fleshbeasts?](https://steamcommunity.com/sharedfiles/filedetails/?id=3234422589) | `ferny.fleshbeasts` |
| 664 | [Consistent Text](https://steamcommunity.com/sharedfiles/filedetails/?id=3578462306) | `ferny.consistenttext` |
| 665 | [Local Tweaks (sammael)](https://github.com/hugalafutro/LocalTweaks) | `sammael.localtweaks` |
| 666 | [Muzzle Flash for Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3451641816) | `stellicarsenal.mf.nihao` |
| 667 | [Wood's Muzzle Flash for Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3442021825) | `charlie.muzzle.flash.for.ancientruins` |
| 668 | [[CM] Ideology DLC Texture Upscale](https://steamcommunity.com/sharedfiles/filedetails/?id=2603637629) | `cm.ideology.upscaled` |
| 669 | [[sbz] Mini Band Node](https://steamcommunity.com/sharedfiles/filedetails/?id=3646711814) | `sbz.minibandnode` |
| 670 | [Alpha Mechs](https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158) | `sarg.alphamechs` |
| 671 | [[FSF] Complex Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=2069684319) | `frozensnowfox.complexjobs` |
| 672 | [Sleek Work Priorities](https://steamcommunity.com/sharedfiles/filedetails/?id=3764537806) | `squishyjellyfish.sleekworkpriorities` |
| 673 | [[sbz] Mini Mech Booster](https://steamcommunity.com/sharedfiles/filedetails/?id=3547537902) | `sbz.minimechbooster` |
| 674 | [[sbz] Mini Wastepack Atomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3668590852) | `sbz.miniwastepackatomizer` |
| 675 | [Childrens Drawings](https://steamcommunity.com/sharedfiles/filedetails/?id=2918817041) | `zylle.childrensdrawings` |
| 676 | [FEB's Colored Gravship Lights](https://steamcommunity.com/sharedfiles/filedetails/?id=3577698779) | `feb.coloredgravlights` |
| 677 | [Injury Texture Variety](https://steamcommunity.com/sharedfiles/filedetails/?id=3561610873) | `keshash.injurytextures` |
| 678 | [Prothesis Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3236905283) | `prothesis.texture` |
| 679 | [Vanilla Genes Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=2905707100) | `redmattis.vanillagenesrebalanced` |
| 680 | [Vanilla Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2748834409) | `neronix17.retexture.vanillahair` |
| 681 | [Vanilla Pawns Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2275310562) | `neronix17.hd.pawns` |
| 682 | [EyeGenes3](https://steamcommunity.com/sharedfiles/filedetails/?id=3745223213) | `lucius.eyegenes3` |
| 683 | [Character Development](https://steamcommunity.com/sharedfiles/filedetails/?id=3770112044) | `ferny.characterdevelopment` |
| 684 | [Vanilla Experimentals for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3753978140) | `sd.fa.movingvanillaeyes` |
| 685 | [Vanilla Upscaled Floors](https://steamcommunity.com/sharedfiles/filedetails/?id=3524080368) | `adagio.vanilla.upscaled.floors` |
| 686 | [Vanilla Upscaled Terrain](https://steamcommunity.com/sharedfiles/filedetails/?id=3525395044) | `adagio.vanilla.upscaled.terrain` |
| 687 | [World Map Enhanced (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3599967849) | `zal.worldmapenhanced` |
| 688 | [[CF] VRE - Lycanthrope Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3578135470) | `cf.lycanthrope` |
| 689 | [[CF] VRE - Sanguophage Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3165181669) | `vres.betterparts` |
| 690 | [[CF] VRE - Saurid Better Head Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3539916610) | `cf.vresaurid` |
| 691 | [[sbz] Rimsenal Bench Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2668988577) | `jgh.rimsenalbench` |
| 692 | [Japester's Upscaled Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3788774863) | `japester.upscaledtextures.4x` |
| 693 | [Nice Hands Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3302328100) | `andromeda.nicehands` |
| 694 | [Optional Icons for Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1966995052) | `proxyer.optionalicons4ai` |
| 695 | [Pumpkinlass Visible Pants Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3772689105) | `pumpkinlass.pantsretexture` |
| 696 | [Retextured! - RT Fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=2979449586) | `soulretextured.rtfuse` |
| 697 | [Vanilla Textures Expanded - Variations](https://steamcommunity.com/sharedfiles/filedetails/?id=2493234474) | `vanillaexpanded.vtexvariations` |
| 698 | [ReBuild: Doors and Corners](https://steamcommunity.com/sharedfiles/filedetails/?id=3262718980) | `rebuild.cotr.doorsandcorners` |
| 699 | [ReBuild: Cell Door Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3598547615) | `rebuild.celldoorfix` |
| 700 | [Shelf Stack Logistics Reworked](https://steamcommunity.com/sharedfiles/filedetails/?id=3724884785) | `redmattis.shelflogistics` |
| 701 | [VFE - Settlers textures revamped](https://steamcommunity.com/sharedfiles/filedetails/?id=3685232860) | `regen.vfestexturesrevamped` |
| 702 | [Loading Screen Repository](https://steamcommunity.com/sharedfiles/filedetails/?id=3567860027) | `ferny.loadingscreenrepository` |
| 703 | [[FSF] FrozenSnowFox Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2893432492) | `frozensnowfox.frozensnowfoxtweaks` |
| 704 | [Mechanitor Encounters](https://steamcommunity.com/sharedfiles/filedetails/?id=3417287863) | `makinglovetomy.warqueenmechwife.allnightlong` |
| 705 | [MSS Tweaks and Fun](https://steamcommunity.com/sharedfiles/filedetails/?id=3379574408) | `mss.flavourpack` |
| 706 | [Better Architect Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3563882422) | `ferny.betterarchitect` |
| 707 | [Too Many Mods - Compats and Rebalances](https://steamcommunity.com/sharedfiles/filedetails/?id=3250762483) | `wara.toomanymods` |
| 708 | [Temporary Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3424105991) | `ferny.temporarypatches` |
| 709 | [Wild Crops, a fork of Wild Spawn](https://steamcommunity.com/sharedfiles/filedetails/?id=3776658466) | `somethingsomething.plantsandstuff` |
| 710 | [Xeva's Rimhair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3132223992) | `neronix17.retexture.xevarimhair` |
| 711 | [[sbz] EPOE Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2616058883) | `jgh.epoe` |
| 712 | [EPOE-Forked Prosthesis Eye Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3489953096) | `nyx.epoeforkedtex` |
| 713 | [Rimsenal Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2747368450) | `neronix17.retexture.rimsenalrimhair` |
| 714 | [Upscaled - Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3766152968) | `ntxt.vweupscale` |
| 715 | [Vanilla Chemfuel Expanded Retexture and Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=2951745069) | `nomadrw.retextureandrestylevanillachemfuelexpanded` |
| 716 | [Adjustments for Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3532050275) | `sd.fa.vteadjustments` |
| 717 | [Bedrooms are always better than Barracks](https://steamcommunity.com/sharedfiles/filedetails/?id=3533595687) | `redmattis.bedroomnotbarracks` |
| 718 | [ReBuild: Doors and Corners NO Perspective Doors](https://steamcommunity.com/sharedfiles/filedetails/?id=3584349796) | `st.rusik.rebuild.no.perspective.doors` |
| 719 | [Upscaled - VWE Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=3780787134) | `ntxt.upscaled.vwequickdraw` |
| 720 | [[CF] Anomaly Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3538584455) | `cf.anomalyupscaled2x` |
| 721 | [[CF] Bionic Icons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3239007545) | `bionicicons.hd` |
| 722 | [[CF] Odyssey Upscaled 2x](https://steamcommunity.com/sharedfiles/filedetails/?id=3535506453) | `cf.odysseyupscaled2x` |
| 723 | [[LM] LusTech Vanilla Weapons Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3602372541) | `lusty.lustechvwr` |
| 724 | [[S16] Royalty Weapons HD](https://steamcommunity.com/sharedfiles/filedetails/?id=2826282032) | `s16.rwhd` |
| 725 | [[sbz] Mini Mech Rechargers](https://steamcommunity.com/sharedfiles/filedetails/?id=3554922769) | `sbz.minimechrecharger` |
| 726 | [[sbz] WVC - Work Modes Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2913464579) | `jgh.wvcwvc` |
| 727 | [ATH's Retexture Female Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3145326932) | `anthitei.athsretexturefemalapparel.retexture` |
| 728 | [Biotech Mechanoid Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3164022710) | `el.biotechmechrt` |
| 729 | [Erin's Body Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2662457442) | `erin.body.texture` |
| 730 | [Gerrymon's Upscaled Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3276562906) | `gerrymon.uvt` |
| 731 | [Gerrymon's Misc Props: Graveyard](https://steamcommunity.com/sharedfiles/filedetails/?id=3780458386) | `gmmp.graveyard` |
| 732 | [Sexier Visage Masks](https://steamcommunity.com/sharedfiles/filedetails/?id=3638159640) | `gerrymon.sexymasks` |
| 733 | [Mechanoid Cluster Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3523231836) | `happycam.conditioncauserretexture` |
| 734 | [Van's Retexture : Mechanitor](https://steamcommunity.com/sharedfiles/filedetails/?id=2943977908) | `sirvan.mechanitorretexture` |
| 735 | [Van's Retexture : Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2922441211) | `sirvan.mwretextured` |
| 736 | [Van's Retexture : Steel](https://steamcommunity.com/sharedfiles/filedetails/?id=2955560866) | `sirvan.steelretexture` |
| 737 | [Vanilla Mechanoid Slag Chunks Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3228786091) | `auntydonna.mechanoidslagchunksretextured` |
| 738 | [VFE Pirates Custom Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3755883834) | `yourmother.vfepirates.retexture` |
| 739 | [Food Texture Variety - Babies](https://steamcommunity.com/sharedfiles/filedetails/?id=3366505975) | `goat.food.texture.variety.babies` |
| 740 | [Food Texture Variety - Beer](https://steamcommunity.com/sharedfiles/filedetails/?id=3361892196) | `goat.food.texture.variety.beer` |
| 741 | [Food Texture Variety - Chocolate](https://steamcommunity.com/sharedfiles/filedetails/?id=3362958345) | `goat.food.texture.variety.chocolate` |
| 742 | [Food Texture Variety - Nutrient Paste](https://steamcommunity.com/sharedfiles/filedetails/?id=3357299556) | `goat.food.texture.variety.paste` |
| 743 | [Food Texture Variety - Replacement](https://steamcommunity.com/sharedfiles/filedetails/?id=3366513037) | `goat.food.texture.variety.replacement` |
| 744 | [Food Texture Variety - Vanilla Expanded Cooking](https://steamcommunity.com/sharedfiles/filedetails/?id=3388883044) | `goat.food.texture.variety.vecooking` |
| 745 | [H.C_AnimalReskins_Complete2 (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600202032) | `hc.animalreskins.complete2` |
| 746 | [Pointed Elf Ears Refined](https://steamcommunity.com/sharedfiles/filedetails/?id=2886966525) | `hysila.refinedelfears` |
| 747 | [VFME - Caravan Packs!](https://steamcommunity.com/sharedfiles/filedetails/?id=3609057140) | `vfme.caravanpacks` |
| 748 | [Alpha Animals Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3536598972) | `ks.aaretextured` |
| 749 | [Charge Lance Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=3697662862) | `regen.chargelancerestyle` |
| 750 | [UI Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2978831421) | `katsudon.uiretexture` |
| 751 | [Consistent Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3586064345) | `ferny.consistenttextures` |
| 752 | [Pacing Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3613142246) | `ferny.pacingmanager` |
| 753 | [Architect Menu Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3697920753) | `mrk.architectmenuoptimizer` |
| 754 | [Facial Animation Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3790129900) | `astryl.facialanimationperformancepatch` |
| 755 | [Turret Performance Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3621644930) | `jaeger972.turretperformancetweaks` |
<!-- MODLIST:END -->
