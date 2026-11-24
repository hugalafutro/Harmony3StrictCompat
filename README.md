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
| Alpha Books | Ruined and burned books cannot be ticked in a reading policy, so colonists never read them for recreation once a policy lost them: Alpha Books' postfix on `Dialog_ManageReadingPolicies.PolicyGlobalFilter` hides every `ABooks_*` def (right for its single-use `Book_NotAutoReadable` books, wrong for these two plain `Book` defs), "Clear all" removes hidden defs and "Allow all" only restores shown ones, and vanilla `BookUtility.IsValidBook` only picks a book the reader's policy allows | Postfix on the same getter at `Priority.Last` (after Alpha Books' own) adds the defs in Alpha Books' `ruinedBooks` set back to the filter the dialog shows. Existing policies are not changed: tick the two books (and check the quality range) in each policy that should allow them |
| Alpha Books | Colonists never pick a ruined or burned book on their own while a novel is around: `BookUtility.TryGetRandomBookToRead` picks at random among the valid books that "provide an outcome" for the reader and falls back to the others only when there are none; a novel always provides one, the ruined books' doer (`ReadingOutcomeDoer_Text.DoesProvidesOutcome`) never does | Transpiler on `TryGetRandomBookToRead` swaps its one `Book.ProvidesOutcome` call for one that also counts a book in Alpha Books' `ruinedBooks` set, so those books share the random pick with novels and useful textbooks. Reading policy, quality range and forbidden checks still apply |
| Vanilla (seen with Alpha Books ruined libraries) | "Read now" drops the book within a few ticks: the float menu does not check forbidden but the read toil fails on a forbidden book (the library loot spawns forbidden), and `JoyUtility.JoyTickCheckEnd` ends any read during a Work schedule slot because "Read now" never sets `ignoreJoyTimeAssignment` | Postfix on `Notify_Starting` of `JobDriver_Reading` and Alpha Books' `JobDriver_ReadAlphaBook`: a player-ordered read unforbids its book, as vanilla's Equip and Wear orders do, and ignores the recreation schedule, as vanilla's meditate-in-bed and visit-sick jobs do |
| Altered Carbon 2 + Too Many Mods - Compats and Rebalances | Pawns wearing the Cuirassier belt threw a NullReferenceException in `JobGiver_Reload_TryGiveJob_Patch.Postfix` on every job search: Too Many Mods swaps the belt's VEF shield (`CompShieldBubble`) for the vanilla `CompShield`, and Altered Carbon's postfix still reads the VEF comp | While the belt def has no VEF shield, Altered Carbon's postfix is skipped. It only adds the "charge the belt at a station" job, which the self-recharging vanilla shield never needs |
| Auto-Cast Specialist Commands + Det's Xenotypes - Stoneborn | Personal tweak, not a fix: Auto-Cast only knows the command abilities by name, so the Stoneborn Stonecall (self-only +30% mining and construction speed) is never auto-cast | On a mine job or when finishing a construction frame, hands `DV_Stonecall` to Auto-Cast's own toil injector, gated by its "Auto-Cast Mining Command" setting; right-click on the Stonecall gizmo toggles that setting and Auto-Cast's check icon shows it, as on its own commands |
| Basic Traits | "TraitHediffApplier: Processing pawn" debug spam | Log calls removed, logic kept |
| Better Beggars | With the colony on two maps (home + gravship site) a beggar quest arrived asking for nothing ("ERR: Travelers" text), the beggars left at once and the quest counted as a success: its "want thing" quests pick a random player map with `QuestGen_Get.GetMap()` in both the can-fire test and `RunInt`, so the test passed on the stocked map and the quest was built on the bare one, where no item was found | `RunInt` first runs the node's own `TestRunInt` on each candidate map and is built on one that passes (`GetMap` returns it until `RunInt` ends); with no stocked map the quest does not fire |
| Better Workbench Management | Its bill options (output filter, count away, restrict workbench) overlap the Ideology "Use global style" dropdown at the bottom of the bill dialog | Lifts BWM's block 50 px whenever Ideology is active and the recipe makes a thing (vanilla classic-mode dropdown and Style Change Anytime's dropdown both sit there) |
| Better Workbench Management | Personal tweak, not a fix: the previous/next bill arrows overlap the top of the ingredient filter by a few pixels | Draws them 7 px higher |
| Biomes! Caverns | `BMT_ResourceBlueCrystal` "is equipment but has no verbs or tools" config error | XML: strips its equipment nodes (`1.7/Patches/BiomesCaverns_BlueCrystal.xml`) |
| Biomes! Caverns (Caveworld Flora) | Wild fungus colonies never regrow: `Mycelium.NotifyPlantRemoved` is never called, so a colony that once filled up stays "full" with no mushrooms | Each wild mushroom removed lowers its colony's count (floored at 0, colony kept); on load every count is reset to the live mushrooms |
| Biomes! Caverns (Caveworld Flora) | Wild fungi mouseover reads `Brightbells)`, growth missing | Re-inserts ` (NN% growth` in `FruitingBody.LabelMouseover` |
| Biosculpter Pod and Neural Supercharger Resize + Vanilla Textures Expanded | With the pod resized (e.g. 1x2), its blueprints, frames and architect button still show VTE's full 3x2 menu icon squeezed into the new shape: the resizer sets the icon before the game loads VTE's `uiIconPath`, which then overwrites it | Startup fix: when the resizer's graphic is in use, the pod's icon is its resized south texture, as the resizer intends |
| Busywork + LTO Colony Groups | With marker position "Upper" the job icon never shows on Colony Groups portraits: Busywork draws it only from a postfix on the vanilla colonist bar drawer, which LTO replaces | Runs Busywork's own portrait postfix from LTO's `DrawColonist` (icon in the portrait's top-left corner, as on the vanilla bar) |
| Catalog Tab (Colony Tabs Framework) + UI Rethemed - Colonist Card / Info Card | The "C" button (open this thing in the catalog) is drawn by the framework as its own box at the game's coordinates: in the inspect pane it sits between and under the buttons UI Rethemed moved into its header slots, and on the info card it lands on the themed title text, where the click never arrives because the title bar's drag grip uses the mouse-down first | While UI Rethemed draws, the framework's own draw (`InfoButtons.DrawInspect` / `DrawCard`) is skipped and each registered button is drawn as a UI Rethemed header button with its letter: in the card header's next slot, and left of the info card's title tools (Compare, materials, back). Clicks go through the framework's `InfoButtons.Press` |
| Character Development (seen with Vanilla Gravship Expanded) | The "live in a new settlement" want was fulfilled for the wrong pawns: its postfix on `SettleUtility.AddNewHome` completes it for every player pawn spawned on any map and in any caravan, wherever the new settlement is. A gravship landing on an empty tile calls `AddNewHome` while the crew is still held inside the gravship (on no map), so the crew got nothing and a pawn left on a kept map (Vanilla Gravship Expanded keeps the home tile) got the want | Its postfix is skipped; a postfix on `AddNewHome` completes the want only for player pawns at the new settlement's tile: on its map, in a caravan standing on it, or aboard a gravship landing on it |
| Common Sense | "Getting random element from empty collection" warnings, repeated on every stack split: "add meal ingredients" looks for a recipe for food that no recipe makes and never caches the miss | Skips that lookup for food no ingredient recipe produces |
| Common Sense | Eating job ends with an error (`CleanFilthToil` null-ref) when the filth a pawn cleans before eating vanishes first | Moves on to the next filth / the meal when the target is gone |
| Common Sense | "Clean before working" queues a sweep of the bed's room before every lie-down on a bed; it only skips pawns that need a tend right now, so a tended patient on medical bed rest (flu, plague, healing wounds) gets up to sweep each time it lies down again, and a doctor's tend fails while it is out of bed | Prefix on `OpportunisticTasks.Cleaning_Opportunity`: no sweep before `LayDown` while the pawn should seek medical rest; cleaning before ordinary sleep, bills and recreation unchanged |
| Designator Shapes + Better Architect Menu | Flood fill (e.g. a floor) runs through doors and fills the next room: Shapes only treats things in the architect category named `Structure` as barriers, and BAM moves `DoorBase` to `Ferny_Doors` | Shapes' private `FloodFill.IsStructure` also answers true for doors (`Building_Door` subclasses and anything in `Ferny_Doors`); only Shapes' flood fill calls it |
| Flourish - Everyday Animations | The telescope pose uses one fixed world offset and facing (`JobAnimationWorker_Anchored`), set up for the telescope's normal art, but vanilla draws that art mirrored when the telescope is rotated West, so there the pawn stands on the wrong side of the eyepiece | When the target is drawn mirrored (`Graphic.WestFlipped`/`EastFlipped` for its rotation), the worker reads the anchor with x negated and East/West facing swapped; the shared def is never written (render threads) |
| Flourish - Everyday Animations | Visual tweak: in the telescope pose the pawn's body covers the telescope (buildings always draw below pawns), so the hands hold nothing visible | While the anchored pose runs, the telescope is drawn a second time just above the pawn and below Flourish's hands (mesh only, no second shadow) |
| Giddy-Up 2 | Dismounted caravan animals park where the rider stepped off, so the caravan never arrives or leaves | Keeps lord animals' travel/follow focus across `Dismount` |
| Giddy-Up 2 | An animal rescuing a downed pawn can be picked as a mount: Giddy-Up stops its job at once, so it drops the patient and waits for a rider who may be far away | Adds vanilla `Rescue` to Giddy-Up's private busy-job set (`IsMountableUtility.BusyJobs`), so auto-mount skips the animal and the right-click menu shows it as busy until the rescue ends |
| Giddy-Up 2 | "<pawn> started 10 jobs in one tick. newJob=Mount": the Mount job fails on a forbidden animal (outside the rider's allowed area counts), but `IsMountable` never tests that, so auto-mount picks the same animal again at once | Postfix on `IsMountableUtility.IsMountable` refuses an animal that is forbidden for the rider |
| Goji's Misc Genes + Living Bonds | `NullReferenceException` every ~40 s from `LivingBonds_Manager.GameComponentTick`: Goji's Parroting gene answers every interaction through `recipient.interactions`, which is null while the recipient is not spawned, and Living Bonds' baby babble interacts with a carried baby | Prefix on Goji's `InteractionWorker_Interacted_Patch.Postfix`: no parroting when the recipient has no interactions tracker; the original interaction is unchanged |
| Vanilla Social Interactions Expanded + Goji's Misc Genes | "Exception ticking <pawn> ... `KeyNotFoundException`: The given key '<pawn>' was not present in the dictionary" from `InteractionWorker_Teaching.Interacted`: VSIE stores a teacher's topic only when it weighs the interaction, and a pupil with Goji's Parroting gene answers with the same interaction, so the pupil "teaches" back without one. The exception aborts the first teacher's tick; logged once per pawn, then silent | A teaching interaction without a stored topic picks one the way VSIE does; with nothing to teach it ends quietly |
| Hauler's Dream | A bulk-hauled load goes straight back to the storage it came from ("bulk-hauled 6 times ... net-zero", "WorkGiver_HaulGeneral ... yielded no actual job", food left in a lower-priority storage instead of the fridge). HD ignores a pawn's own "on its way" claim only while the pawn carries the load, but its test for a load in the inventory (`StorageCommitments.IsDelivering`) compares `Thing.ParentHolder` with the inventory's `ThingOwner` and is never true. A haul that claims all the free room of its destination (Kanban cap, nearly full feeder / fridge / hopper / log pile) then finds it full of its own claim at unload, and the search falls through to the next priority | Counts a load in the pawn's own inventory as delivering |
| Hauler's Dream + Compositable Loadouts | CL's 1.7 `Item` has no `Def` (items hold several alternative defs), so HD logs "loadout keep API ... did not fully resolve" and stops shielding loadout items from "adopt all surplus": possible unload / re-fetch loop | Turns HD's shield back on and counts every loadout item whose `Defs` include the def (HD's own startup warning still prints once) |
| Hauler's Dream + Smart Medicine - Continued | HD protects Stock Up items only on stacks it has not tagged as its own haul, and the tag spreads to every inventory stack of a def the pawn hauls. A tagged stocked stack is partly unloaded, Stock Up fetches it back, and the stack's growing tag age raises "Cannot unload inventory" whatever the hours setting | Postfix on HD's `InventorySurplus.KeepCountOf` raises the keep count to the pawn's Stock Up count, the path HD also honours on tagged stacks |
| Hauler's Dream + Kanban Stockpile Continue | HD ignores Kanban's max stack size: its bulk pick-up counts each slot at the full stack limit and its unload drops the whole carried stack, so vending machines capped at 25 held 248 cheese; Kanban's "remove excess" hauls the surplus out, HD hauls it back (churn, HD "net-zero" warnings) | While HD measures storage space, slots of capped storage count at the cap; HD's unload onto a capped cell drops only what the cell may still take, the rest goes to the next cell. HD's space count also follows Kanban's refill threshold and its similar stack limit per storage (counting it per cell made HD pick up cellar hay for two linked, already-full animal feeders and drop it straight back) |
| Hauler's Dream | "<pawn> started 10 jobs in one tick. newJob=DoBill" (short stutters, seen at smelters and a brewery): "share for crafting" offers stacks inside other pawns' inventories as bill ingredients, but only HD's batch job can take them; for a bill not marked as batch (or a pawn that may not batch it) the vanilla bill job targets an item that is not on the map, fails and is handed out again | Postfix on `InventoryShare.AddSharableStacksForBill` removes other pawns' inventory stacks from the candidates unless the bill is a batch bill this pawn can batch; a last-priority postfix on `WorkGiver_DoBill.JobOnThing` drops a vanilla bill job that still targets one |
| Hauler's Dream | "Cannot unload inventory" lists pawns that cannot get up: anesthetized or otherwise downed patients, and pawns in deathrest (days). HD's alert skips only drafted pawns | Prefix on `Alert_CannotUnloadInventory.Evaluate` reports no fault for a downed or deathresting pawn; once it is up, the alert's usual grace hour starts over |
| Unique Melee Weapons | "Could not resolve reference to object with loadID Ideo_N ... curParent=RimWorld.Pawn_IdeoTracker" on the first load after a warband quest ("The Bloodthirsty War Party") ends unvisited: `QuestPart_SpawnWarband` keeps its pre-generated raiders while the game removes their temporary faction and ideoligion. The warning stops after one save, but the raiders (~580 KB) stay in the save | Postfix on `Quest.CleanupQuestParts` discards the part's unspawned pawns when the quest ends; postfix on `Game.FinalizeInit` does the same once for quests that ended earlier |
| Alpha Armoury (tesla coil trait) + VE Framework | VEF's `TeslaProjectile` attaches a fire to every pawn its chain hits without vanilla's ignition checks, so mechanoids (flammability 0) burn and repeated hits stack several fires on one target; the trait is meant to stun mechs and burn organics | Transpiler on `TeslaProjectile.Impact`: the fire attaches only when vanilla `CanEverAttachFire` allows it and the target is not already burning, otherwise the spawned fire is removed |
| Vanilla (seen with Alpha Armoury's tesla coil trait) | The floating "Adapted" text is thrown for every stun damage instance on an adapted pawn. A tesla bolt is an EMP bullet plus an EMP explosion at each of up to four chain impacts, from a burst weapon, so adapted mechanoids got dozens of texts per burst: a white blob on each | The text shows at most once per second per cell; damage unchanged |
| Vanilla (seen with Alpha Genes plagued shamblers) | "Disease: Shambler plague" letter, but no disease in the health tab: a damage def's extra hediff (`additionalHediffs`) is added at severity 0 when the hit was fully blocked by armor or the victim has 100% of the scaling stat (here toxic resistance). The add sends the discoverable disease's letter, and the next health tick removes the 0-severity hediff | Transpiler on `Pawn_HealthTracker.PostApplyDamage`: the extra hediff is added only when its severity is above 0. Hits that deal damage are unchanged |
| IMPACT - Combat Overhaul | Its weapon equalization lifts every ranged weapon's damage per projectile to a floor per tech tier (spacer 20) and looks at neither burst size nor cycle time, so weapons balanced as many weak hits came out far above their design: Sparkling Worlds' Bulletstorm 3 -> 20 damage at ten shots a second (30 -> 200 damage per second), Apex Mechanoids' Beam Witherer 1 -> 20 on each of 15 burst hits (a fully armored colonist went to 1 torso hit point in one burst, 2026-11-23). Of 405 equalized weapons, 156 projectiles were lifted to the floor and 91 of those fire faster than one shot a second | Prefix and postfix on `WeaponEqualizer.Run`: the floor stands for damage per second of fire. A weapon firing more than 1 shot a second (burst / (warmup + cooldown + time between burst shots)) gets the floor divided by its rate, never below the damage its own mod gave it (Bulletstorm back to 3, minigun and charge blaster back to their own values). A weapon whose own mod gave it a third of the tier floor or less is not lifted at all, since damage is not what it is for: the non-lethal and utility guns IMPACT had turned into 12 to 26 damage weapons (VWE Non-Lethal dart gun 3, tranquilizer blowgun 3, pain gun 1, EMP and toxin dart guns, Eltex psycaster guns 6) and many-weak-hits designs (Beam Witherer back to 1 per hit). Slower weapons, the upper clamp, armor penetration, range, projectile speed and turret guns stay as IMPACT set them; a projectile shared by several weapons takes the fastest. A file `weaponsurvey.on` in the mod folder writes every weapon's own, IMPACT and final damage to `Logs/impact_weapons.tsv` |
| Kanban Stockpile Continue | "WorkGiver_RemoveExcessFromKanban provided target ... but yielded no actual job": its remove-excess `HasJobOnThing` ignores the destination's own max stack size, which `JobOnThing` clamps to, so a capped vending machine or fridge already at its cap gives no job | Postfix on `HasJobOnThing` also requires `JobOnThing` to build a job; Kanban's `OnExcessRemoved` event is held back during that check |
| AdaptiveMealBill + Hauler's Dream | HD batch cooking replaces the bill job, so AMB loses the meal kind it picked, and HD splits the batch by AMB's placeholder recipe (any raw food), so a rep can be mostly meat with a little veg; AMB then logs "Could not recover the meal recipe from the job or its ingredients." and cooks the placeholder's product | When AMB's own recovery finds nothing, picks the veg-only or meat-only recipe if every ingredient fits it, else the mixed one |
| Kanban Stockpile Continue | With a similar stack limit, the accept check counts only stacks the item can merge with, but "remove excess" counts stacks per def. Same-def meals with different ingredient kinds cannot merge, so a capped fridge accepts a third fine meal stack, remove-excess carries it out, the next haul brings it back: a pawn shuttles the same meals between two storages | The accept check also counts every stored stack of the same def and stuff, like the removal does. Topping up a mergeable stack still works |
| Kanban Stockpile Continue | A storage that holds more stacks than its similar stack limit never settles: remove-excess hauls the surplus stack out one carry load at a time, which leaves it partial, and the accept check lets any partial stack be topped up without asking the limit. A steel shelf set to 1 stack held 750 + 698 and took 71 deliveries in 12 minutes (free-space readings of 75, 73 and 52: one pawn carries a load out, the next brings it back) | Postfix on `KSUtil.CanStackIntoExistingAt`, which the accept check and the haul count clamp ask first: "no" while the storage already holds more stacks of that def and stuff than its limit, so the accept check falls through to its similar stack count and refuses. Remove-excess then drains the surplus and the storage settles. A thing already lying in that storage is exempt (moving or merging inside it adds no stack). How the extra stack gets in is not closed: it is carried out once instead of forever. Logs `Kanban: <storage> holds more than its N allowed stack(s) of <def>` once |
| Kanban Stockpile Continue | With two or more stacks allowed and a stack size below the item's own stack limit, only the first stack fills: a steel shelf (stack limit 750) set to 2 stacks of 500 stopped at 500 + 240. The top-up wish of a cell is the smallest wish of every stack below the item's stack limit; the stack at 500 is below 750, wishes 0 and wins, so the cell wants nothing and the second stack keeps only its first delivery | Postfix on `KSUtil.TryGetStackRefillThresholdDesired`: when the wish came out as 0, it is taken again over the stacks still below their Kanban stack size. A stack at its size cannot absorb anyway (Kanban's own `TryAbsorbStackNumToTake` prefix), so the delivery cannot overfill it. Refill threshold unchanged. Logs `Kanban: <storage> may take N more <def> into a partial stack beside one already at its stack size` once |
| Kanban Stockpile Continue | Newly built storage starts with Kanban numbers nobody chose (e.g. 1 stack / 500): every storage settings object without an owner shares the one key `___clipboard`: the copy/paste clipboard, but also every def's default storage settings, a frame's settings and a flying gravship's stockpiles and storage groups. A new blueprint copies from its def's defaults, so it reads whatever a Copy or a finished frame left in that slot; the slot is written into every ownerless settings node of the save, so it survives a reload, and sarcophagi, biosculpter pods and mortars pick the numbers up too | Transpilers on Kanban's `StorageSettings_CopyFrom_Patch.CopyFrom` and `StorageSettings_ExposeData_Patch.Postfix` replace the `___clipboard` fallback with a key per ownerless object. Def defaults carry no Kanban numbers; the clipboard, a frame, an auto-rebuild blueprint and each gravship stockpile carry only what was copied into them. Numbers already leaked into existing buildings stay until changed by hand |
| Vanilla Ideology + Alpha Memes (heart extraction) | A ritual sacrifice of an innocent prisoner counts as "executed innocent prisoner", so with "Execution: horrible if innocent" every member hates the moral guide for holding the ideoligion's own ritual (-30). Alpha Memes' heart extraction also kills with Cut instead of ExecutionCut damage, so the death counts again as "responsible for the death of a prisoner" (-15) | While the killer is in a ritual, an innocent prisoner's death records no execution events and the death thoughts see execution damage. Guilty prisoners and executions outside rituals are unchanged |
| Hospitality | Guests with a conceited royal title buy food the title forbids (a Duke bought legendary cheese and simple meals), refuse to eat it and get the "has no food" thought; the same food counts as "carrying enough", so they stop shopping for food | Postfix on `JoyGiver_BuyStuff.CanEat`: food that vanilla `FoodUtility.InappropriateForTitle` rejects for the guest is not bought as food and not counted as carried food |
| Hospitality | Arrival re-gear destroys visitors' warcaskets | Skips the re-gear for casket wearers and re-locks their casket pieces |
| Hospitality | Arriving guests pocket their headgear, warcasket helmet included | Skips headgear pocketing for casket wearers |
| Hospitality | Guests with a claimed bed sleep on the floor after a save is loaded in the same session: `CompUtility.CompGuest` caches comps in a `Dictionary<Pawn, CompGuest>` that is never cleared, and 1.7's `Pawn.Equals` compares IDs, so reloaded pawns get the previous game's comp (stale bed and owner) | Clears that cache after `MemoryUtility.ClearAllMapsAndWorld` (every load / return to menu), logging how many entries it dropped |
| Hospitality | "threw exception in WorkGiver CookFillHopper" null-ref for guests helping out: Hospitality answers `InAllowedArea` from the guest area while the guest's own allowed areas stay empty, so vanilla's fail reason reads `.Label` on a null area for food outside the guest area | Transpiler on `HaulAIUtility.PawnCanAutomaticallyHaul` makes that label read null-safe |
| Hospitality: Casino | Casino only charges from its own drivers (poker, VFE roulette, slots), so tables that LocalTweaks gives its vending comp (tongo, billiards) would show the pricing gizmo but never earn | Charges once per job, on the first joy tick at any building with Casino's comp whose driver is not Casino's own (Casino's `InsertCoin`: other factions pay, colonists play free) |
| Hospitality: Casino | The pricing setting (free / low / default / high stake) only shows as the pricing gizmo's label, not in the inspect pane | Inspect pane gets a "Pricing:" line with the setting and the silver charged per game |
| Hospitality: Casino | Its slot-machine job giver sits above lord duties for every friendly visitor: the bestower left the bestowing ceremony to gamble, guards were pulled off shuttle boarding (shuttle left pawns behind or stayed with pawns inside), leaving trade caravans ran back across the map | Only casino visitors and trade caravans waiting at their chill spot get the slot-machine job |
| Hospitality: Casino, Vending Machines | `VendingMachineJobHelper.GetActiveVendingMachines` (same code in both mods) filters every colony building through `IsVendingMachine` on every "withdraw earnings" work turn (reach probe: about 1 ms per turn each) | Prefix: only the player's things of defs that carry the mod's vending comp go through the mod's own `IsVendingMachine`. Same machines; the list is ordered by def instead of building order, which only decides between two machines at exactly the same distance. Logs `Vending machine listing: N turns looked at M machines instead of every colony building` once |
| Hospitality: Vending machines | Personal tweak, not a fix: the powered vending machine should work as a fridge | Draws 100 W instead of 10 (`1.7/Patches/VendingMachine_Fridge.xml`); while powered, items in it get fridge temperature (ambient - 100, floored at -10 C, same as [sbz] Fridge), so food freezes |
| Hunt For Me | Released (wild) animals keep their hunting training and still run HFM's hunt job giver, which in supervised mode picks prey from the player's Hunt designations without excluding the hunter: a released hydra marked for hunting bit itself until downed | HFM's hunt job giver only runs for player-faction animals (vanilla never puts a Hunt designation on those) |
| [FSF] Advanced Bionics Expansion + Block Unwanted Minutiae | The EMP shield implant patch adds a "remove brain shock" comp with no `<message>` to vanilla BrainShock; vanilla still posts that null message on every EMP hit and BUM's message filter throws `ArgumentNullException (key)` each tick until the hediff is gone | Prefix on `HediffComp_MessageBase.Message` skips posting when the comp has no message text (any mod, any hediff) |
| Level Up! | Personal tweak, not a fix: at conception Better Gene Inheritance generates a throwaway player-faction pawn, adds the rolled hybrid genes to it and destroys it, and Level Up announces its skills ("Baby reached level 9 in Animals!") alongside the pregnancy letter, spoiling the surprise | Prefix on both Level Up hooks (`Patcher.DirtyAptitudesPostfix`, `Patcher.LearnPostfix`) skips pawns that are neither on a map (carried or held counts) nor in a caravan |
| Loading Progress | Thread race in its startup profiler (`ModInfoList.GetModInfoFor`): a mod's defs can fail to load ("same key has already been added"); on 2026-11-03 it dropped Anomaly, the game crashed and reset ModsConfig.xml | Serializes the call with a lock |
| Loadout Compositing + Better Workbench Management | BWM 1.7 Waylith build of 2026-11-03 is a Debug build; LC's transpiler no longer matches ("Error in transpiler ... off map"), so per-tag bills lose the "count pawns away / on caravan" option | Swaps BWM's `repeatMode == TargetCount` test for LC's own `ShouldCountAway` |
| Mod Settings Framework (VEF) | Slider settings only change the first def their xpath matches: ReBuild: Doors and Corners' grate speed slider fixed the metal grates but left the sterile grate tile at pathCost 10 (57% walk speed) | After the slider writes the first match, copies the value to every other match |
| MSS Tweaks and Fun | Haunt portraits saved upside down on 1.7/Vulkan | Flips the rows before the PNG is encoded |
| MSS Tweaks and Fun (Pondering Orb) + Ideology transhumanist meme | The orb's hologram is a colony pawn with the colony's ideoligion: it wants a neural supercharge and a body modification, but MSS blocks hediffs on holograms and it cannot be operated on. It drained every supercharger again and again for nothing and kept both mood penalties | A hologram never qualifies for supercharger auto-use (`CompNeuralSupercharger.CanAutoUse`) and never gets the "needs neural supercharge" or "no prosthetic" thoughts; other believers get no opinion penalty for its missing prosthetic either |
| MSS Tweaks and Fun | `WorkGiver_HaulToAICore.ShouldSkip` asks every colony building for its AI core comp on every work turn, also on a map without an AI core (reach probe: about 1 ms per turn) | Prefix: when no thing of a def that carries the comp is on the map the answer is "skip"; otherwise the original runs. Logs `AI core haul check: N of M turns answered without walking the colony's buildings` once |
| MSS Tweaks and Fun | VFE Pirates Buckoneer culture patch targets `VFEP_Junkers/VFEP_Mercenaries` | Exact xpath remap to the `VFEP_Mercenaries` faction |
| Positive Connections | Selecting any building in one room throws `NullReferenceException` in `Gizmo_RoomStats.GizmoOnGUI` and the gizmo bar (UI Rethemed Main Bar) stops drawing: `RoomRoleWorker_Den.CountDoors` walks region links without null checks, a one-sided link throws inside `Room.UpdateRoomStatsAndRole`, and the room's role stays null | Prefix replaces `CountDoors` with the same count that skips null regions |
| Prisoner Realism + Cleaning Area Relaunched | Prisoners stand still in an endless Clean job loop when their room has filth inside Home but outside the Clean area: Prisoner Realism picks it, the Cleaning Area Relaunched driver skips it, the job ends at once and is handed out again | Prisoner cleaning only picks filth inside the Clean area |
| Pulse Weaponry (any weapon def with the same mistake) | `DV_PulseRifle` adds `CompProperties_EquippableAbilityReloadable` without `<comps Inherit="False">`, so it also keeps BaseWeapon's plain `CompEquippable`. Both comps save a verbTracker with the same load IDs: "Exception registering ... in loaded object directory" for every such weapon on load | At startup, drops the plain `CompEquippable` from every ThingDef that also has a `CompEquippable` subclass (logs the defs); the extra saved verbTracker is ignored on load |
| Quiet Wonders | Sleep dim (`QWLife.FindAsleepRooms`, every 60 ticks) reads the room type of every humanlike pawn's room, awake pawns too; a room changed since its last read recomputes all its stats. After a fight the cleanup keeps rooms changing: 4% of every frame in the sampling profile | Prefix with the same result: only a pawn asleep in bed gets the room checks, an awake pawn just marks its room as awake (that set is only subtracted from the sleeping bedrooms). Logs `Quiet Wonders sleep dim: N scans checked the room type for X sleeping pawns instead of all Y humanlike pawns` once |
| ReGrowth plant patches | Duplicate wild plant records abort the world-gen Landmarks / Mutators steps | Dedupes `BiomeDef.wildPlants` |
| 1trickPwnyta's Ideology Patch | Big raids freeze the game while the raiders are generated (a 207-shambler swarm: 30 s, 27 s of it in this mod): its "Look change desired" fix (postfix on `PawnStyleItemChooser.WantsToUseStyle`) rebuilds and intersects two lists over every hair, beard or tattoo def on each call, and vanilla asks once per candidate hairstyle for every new pawn | Prefix replaces `LookChangeDesiredUtility.CanUseStyle` with the same rule without lists: a style passes when the pawn's genes and ideoligion both accept it, otherwise only when no style of that kind satisfies both |
| IMPACT - Combat Overhaul | Natural armor of animals, insects and anomaly entities can never be breached: armor is deterministic (penetration / armor <= 0.5 always deflects) and the focal breach that wears armor down is only read and added for mechanoids, so a weak attacker deflects forever (megaspider, nociosphere; seen with a hunting animal on a downed insect) | Transpiler on `ArmorDeflection.Prefix` makes the breach gates true for every creature; the metal-armor effect flags keep the mechanoid check |
| ReplaceLib + Fortifications - Industrial | The lathe's "make component" bill vanished on load (three times): ReplaceLib removes "duplicate" recipes after every game load, judging by label, product and `recipeUsers`, which is null for recipes a bench lists in its own `<recipes>`, so Fortifications' lathe recipe counted as a duplicate of vanilla's fabrication bench recipe and left the DefDatabase. A second load in the same session then hit `Could not load reference to Verse.RecipeDef named FT_Make_ComponentIndustrial` and dropped the bill | ReplaceLib's `RecipeUsersAreSame` compares the benches that actually offer each recipe (`AllRecipeUsers`) when `recipeUsers` is null, so recipes on different benches are no longer duplicates |
| Smarter Visitors + Event Image Popup | "Visitors are about to leave / afraid to do so: temperature" shows no picture: it is a `Dialog_MessageBox` from a lord trigger, and Event Image Popup only decorates node-tree dialogs inside a letter or incident | Prefix on `WindowStack.Add` rebuilds that one message box as a node tree (same text, same three buttons) with a `VisitorGroup` incident frame in scope, so it gets the visitor-group picture |
| Hospitality + Smarter Visitors + Stylish Dialogs | The "afraid to come" (Hospitality) and "afraid to leave" (Smarter Visitors) visitor questions always pop up and pause the game: they are modal windows, so Stylish Dialogs' per-category "open on arrival" settings never see them | When Stylish Dialogs' Notice letters are set not to open by themselves, each question is sent as a Notice letter (`H3SC_VisitorQuestion`, one-day timeout) with the same buttons; arriving guests wait off the map, leaving guests are held until it is answered; dismissed or expired = they do not come / they leave |
| Vanilla colonist bar + LTO Colony Groups | Personal tweak, not a fix: as soon as the bar has a second group (e.g. a caravan leaves), a translucent grey frame is drawn behind every group | Prefix skips `DrawGroupFrame` in vanilla's and LTO's colonist-bar drawers; only the frame drawing goes, portraits and frame clicks unchanged |
| Vanilla friendly raids | Friendly raid reinforcements arrive in a parka at best and freeze in extreme cold (151 pawns at -76 C); Dress for the Weather only covers neutral groups and its random search would stall a group this size | Postfix on `IncidentWorker_Raid.TryGenerateRaidInfo`, friendly factions only: each pawn gets the warmest non-armor apparel its faction can make wherever that replaces something colder, until comfortable or six pieces; logs how many were dressed |
| Readable Numbers + Better Scroll Bars | Both wrap the game's GUI backend, but the 1.7 ImGui host swaps its own backend in for every GUI pass and restores it afterwards, so neither wrapper is ever used: stock grey scroll bars, unformatted numbers | After the host's swap, the same wrappers go around the host's backend (game, then Readable Numbers, then Better Scroll Bars) |
| Vanilla gatherings | A party host whose schedule turns to work or sleep leaves and vanilla ends the party: "The party has been called off.", with no reason | The host of any party stays until it ends even when its schedule turns to work or sleep (it still leaves when asleep, starving, bleeding ...), and every gathering's called-off message names the reason ("...: the host X is downed.") |
| Rimsenal Faction Pack - Spacer | Second style comp on the mech commander helmet, Dresser, EndTable and FloodLight (duplicate load IDs) | Those conditionals skipped, for that mod only |
| Royalty monument quests + VE Framework pipes | Personal tweak, not a fix: the monument marker lets only power conduits share its footprint or sit next to it ("Cannot place monument over hidden helixien pipe") and destroys a finished monument after a day if anything else stays on its floor; VE pipes (helixien, astrofuel, uranium, ...) are the same kind of conduit-layer building | Transpiler on `PlaceWorker_MonumentMarker.AllowsPlacing` and `MonumentMarker.FirstDisallowedBuilding`: every `isPowerConduit` check also accepts a non-edifice `PipeSystem.Building_Pipe`, visible or hidden |
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
| UI Rethemed - Info Card + Alpha Prefabs | A prefab box's info card shows no floor-plan picture: Alpha Prefabs draws it from a prefix on `Dialog_InfoCard.FillCard` at a fixed spot on the game's card, which the themed card never draws | XML: a `CardArtDef` (`1.7/Defs/UIRInfoCard_AlphaPrefabsArt.xml`) points UI Rethemed's card-art hook at `AlphaPrefabs.Thing_Prefab.prefab.detailedImage`; the picture shows in the details while the description is selected |
| UI Rethemed - Colonist Card + TD Enhancement Pack | Personal tweak, not a fix: TD's skill gain arrows only draw on the vanilla skill rows in the Bio tab | Postfix on `Glance.DrawSkill` runs TD's own `LabelLearning` at the right end of each card skill bar (beside the level number, or at the bar end with UIR's "level on the left"), so its up/down arrow settings and fade still apply |
| UI Rethemed - Colonist Card + TD Enhancement Pack | The "jump to selection" eye button in the inspect pane header shows its "Jump to this object" tooltip over a different header button: UI Rethemed moves the icon into a header slot, but TD's `TooltipHandler.TipRegion` stays at the game's coordinates | Transpiler on TD's `JumpToSelection.Postfix` sends that tooltip region to the slot the icon got (the last slot handed out) while the card header is drawing |
| UI Rethemed - Core (hover cards, moved there from Inspector Tabs on 2026-11-19) | Hover card rows with a value have a fixed height, so a long label (Vanilla Social Interactions Expanded's opinion thoughts are full sentences) overlaps its own value and the row below | A row that does not fit on one line wraps its label beside the value and grows, with UIR's row hover glow over the taller row; other rows keep UIR's drawing |
| UI Rethemed - Inspector Tabs | Gear tab vacuum resistance row flickers every rebuild between "Vacuum resist... 9..." and the full label and value, and its bar restarts its fill animation: the gauge measures its text widths in whatever font the last drawer left (its other gauges force Small first) but draws in Small | Forces Small around `GearTab.RefreshVacuumGauge`, restoring the previous font after; personal tweak on top: the row is relabelled "Vacuum res." so the bar keeps some width |
| UI Rethemed - Inspector Tabs + Full Armor - Hands and Feet | Gloves and boots never appear in the doll's Hands/Feet cells and hover as "Headgear": Full Armor moves every gloves/boots def to the Overhead layer (its `moveGlovesHeadgearLayer`), and UIR classifies by layer before body part, so they land in Extra | `GearTab.VanillaSlotFor`: an Overhead/EyeCover item covering hands or feet and no head, torso or leg group goes to the Hands/Feet cell |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the Gear tab overview orders its cards loadout, mass, stock-up, comfy temperature, combat | Transpiler on `GearTab.DrawStatsPanel` moves the combat card in front of the temperature card and the stock-up cards to the end: mass, combat, temperature, stock-up |
| UI Rethemed - Inspector Tabs (+ Packs Are Not Belts) | Personal tweak, not a fix: empty doll slot captions "Mechanitor", "Exoskeleton" and "Ammunition" (Tiny font) are too wide for the cell and wrap onto a second line | `GearTab.SlotLabel` returns "Mech.", "Exo." and "Ammo" for those layers |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the gear hover card subtitle ("Masterwork · Hardleather") is drawn whole in the quality colour, so the material looks quality-coloured too | Postfix on `GearTab.BuildThingCard` tags the quality word with the accent and the rest (material, stack count) with the default subtitle colour |
| UI Rethemed - Inspector Tabs | Personal tweak, not a fix: the Compare gear window marks the equipped side with the theme blue and the compared side with the theme yellow (bars, frames, subtitle, values). That blue is exactly the Excellent quality colour and the yellow is close to Legendary, so "Equipped · Legendary" looks like an Excellent item | Transpiler on `GearCompareWindow.Contents` and `.Row` swaps the side colours for teal (equipped) and pink (compared), hues no quality uses |
| VFE Pirates (warcasket weapons, any VEF `ThingWithFloorGraphic`) | A warcasket weapon lying on the map or in storage shows its boxed floor graphic (drawSize 1) as icon, but the icon scale comes from the weapon's own drawSize (1.7): the box icon is 1.7x too big in the info card, inspect pane and storage tab | Postfix on `Widgets.GetIconFor(Thing, ...)`: while the floor graphic is the icon, the scale comes from the floor graphic's drawSize |
| VFE Pirates + Shelf Stack Logistics | Warcasket welding ignores the logistics pickup point: welders walk to the depot itself, often across the map, while ordinary bills get their ingredients moved to the pickup point next to the bench. Shelf hooks the vanilla bill job's "next ingredient" toil (`JobDriver_DoBill.JumpIfTargetInsideBillGiver`), and the welding job's own `CollectIngredientsToils` has no such toil | Postfix on `JobDriver_DoWelding.CollectIngredientsToils` inserts a toil after the ingredient is taken from the queue and lets Shelf's own hook wrap it, so welding ingredients on depot tiles are moved to the nearest pickup point like any bill |
| Stylish Dialogs | Personal tweak, not a fix: letters and event dialogs are one window holding the storyteller portrait and the text panel, and the game centres the whole window, so the panel sits about 195 px right of the screen centre | Postfix on `Window.SetInitialSizeAndPosition` for Stylish node-tree dialogs with a portrait moves the window left by half the portrait strip, so the panel is centred and the portrait hangs off its left |
| UI Rethemed - Main Bar (+ Heat Map) | Personal tweak, not a fix: the temperature readout only shows a hover card, and its face swallows every click, so it can no longer toggle the temperature overlay the way Heat Map's outdoor thermometer did | Left- or right-click on the temperature readout (text or tile) toggles the vanilla temperature overlay (`showTemperatureOverlay`, the flag Heat Map recolours and its key binding flips), with the checkbox on/off sounds |
| UI Rethemed - Main Bar + TD Enhancement Pack | TD's "Shift-click the time controls to stop the forced slowdown" only patches the vanilla time buttons, which UIR hides, so the forced 1x speed during danger could not be cut short | With TD's setting on: Shift-click on the speed tile ends the forced slowdown while one runs (instead of opening the flyout, with the clock sound of the current speed); Shift-click on a speed in the flyout ends it and picks that speed. Combat can signal a new slowdown, as with TD on vanilla controls |
| UI Rethemed - Inspector Tabs + Auto Extract Genes | AEG draws its "Auto-Extract Genes" checkbox only in the vanilla Health tab, which UIR replaces, so the setting had no switch in the themed tab | Adds it as a switch in the Health tab's Care section (UIR `CareSwitchDef`, DNA icon, AEG's own label and tooltip), shown for the same pawns AEG covers (colonists, slaves, prisoners, colony ghouls), not for animals |
| Colony Manager Redux + MSS Tweaks and Fun | CMR re-finishes its starting research (managing spot/desk) in every map's `FinalizeInit` on every load, without an `IsFinished` check. Each call sends vanilla's "ResearchCompleted" signal, which MSS counts toward "Reformation points for tech levels", so reloading a save gave a free ideology point | CMR's `FinishProject` call skips projects that are already finished |
| Flourish - Combat Animations + Muzzle Flash | Both mods drew a muzzle flash on every shot, so guns covered by Muzzle Flash (vanilla and its add-on packs) flashed twice | Flourish skips its own bloom and flash for guns that carry Muzzle Flash's `MuzzleFlashProps`; every other gun keeps Flourish's flash |
| Leo - Events + Biomes! Caverns (personal tweak) | The "unexpected mineral seams" cave always used the vanilla Underground biome | Each new cave gets a random Caverns biome (Fungal Forest, Crystalline Caverns or Earthen Depths) with its terrain, plants and wildlife, plus Caverns' stalagmite and crystal gen steps. Lava cells get their rock roof back (vanilla terrain gen strips it, leaving the cave "Unroofed"). Existing caves keep their biome |
| Vanilla Expanded Framework (PipeSystem, 1.7 build) | Selecting a minified pipe consumer (e.g. a gas wall lamp from Vanilla Helixien Gas Expanded) throws a `NullReferenceException` every frame: `CompResourceTrader.CompInspectStringExtra` reads `PipeNet.def`, and `PipeNet` is null off the map | Prefix: an unspawned building reports no pipe line |
| Vanilla Expanded pipe networks (hemogen, chemfuel, ...) + Hauler's Dream | Taps filled far past their set max (a tap is a one-cell shelf, and Hauler's Dream "haul to stack" sends items to its partial stack instead of the drain); a drain whose tanks are full kept collecting hauled items | A tap accepts only its own output already on its cell, up to its max (a bigger stack is hauled off to the drain). Works for linked taps too, including vanilla's merge-stacks job (it moved one linked tap's output onto the other). A drain takes hauled items once its network has room for the stack or at least 50 items, and pawns carry only what the tanks can still absorb; while the tanks are full, what sits on the drain is hauled off to ordinary storage (even lower priority), which feeds the drain again once room frees up |
| VFE Farming | The scarecrow threw "Exception ticking VFE_Scarecrow" (NullReferenceException in CellFinderLoose.GetFleeDestAnimal) when a wild animal held inside a building or container was in its radius, and stopped scaring the rest | The scarecrow only considers animals spawned on the map |
| VFE Factory | A splitter with exactly two outputs could draw as a red X ("Could not load Texture2D at ...ConveyorSplitter_NorthSouth_south") or face the wrong way: its picture was named after the output it was feeding when the picture was built, but the textures are named after the way it faces. Seen on an end-of-line splitter (facing east, outputs north and south) on a backed-up line | Two-output splitters pick their picture by the way they face; mergers unchanged |
| VFE Factory | An end-of-line splitter (belt in, outputs only to the sides) stops the whole line when the cell it faces holds a building, e.g. a lamp: before each item it checks that faced cell as a ground-dump spot (`TryDumpItem`), gets `CannotDump` for any non-refuelable building, and waits forever without ever picking a side output. With empty floor there it works only because the dump check passes and then picks a side; nothing is dumped there | A splitter with no output picked yet picks its next side output and starts moving instead of checking the faced cell; when it cannot pick one, the original runs |
| VFE Factory | A hopper with "allow inserting" off refuses its cell only when a pawn asks (`StoreUtility.IsGoodStoreCell`, `carrier != null`). The game's haulables list asks without a pawn, so the hopper still counts as a better storage: every lower-priority stack its filter accepts stays on the list and no pawn ever gets a job on it. Two such hoppers kept 982 of 1,007 listed things jobless; each general haul search ran the store search on all of them, 20-37 ms in one tick | `StoreUtility.IsInValidBestStorage` marks its own search; a postfix on `IsGoodStoreCell` then refuses a no-insert hopper's cell for that search too. Pawn searches and other pawn-less callers are untouched. Logs `Factory hoppers with inserting off: N haul-list checks no longer count one as a better storage` once |
| VFE Factory + Kanban Stockpile | Conveyors ignore Kanban's max stack size: a belt tops the stack in the storage it ends at up to the full stack limit itself and drops new stacks whole, and it empties the capped hopper it starts from, so haulers carried a whole stockpile through a hopper capped at 300 (2,469 gold in the forge hopper at the far end) | In `CanTransferToStorage`, `GetDumpCapability` and `CompleteTransfer` a stack lying in storage counts as full at the storage's Kanban max stack size, and a new stack is placed only up to it; the rest waits on the belt. Every belt piece on the line that ends at that storage (through turns, underground links and splitters) merges what it carries only up to the same size (`TryAdd`, `TransferItemTo` and their checks), so the surplus no longer piles up on the belt. Set the max stack on the hopper the belt ends at. Storage without a max stack is untouched |
| VFE Factory + EPrime's Readouts | The readout left out everything riding a conveyor (306 gold missing next to Colony Manager's count): it makes its own scan of spawned items and haul-source containers instead of showing the vanilla resource counter, and a stack on a belt is neither | After each scan (`GameCounts.AccumulateMap`) belt contents are added as stored, unforbidden stock (fresh, belt not fogged). Stacks inside an underground tunnel stay uncounted, as in the vanilla counter. Hopper contents with "Allow taking" off are forbidden for the player: untick the readout's "Ignore forbidden items" to see them |
| Vanilla wild animal spawner on pocket maps | A cave with a Caverns biome (which has wildlife, unlike vanilla Underground) threw "Index was out of range" in WildAnimalSpawner.CommonalityOfAnimalNow: it reads the world tile (pollution, coast, mutators), which pocket maps lack | On pocket maps animals are weighted by the biome alone |
| Smart Deconstruct + Odyssey vein mining | When a mine job would collapse a thick roof, Smart Deconstruct cancels it and removes the Mine designation, but ore marked with the vein designation (MineVein) kept it, so pawns looped: "started 10 jobs in one tick" and a "cannot mine" message every tick | Its designation lookup falls back to MineVein, so the order is removed like a normal one |
| Nice Research Tab (vanilla alerts) | Clicking "Need research project" (or the anomaly one) cast the research window to the vanilla type and threw InvalidCastException; UI Rethemed - Notifications then drew alerts over the gizmos until restart | With a replaced research window the alert just opens the research tab |
| Colony Groups | A task force (e.g. for a second map) and a colony group could share a load ID ("TacticalGroups.ColonyGroup1"), so loading logged "An item with the same key has already been added" | Task forces get their own load ID; nothing references these groups, so saves are unaffected |
| Vanilla rituals (seen with VFE Tribals) | A finished ritual obligation stored twice in completedObligations logged "An item with the same key has already been added. Key: RitualObligation_30" on every load | Each finished obligation is saved once (the existing duplicate is gone after the next save) |
| UI Rethemed - Notifications | Visual tweak: the game's transient top-left messages stay plain text outside UIR's notification column (its own `MessagesDoGUI` patch only shifts them sideways) | While a game is loaded and the column is bottom-right, middle-right (UIR's default since 2026-11-13) or top-right, draws each live message as a UIR `StatusRow` (the alert-row widget, UIR's own kind colours/icons) next to the alerts, newest nearest them (bottom-right: stacked above; middle-right and top-right: hanging below); left-click jumps / opens the quest, right-click dismisses with UIR's card click sound, hover highlights and shows UIR's own hover card beside the column at once, as its alerts do (header naming the message type, rule, full text) in the row's colour; messages live 30 s instead of 13 and a hovered one restarts its timer; with UIR's section headings on, the rows (sharing UIR's column width: their own wanted width, worked out as UIR's sections do, widens the whole column through a `HudColumn.Shared` postfix) sit under their own folding "Messages" heading (UIR's `SectionHeading`, list icon, count, red/yellow when a bad/warning message is live; folded state saved with UIR's other sections; UIR's heading width counts the "Messages" name so it is not cut off; no "new" count while folded, which UIR's own sections now show) |
| UI Rethemed - Notifications + Satellite Minimap | Personal tweak, not a fix: with the column set to top-right it starts 12 px from the top, under the minimap window, which covers it | While the minimap window is open, the top-right column starts 10 px below its bottom edge (assumes the minimap sits in the top-right corner) |
| UI Rethemed - Notifications + Alpha Biomes | Personal tweak, not a fix: on the world map, selecting a tile with features (Odyssey mutators) opens Alpha Biomes' "Features in this tile:" panel in the top-right corner, over the top of the top-right column | While that panel is open, the top-right column starts 10 px below it (same postfix on `Placement.TopStart` as the minimap row) |
| Satellite Minimap | Personal tweak, not a fix: colony mechanoids are filed under "Colonists" in the layers menu ("Colonists (95)"), and every marker class is capped at 50 dots, so with many mechs some colonists and mechs get no dot at all | A "Mechanoids (n)" row under Colonists in the layers menu: unchecked, colony mechs are neither drawn nor counted (they still need Colonists checked to show); the Colonists count leaves mechs out; saved in the minimap's settings file. The per-class cap is raised from 50 to 250. The "Predators" row also held the colony's own predator animals (hunting animals drawn as big outlined threat dots): those now count as colony animals, so Predators shows only wild and other factions' predators. Colony animals shared one switch with wildlife: they get their own "Colony animals (n)" row under Colonists, independent of every other row, and "Wildlife & animals" becomes "Wildlife (n)" |
| UI Rethemed - Notifications (days of food) | The "Days of food" row re-counted every second and rebuilt its hover card, so the tooltip restarted every second and could only be read while paused | The row re-counts every 5 seconds, and not while it is hovered, so the tooltip holds still |
| UI Rethemed - Colonist Card (item info rows) | The meal "Ingredients" row ran off its left edge, cut mid-word, and its tooltip never showed: the rows are drawn outside the inspect window's own pass, so Mouse.IsOver (hover highlight, TipRegion) always said blocked | The row drops " (* only stacks with similar meals)" so more fits; hover uses the plain mouse-in-rect test, and the row's tooltip (the whole line, note included) is entered straight into the game's tooltips (UIR shows it only for rows it had to cut short) |
| UI Rethemed - Architect (+ Vanilla Plants Expanded) | Personal tweak, not a fix: Tilled soil is the only buildable in the Orders category, so UIR gives it a one-card "Orders" group and moves Cancel, Mine, Haul and the other orders to its side column (it does that for any category with something to build) | Postfix on `ArchitectData.ShownApart`: the Orders category's orders stay in the grid, in one group with Tilled soil |
| UI Rethemed - Architect | Personal tweak, not a fix: long names are cut to one line with "..." on the grid cards ("Underground...") and in the Details panel header | Cards get room for two label lines (the second still ends in "..." when the name is longer). A single word wider than the card is cut with "..." instead of wrapping mid-word ("Matterweave" / "r"). The Details header grows by one line when the name does not fit and draws it wrapped; category row and chips move down with it |
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
| Vanilla + Better Workbench Management | Personal tweak, not a fix: every time a pawn looks for work at a bench, each "do until you have X" bill counts its products (every item of that def on the map for armor and weapons; BWM adds every colonist's gear, caravans and its extra product filter). Circinus, 29 colonists: 0.45 counts a tick, 87 us each in BWM's postfix alone, 2.7% of frame with Hauler's Dream's postfix and Compositable Loadouts' `ShouldDoNow` prefix | Transpiler on `Bill_Production.ShouldDoNow`: the count goes through a per-bill cache, one real count (vanilla plus every mod's patches) reused for 600 ticks. Finishing an iteration of any bill drops the entries for that product, so a freshly made item is seen at once. Late by up to 600 ticks: a bill restarting after its product was destroyed, sold or taken off the map, and changes to the bill's count options; a bill counting through BWM's extra product filter can make one item too many. The bill row ("3/5") stays live. Loadout Compositing's per-tag bills never reach that call (its `ShouldDoNow` prefix counts on its own through `BillUtility.Satisfied`: 1.8 s of a 91 s tick in a sampling profile), so the same call swap in `Satisfied` sends them through the same cache. Logs `Bill count cache: N hits, M real counts, ... K lookups from Loadout Compositing per-tag bills` once. A colonist or colony mech spawning on a map (gravship crew or caravan back home) drops every cached count, so their worn gear and a per-tag bill's grown target are counted at once. Temporary probe (2026-11-23, per-tag bills overcrafted by 2-3 after a gravship trip): logs `per-tag bill probe: <recipe> ... wants to craft / finished one: target, cached count, live count, tagged pawns on map` |
| Vanilla Gravship Expanded - Chapter 2 | Its `Building_TurretGun.Tick` postfix calls `WarcomputerHelper.ApplyForcedMissRadiusBuffs` for every player turret once every 250 ticks. The method first walks the colony's building list for an active warcomputer and only then looks at the turret; the answer is used for the gauss gun and gauss howitzer only. Sampling profile: 1.1 s of 5.1 s turret tick time in 3 minutes, 0 s with the fix | Prefix: every other turret skips the call, which for it only computed the unused answer; the two gauss turrets run the original unchanged. Logs `Warcomputer check: skipped for N of M turret calls` once |
| Vanilla | Personal tweak, not a fix: two hot paths in the tick, found with a sampling profile (30 colonists, about 80 mechs and 200 animals, speed 3). `GenTypes.IsSealedWithCache` (called by `GetComp<T>` for a missing comp) locks a dictionary on every call: 29% of main-thread time. Every door that opens or closes asks `HostileTo(door)` once per reachability cache entry: 27% | `IsSealedWithCache` returns `Type.IsSealed` directly (a flag read on this runtime). The door check is asked once per pawn per door faction per tick and remembered; a pawn that turns hostile mid-tick is seen one tick later |
| Vanilla | Every door that opens or closes walks the whole reachability cache (`ReachabilityCache.ClearForHostile`) to drop the entries of pawns hostile to the door: about 51,500 entries per door event in a calm colony of 262 pawns, 0.15 ms per tick (3.6% of the tick) even with the per-entry hostility memo | A count of entries per pawn is kept beside each cache (`AddCachedResult`, `Clear`, `ClearFor`). A door event asks hostility once per pawn that has entries; when none is hostile the walk is skipped, otherwise the game's own walk runs. The first 300 skips and every 256th after still run the walk and compare the entry count; a disagreement turns the shortcut off or falls back to the full walk. Logs `Door reach-cache walk: N of M door events needed no walk ...` once |
| Vanilla | Personal tweak, not a fix: the game runs at most 6 ticks per frame at 3x speed and drops the backlog, so TPS is capped at 6 x FPS and any frame slower than 33 ms costs ticks | Limit raised to 12 ticks per frame at 3x (speed multiplier x 4 instead of x 2): frame rate is traded for tick rate. The 45 ms per-frame tick budget is unchanged. Logs TPS, FPS, mean tick time and limit hits for the first six 20 s windows at 3x |
| Vanilla | Diagnostic, off by default: exact time per phase of the pawn tick, which neither Circinus (sampled timing) nor a sampling profile (safe-point bias) gives | With a file `ticktrace.on` in the mod folder at startup, times every method `Pawn.Tick` / `Pawn.TickInterval` call directly (or, when the file contains the word `stages`, the stages of `DoSingleTick`, `MapPreTick`, `MapPostTick` and `WorldTick`) and logs microseconds and calls per game tick, three times per session. When the file contains the word `reach` it runs the work search probe instead: per work giver, colonists and mechs apart, stopwatch time of its turns in `JobGiver_Work`, `Reachability.CanReach` calls and time, region floods (cache misses) and regions opened per flood, the 25 costliest logged every 3600 ticks, three times per session. Each report ends with a `merge list now` line: how many stacks HaulMerge searches and how many of them are in storage that does not accept them, have no partner stack in their storage group, have a partner whose cell takes no more, or could merge. With the word `haul` in the file instead: `haul list probe` entries 600 and 4200 ticks after startup, listing the things in the haulables list by def, whether each lies outside storage, whether a better place exists without a pawn and with one, and one example per def with the storages involved |
| Vanilla (+ Vanilla Recycling Expanded) | Personal tweak, not a fix: `HaulAIUtility.FindFixedIngredientCount` walks every reachable region and every haulable thing in it until it has the wanted count. Vanilla Recycling Expanded's alloy splitter asks it on every work turn for each splitter that is at most half full; with no loose alloypack on the map every call walks the whole map for an empty list (reach probe: 0.9 ms per turn, 50-70 ms per 3600 ticks) | Prefix: when no thing of that def is spawned on the map, the answer is the empty list the walk would return; otherwise the original runs. Same for every other caller (trash compactor, other mods' loaders). Logs `Ingredient search: N of 300 searches answered without walking the map` once |
| Vanilla | Personal tweak, not a fix: a bill work giver that names more than one bench def (DoBillsCook, ButcherFlesh, Smelter, ...) searches every thing whose def has recipes: 251 on the test map (about 190 pawns, animals included, plus every bench), each reach-checked by `GenClosest` before the work giver looks at its def (reach probe: mech DoBillsCook 36-44 ms per 3600 ticks, never finding a job among them) | Postfix on `WorkGiver_Scanner.PotentialWorkThingsGlobal`, for the exact class `WorkGiver_DoBill` with other than one bench def: the global search set becomes the things that pass the work giver's own `ThingIsUsableBillGiver`, in the game's list order, so the same bench wins. The nearby region search is untouched. Logs `Bill bench listing: 300 work turns searched N bill givers of their own instead of all M on the map each` once |
| Vanilla | Personal tweak, not a fix: the merge work giver searches every stack in storage that is below its stack limit, on every turn of every hauler: 362-368 stacks on the test map, each reach-checked and run through `JobOnThing`, of which 329-332 have no partial stack of their kind in their storage group, 29-30 have one whose cell takes no more (storage cap) and 1-9 can merge (reach probe: 35-51 ms per 3600 ticks, colonists plus mechs) | Postfix on `WorkGiver_Merge.PotentialWorkThingsGlobal`: only stacks for which the partner tests of `JobOnThing` that do not depend on the pawn can pass (another stack in the same storage group that stacks with it, is at least as big, below the limit, in a cell that is valid storage for it). Worked out anew on every turn; all pawn tests stay with the game. A right-click order still sees the full list. Logs `Merge search: 100 work turns searched N stacks that have a possible partner instead of M listed` once |
| Vanilla | Personal tweak, not a fix: a work giver's global search asks `Reachability.CanReach` for every candidate (Refuel: 200 things, every DoBills giver: all 240 bill benches), the game caches one answer per (start district, destination district, traverse parameters), and every miss floods the region graph until it meets the destination: 420-710 regions per flood, 30 and more floods in one Refuel turn. Reach probe: work searches 0.4 ms of a 3.6 ms tick, at least 40% of it reachability | Prefix on `Reachability.CheckRegionBasedReachability`: on a cache miss the flood runs to the end once and the reached districts are kept per (pawn, start district, traverse parameters); later misses from the same place are answered from that list and booked in the game's cache as the game would. Lists are dropped with the game's cache (all, one pawn, pawns hostile to a door) and expire after 600 ticks. The first 5000 list answers of a session are checked against the game's own flood; differences are logged and above 0.2% the memo switches itself off. Logs `Reach flood memo check: N of 5000 answers differed` and `Reach flood memo: N floods, M answers without a flood` once |
| Vanilla (Odyssey) | Personal tweak, not a fix: the gravcore subquest generator walks every quest in the game twice on every tick before its cheap tests (minimum wait, random roll): about 2.4% of main-thread time | Cheap tests first; the quest walk runs only on a tick where a subquest is due. Odds per tick unchanged. Once the maximum wait has passed (the generator then asks on every tick, 0.2 ms each with 700 quests), it asks every 250 ticks |
| Vanilla (Odyssey, + Gravship Fleet) | A visitor told to leave the ship for a gravship launch kept standing after the ritual ended, without eating or sleeping, until it collapsed (a Hospitality guest starved for 1.5 days with food in her pack, and her lost group cost -10 goodwill). `LeaveShip` / `GotoShip` (`JobDriver_BoardOrLeaveGravship`) stand with no end; the ritual cleanup clears the engine's lists and ends only the current job, so a copy still in the job queue starts afterwards | Those jobs end as soon as no grav engine on the pawn's map (any engine def, as Gravship Fleet allows several) lists the pawn; logs `Gravship: <pawn> stopped a stale LeaveShip job`. Pawns in a ritual lord are skipped: the launch ritual sends its own colonists with `GotoShip` without listing them |
| TD Enhancement Pack - Continued | Its "urgent refill" work scan runs a full better-storage search on every haulable thing on the map before checking whether any storage is marked for urgent refill; with nothing marked that is about 5% of the tick for no work | Skips the scan while no building and no stockpile is marked for refill; marking one brings the original scan back |
| TD Enhancement Pack - Continued + Altered Carbon 2 | The "Empty Workbench" alert points at the neural editor for good: it shows a Bills tab, but no recipe names it (its gizmos create the bills) | The alert skips work tables whose def has no recipes at all |
| Altered Carbon 2: ReSleeved | Starting the childbirth ritual from the mother's gizmo picks a sleeve casket as the bed (it is a medical `Building_Bed`; Altered Carbon hides it from ordinary bed searches, but the ritual only asks `PregnancyUtility.IsUsableBedFor`), and the attendant carries her there | `IsUsableBedFor` refuses sleeve caskets, which covers the ritual's targets, its "target still allowed" check and the mother role |
| Vanilla + VFE Pirates (Junkers) | Quest says "raid of roughly this size:" followed by nothing, then the raid comes anyway. `PawnGroupKindWorker_Normal.CanGenerateFrom` checks options against the full point total, but pawn picking caps each pawn at the faction's `maxPawnCostPerTotalPointsCurve`, so the melee-only Junker group (Hussars, 600 each) passes below ~5800 points and yields no pawns | Postfix on `CanGenerateFrom` also rejects a normal group when no option fits under that cap, so another group is rolled |
| Vanilla / unknown | Trade caravan whose trader has a non-trader pawn kind: dismiss does nothing, caravan never exits | A guard holding a trader kind counts as the Trader |
| Vanilla / unknown | Trade ship slave also saved as a world pawn loads twice | World copy dropped after load |
| Vanilla | Trade caravans and visitor groups arrive and leave at once "because of dangerous temperature" at a comfortable temperature: hypothermia/heatstroke stays frozen on a mothballed world pawn (mothballed pawns only age) and the 15% leave trigger fires when that pawn is reused, even a year later | While a world pawn is mothballed, its hypothermia, heatstroke and insect hypothermic slowdown recover at vanilla's off-map rate; caravan members (never mothballed) are unchanged |
| Vanilla | Personal tweak, not a fix: the Alt cell inspector is 336 px wide with a 170 px value column, so room stats are cut ("Unbelievably impressiv...") and their numbers never show | Transpiler on the `CellInspectorDrawer` draw methods widens the window, row, divider and value column by as much as the widest value (or room heading) needs, measured while drawing and applied on the next frame like vanilla's height; 0 when everything fits, at most 300 px (`CellInspectorMaxExtra`) |
| Vanilla Quests Expanded - Ancients | Personal tweak, not a fix: the pneumatic capsule arrival message does not say what arrived, and the items are hauled away before you can look | Transpiler on `Building_PneumaticTubeLaunchPort.Deliver` sends the message after the contents are known and lists up to 6 kinds with total counts |
| Vanilla Social Interactions Expanded (+ UI Rethemed - Inspector Tabs) | "Cured my friend" thought shows the raw `{SECONDPAWN_nameDef} cured my {0} ...` template with "Could not resolve symbol" errors when the patient is an implied relative (sibling, cousin, grandparent): the thought checks all relations, its label only direct ones. UIR then falls back to the vanilla Social tab until restart | Label built from the same tale with the vanilla most-important relation, gendered by the patient |
| Vanilla Plants Expanded - Mushrooms + Biomes! Caverns | Personal tweak, not a fix: Caverns' own patch lets six VPE mushrooms grow in its fungiponics basins (timbercap, healtruffle, stinker, blisscap, bileceps, mycothread); redcap, zapcap, foampuffer, toxpuff, lullabloom, glowcap and thermoregulus cannot be planted there | XML patch adds the `BMT_Fungiponics` sow tag to those seven (`1.7/Patches/VPEMushrooms_Fungiponics.xml`). Research prerequisites and each plant's own growing rules are untouched; zapcap still needs a power conduit under the basin |
| Vanilla Psycasts Expanded (Cold zone) | Personal tweak, not a fix: enemy psycasters cast nothing but Cold zone, because VEF hands NPCs the longest-range autocastable ability when the AI asks for an attack verb without a target, and Cold zone (range 62.9) is the longest; several of them stack zones on one spot, and the zone's additive glow, drawn above pawns, turns into a white-out | `VPE_ColdZone` gets `chance` 0, so NPCs no longer autocast it (colonists can still cast it by hand; only its autocast toggle goes) and fall back to the other frost casts. Its `VPE_ColdGlow` fleck gets half alpha and is drawn on the ground (`MoteLow`) under buildings, items and pawns (`1.7/Patches/VPE_ColdZone.xml`) |
| Vanilla Races Expanded - Sanguophage | Animalfeeders (strigoi) never drink animal hemogen packs on their own, even with packs in their inventory: VRE only added an auto-drink job for corpse packs, vanilla only looks for regular packs | When vanilla finds no prisoner or regular pack, an animalfeeder drinks animal packs the vanilla way (carried, inventory, then nearest on the map) up to max hemogen |
| VEF | Verb range factor mutates the shared verb def and can stick (29.85 instead of 19.9) | Factor applied to the result instead |
| VEF (MVCF) | Multi-verb weapons get 100% accuracy and 1 s cooldown; duplicate stat rows; "Range 1" | Unset verb stats fall back to the weapon's own; duplicate rows hidden; Range row rebuilt |
| VFE Tribals + UI Rethemed - Factions | Visual tweak: the Cornerstones button keeps the vanilla button look in the themed Factions tab (UIR leaves room for it in the title bar but does not restyle it) | While the tab is themed, draws it with UIR's own action button (blue, campfire icon), sized to its label, and widens that title bar gap to fit; VFE's vanilla button otherwise |
| VTE Variations + Reel's Expanded Storage | "Choose graphic" / "Randomize graphic" change nothing on furniture that is also an Adaptive Storage building (wardrobe, royal dresser, tool cabinet, ...), which draw their GraphicsDef texture (base graphic hidden) and cache it, while VEF only swaps the base graphic | For Adaptive Storage defs with VEF's random-graphic comp, a GraphicsDef layer whose texture is one of the variants draws the chosen variant; picking one redraws the building |
| WOWGAG's Performance Patch | Its "fight target reuse" (prefix and postfix on `AttackTargetFinder.BestAttackTarget`) compares the validator delegate's `Method` to know whether a search asks the same question as the remembered one. A turret passes a new delegate on every search, so the runtime resolves the method from the code address each time (`Delegate.FindMethodHandle`). Sampling profiles: 2.5 s and 3.1 s in 3 minutes, about half of all turret tick time and more than the search the reuse saves | Transpiler on `GppFightTarget.Pre` and `Post`: the one `Delegate.Method` call in each goes through a table keyed by what the runtime's answer depends on (code address, second address, target type, delegate type), so it returns the same `MethodInfo`. Combined delegates and methods in unloadable code still use the real call. `Tools/dlgtest` checks table against real call for 26 delegate shapes (`dotnet build`, then run with `DOTNET_ROLL_FORWARD=LatestMajor`). Logs `Fight target validator method: N real lookups of M` once |
| (tooling, this mod) | Personal tooling, not a fix: the game keeps only `Player.log` and `Player-prev.log`, so a problem from two runs ago (a bill dropped on load) could not be traced | At startup, copies the previous run's `Player-prev.log` and Hauler's Dream debug logs into `Logs/Archive`, named by the time that run ended (`Player-2026-11-17_145212.log`), keeping the newest 30 of each |
| (tooling, temporary) | "<pawn> started 10 jobs in one tick" names only the job and its first target | Probe: prefixes on `Pawn_JobTracker.StartJob` and `JobUtility.TryStartErrorRecoverJob` log every target of the looping job (holder, position, forbidden, allowed area, reservable, bill and bench state) as one warning; to be removed once the loops are solved |

Companion XML tweaks: [LocalTweaks](https://github.com/hugalafutro/LocalTweaks).

Build: `Tools/build.ps1` (compiles `Source/*.cs` against the game's DLLs into `1.7/Assemblies`).

<!-- MODLIST:START -->
## Active mod list

766 mods in load order, RimWorld 1.7.5014 rev1195. Generated from ModsConfig.xml by `Tools/modlist.py`.

| # | Mod | Package ID |
|---:|---|---|
| 1 | [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) | `brrainz.harmony` |
| 2 | [Load Them Last](https://steamcommunity.com/sharedfiles/filedetails/?id=3714307386) | `ferny.loadthemlast` |
| 3 | [Harmony 3 Strict Binding Compat (local)](https://github.com/hugalafutro/Harmony3StrictCompat) | `sammael.harmony3strictcompat` |
| 4 | [RimWorld Loading Optimizer (RLO)](https://steamcommunity.com/sharedfiles/filedetails/?id=3817423183) | `aruclya.rimworldloadingoptimizer` |
| 5 | [Loading Progress](https://steamcommunity.com/sharedfiles/filedetails/?id=3535481557) | `ilyvion.loadingprogress` |
| 6 | [[WG] WOWGAG's Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3797541313) | `wowgag.guiperformancepatch` |
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
| 23 | [Vehicle Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404) | `smashphil.vehicleframework` |
| 24 | [[LFS] UI Rethemed - Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3832843406) | `lazyfridaystudio.uirethemed.core` |
| 25 | [Milky Way](https://steamcommunity.com/sharedfiles/filedetails/?id=3773448562) | `andromeda.milkyway` |
| 26 | [ReplaceLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3417393194) | `ferny.replacelib` |
| 27 | [Tabula Rasa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) | `neronix17.toolbox` |
| 28 | [Visual Progression Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3834891951) | `groovy.visualprogression.framework` |
| 29 | [Ambience Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3835975372) | `neko.ambiencecore` |
| 30 | [ilyvion's Laboratory](https://steamcommunity.com/sharedfiles/filedetails/?id=3296362231) | `ilyvion.laboratory` |
| 31 | [Useful Marks](https://steamcommunity.com/sharedfiles/filedetails/?id=3506573327) | `andromeda.usefulmarks` |
| 32 | [Colony Tabs Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3840815834) | `doorajp.colonykit` |
| 33 | [1trickPwnyta's Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2764625248) | `ideologypatch.1trickpwnyta` |
| 34 | [[1.6] Do Not Avoid Fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3303359387) | `antihydrogen.donotavoidfire` |
| 35 | [[CAP] Thunder Snow](https://steamcommunity.com/sharedfiles/filedetails/?id=3590107592) | `cap.thundersnow` |
| 36 | [[DG]Lovly Animal Cuddling](https://steamcommunity.com/sharedfiles/filedetails/?id=3823442009) | `local.animalcuddling` |
| 37 | [[DR] Dream's Damage Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3789796309) | `mjcg.drdamageindicators` |
| 38 | [[DR] Where's My Treasure](https://steamcommunity.com/sharedfiles/filedetails/?id=3559591864) | `mjcg.wheresmytreasure` |
| 39 | [[LFS] UI Rethemed - Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=3831602102) | `lazyfridaystudio.uirethemed.animals` |
| 40 | [[LFS] UI Rethemed - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=3830054994) | `lazyfridaystudio.uirethemed.architect` |
| 41 | [[LFS] UI Rethemed - Colonist Card](https://steamcommunity.com/sharedfiles/filedetails/?id=3828206639) | `lazyfridaystudio.uirethemed.colonistcard` |
| 42 | [[LFS] UI Rethemed - History](https://steamcommunity.com/sharedfiles/filedetails/?id=3830774158) | `lazyfridaystudio.uirethemed.history` |
| 43 | [[LFS] UI Rethemed - Inspector Tabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3825029855) | `lazyfridaystudio.uirethemed.inspectortabs` |
| 44 | [[LFS] UI Rethemed - Main Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3826865706) | `lazyfridaystudio.uirethemed.mainbar` |
| 45 | [[LFS] UI Rethemed - Notifications](https://steamcommunity.com/sharedfiles/filedetails/?id=3827650605) | `lazyfridaystudio.uirethemed.notifications` |
| 46 | [[LFS] UI Rethemed - Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3829391880) | `lazyfridaystudio.uirethemed.quests` |
| 47 | [[LFS] UI Rethemed - Wildlife](https://steamcommunity.com/sharedfiles/filedetails/?id=3828731834) | `lazyfridaystudio.uirethemed.wildlife` |
| 48 | [[LTO] Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=2345493945) | `derekbickley.ltocolonygroupsfinal` |
| 49 | [[NL] Facial Animation - WIP](https://steamcommunity.com/sharedfiles/filedetails/?id=1635901197) | `nals.facialanimation` |
| 50 | [Genetic Heads Framework for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3498759997) | `sd.fa.geneticheadsframework` |
| 51 | [[Og] Automatic Power Switch](https://steamcommunity.com/sharedfiles/filedetails/?id=3031634496) | `og.automaticpowerswitch` |
| 52 | [[Og] Repair Your Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3513376486) | `og.repair.your.gear` |
| 53 | [[sbz] Bookcase](https://steamcommunity.com/sharedfiles/filedetails/?id=3789304104) | `sbz.bookcase` |
| 54 | [[sbz] Fridge](https://steamcommunity.com/sharedfiles/filedetails/?id=3486264784) | `sbz.neatstoragefridge` |
| 55 | [[sbz] Gravship Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3537905298) | `sbz.gravshipstorage` |
| 56 | [[sbz] Neat Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3416243474) | `sbz.neatstorage` |
| 57 | [[sbz] Workbench Shelf](https://steamcommunity.com/sharedfiles/filedetails/?id=3484671309) | `sbz.neatstorageworkbenchshelf` |
| 58 | [[UR] Better Filter for Storage, Clothes and Food Restrictions](https://steamcommunity.com/sharedfiles/filedetails/?id=3753101034) | `uranv.betterfilter` |
| 59 | [[XND] Memorable Auroras (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2270039395) | `mlie.xndmemorableauroras` |
| 60 | [[XND] Visible Pants](https://steamcommunity.com/sharedfiles/filedetails/?id=2264108215) | `xeonovadan.visiblepants` |
| 61 | [Adaptive Primitive Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3400037215) | `adaptive.primitivestorage` |
| 62 | [AdaptiveMealBill](https://steamcommunity.com/sharedfiles/filedetails/?id=3668761367) | `rabiosus.adaptivemealbill` |
| 63 | [Additional Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3692484664) | `ferny.additionalherobackgrounds` |
| 64 | [Advanced Battle Cloak](https://steamcommunity.com/sharedfiles/filedetails/?id=3598135811) | `darjeeling.battlecloak` |
| 65 | [Against all odds](https://steamcommunity.com/sharedfiles/filedetails/?id=3552336221) | `literallyfree.colonistreturnbuff` |
| 66 | [Alfred Ancient - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3579763743) | `ferny.alfredancient` |
| 67 | [All Ideoligions Are Fluid](https://steamcommunity.com/sharedfiles/filedetails/?id=2872901762) | `superniquito.allideoligionsarefluid` |
| 68 | [Allies Fight the Fleshmass Heart](https://steamcommunity.com/sharedfiles/filedetails/?id=3772519386) | `lg.fleshmassalliedtarget` |
| 69 | [Almost There! Fork](https://steamcommunity.com/sharedfiles/filedetails/?id=3515165298) | `duz.almosttherefork` |
| 70 | [Alpha Armoury](https://steamcommunity.com/sharedfiles/filedetails/?id=3622294464) | `sarg.alphaarmoury` |
| 71 | [Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677) | `sarg.alphabiomes` |
| 72 | [Alpha Implants](https://steamcommunity.com/sharedfiles/filedetails/?id=3275091331) | `sarg.alphaimplants` |
| 73 | [Alpha Memes](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814) | `sarg.alphamemes` |
| 74 | [Alpha Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021) | `sarg.alphaprefabs` |
| 75 | [Alpha Random](https://steamcommunity.com/sharedfiles/filedetails/?id=3642631969) | `sarg.alpharandom` |
| 76 | [Always Position Gravship on Landing](https://steamcommunity.com/sharedfiles/filedetails/?id=3738322550) | `ferny.alwayspositiongravshiponlanding` |
| 77 | [Always Start in Daytime](https://steamcommunity.com/sharedfiles/filedetails/?id=3323971974) | `ferny.alwaysstartindaytime` |
| 78 | [Amorphous style pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3382499324) | `xmb.amorphousstylepack.mo` |
| 79 | [ANDH - Animals Nuzzling Detects Horrors](https://steamcommunity.com/sharedfiles/filedetails/?id=3230195082) | `latta.petknowstrueyou` |
| 80 | [Animal Collab Project Vanilla-Style](https://steamcommunity.com/sharedfiles/filedetails/?id=2208467668) | `acpteam.acpvanillastyle` |
| 81 | [Animals Forage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667046888) | `mlie.animalsforage` |
| 82 | [Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1195427067) | `com.bymarcin.architecticons` |
| 83 | [Armors For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3787547731) | `bamba.allbambaarmors` |
| 84 | [Asteroid Mineral Scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3536585361) | `broms.asteroidmineralscanner` |
| 85 | [AutoExtractGenes](https://steamcommunity.com/sharedfiles/filedetails/?id=2882834449) | `nibato.autoextractgenes` |
| 86 | [Automatic Foraging](https://steamcommunity.com/sharedfiles/filedetails/?id=3687750532) | `i.automaticforaging` |
| 87 | [Automatic Night Owl](https://steamcommunity.com/sharedfiles/filedetails/?id=2056012179) | `ben.automaticnightowl` |
| 88 | [Barbs And Moats](https://steamcommunity.com/sharedfiles/filedetails/?id=3601561731) | `sgtbruce.barbsandmoats` |
| 89 | [Basic Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3600443734) | `stefanlog.basictraits` |
| 90 | [Battery Capacity in Stat View](https://steamcommunity.com/sharedfiles/filedetails/?id=3651439731) | `linnun.batterycapacity` |
| 91 | [Bed Rest For Food Poisoning](https://steamcommunity.com/sharedfiles/filedetails/?id=2567901366) | `bedrestforfoodpoisoning.1trickponyta` |
| 92 | [Better Abandoned Settlements](https://steamcommunity.com/sharedfiles/filedetails/?id=3826665236) | `edybtt.betterabandonedsettlements` |
| 93 | [Better Beggars (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3006899215) | `mlie.betterbeggars` |
| 94 | [Better Context Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3696948139) | `mrk.bettercontextmenu` |
| 95 | [Better Distress Call](https://steamcommunity.com/sharedfiles/filedetails/?id=3305489753) | `nalzurin.betterdistresscall` |
| 96 | [Better Gene Inheritance](https://steamcommunity.com/sharedfiles/filedetails/?id=3046776238) | `redmattis.bettergeneinheritance` |
| 97 | [Better ground-penetrating scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3203274131) | `kikohi.bettergroundpenetratingscanner` |
| 98 | [Better Growth Moments](https://steamcommunity.com/sharedfiles/filedetails/?id=3642805464) | `arkymn.bettergrowthmoments` |
| 99 | [Better Linkable Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3577372705) | `ganja.betterlinkablecolors` |
| 100 | [Better Mechanoid Sorting](https://steamcommunity.com/sharedfiles/filedetails/?id=3730840942) | `redstonepanda.bettermechrank` |
| 101 | [Better Mod Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3636406008) | `mumei.bettermodmenu` |
| 102 | [Better Records Tab (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2359778562) | `mlie.betterrecordstab` |
| 103 | [Better Traders Guild](https://steamcommunity.com/sharedfiles/filedetails/?id=3684587591) | `shunter.bettertradersguild` |
| 104 | [Better Trees](https://steamcommunity.com/sharedfiles/filedetails/?id=3539609975) | `chaoticenrico.bettertrees` |
| 105 | [Better Workbench Management - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819555998) | `falconne.bwm` |
| 106 | [Big Bed Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=3201556342) | `amch.bigbedbuff` |
| 107 | [Biosculpter Pod and Neural Supercharger Resize](https://steamcommunity.com/sharedfiles/filedetails/?id=2710513531) | `syrus.bpansresize` |
| 108 | [Birth Quality Lifespan Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3654799710) | `saltgin.birthqualitylifespanfix` |
| 109 | [Blindsight - Unified](https://steamcommunity.com/sharedfiles/filedetails/?id=3248249029) | `spencezilla.blindsightunified` |
| 110 | [Blindsight suffers no Unnatural Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=3258096951) | `emitbreaker.blindnessunnaturaldarkness` |
| 111 | [Block Unwanted Minutiae (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3278213153) | `mlie.blockunwantedminutiae` |
| 112 | [Blueprints - 1.7 Waylith (Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3819556044) | `nova.blueprints.waylith17` |
| 113 | [BuilderTweaksReplica](https://steamcommunity.com/sharedfiles/filedetails/?id=3521096349) | `rabiosus.buildertweaksreplica` |
| 114 | [Caravan Mood Buff](https://steamcommunity.com/sharedfiles/filedetails/?id=2680751877) | `syrus.caravanmoodbuff` |
| 115 | [Change map edge limit](https://steamcommunity.com/sharedfiles/filedetails/?id=1546494565) | `kapitanoczywisty.changemapedge` |
| 116 | [Chester Choose - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3761449697) | `ferny.chesterchoose` |
| 117 | [Childhood Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3000770956) | `zylle.childboodbackstories` |
| 118 | [Choose Where To Land](https://steamcommunity.com/sharedfiles/filedetails/?id=3537970831) | `kearril.choosewheretoland` |
| 119 | [Chronos Pointer - Schedule Menu Enhancement](https://steamcommunity.com/sharedfiles/filedetails/?id=3408065390) | `coolnether123.chronospointer` |
| 120 | [Circinus Dev Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3771602203) | `astryl.circinusdevtools` |
| 121 | [Cleaning Area Relaunched](https://steamcommunity.com/sharedfiles/filedetails/?id=3586697885) | `cerule.cleaningarearelaunched` |
| 122 | [Colored deep resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3203269467) | `kikohi.coloreddeepresources` |
| 123 | [Compositable Loadouts](https://steamcommunity.com/sharedfiles/filedetails/?id=2679126859) | `wiri.compositableloadouts` |
| 124 | [Consciousness Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2913127696) | `vex.consciousness` |
| 125 | [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079) | `phoenix.continuebutton` |
| 126 | [Cook for Yourself](https://steamcommunity.com/sharedfiles/filedetails/?id=3766233150) | `lordfelix.cookforyourself` |
| 127 | [Corpse Description Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3639910228) | `ferny.corpsedescriptionfix` |
| 128 | [Corpse info](https://steamcommunity.com/sharedfiles/filedetails/?id=2913588675) | `arl85.corpseinfo` |
| 129 | [Dark Psychic Rituals: The Following](https://steamcommunity.com/sharedfiles/filedetails/?id=3596468709) | `dprtf.darkpsychicrituals.sentinel` |
| 130 | [Deathrest Pauses Biological Age](https://steamcommunity.com/sharedfiles/filedetails/?id=2940569974) | `crykosity.deathrest` |
| 131 | [Default Ingredient Radius](https://steamcommunity.com/sharedfiles/filedetails/?id=2909706985) | `mlie.defaultingredientradius` |
| 132 | [Deselect Zone](https://steamcommunity.com/sharedfiles/filedetails/?id=3328706359) | `cheesex.deselectzone` |
| 133 | [Designations Tooltip](https://steamcommunity.com/sharedfiles/filedetails/?id=3743959668) | `cheatereater.designationstooltip` |
| 134 | [Designator Shapes](https://steamcommunity.com/sharedfiles/filedetails/?id=1235181370) | `merthsoft.designatorshapes` |
| 135 | [Det's Xenotypes - Avaloi](https://steamcommunity.com/sharedfiles/filedetails/?id=3199692643) | `det.avaloi` |
| 136 | [Det's Xenotypes - Brawnum](https://steamcommunity.com/sharedfiles/filedetails/?id=3429572581) | `det.brawnum` |
| 137 | [Det's Xenotypes - Boglegs](https://steamcommunity.com/sharedfiles/filedetails/?id=3146564944) | `det.boglegs` |
| 138 | [Det's Xenotypes - Buzzers](https://steamcommunity.com/sharedfiles/filedetails/?id=3545293786) | `det.buzzers` |
| 139 | [Det's Xenotypes - Half-foot](https://steamcommunity.com/sharedfiles/filedetails/?id=3530817307) | `det.halffoot` |
| 140 | [Det's Xenotypes - Keshig](https://steamcommunity.com/sharedfiles/filedetails/?id=3376864722) | `det.keshig` |
| 141 | [Det's Xenotypes - Stoneborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2888722722) | `det.stoneborn` |
| 142 | [Det's Xenotypes - Venators](https://steamcommunity.com/sharedfiles/filedetails/?id=3140248688) | `det.venators` |
| 143 | [Dignified Empire Names](https://steamcommunity.com/sharedfiles/filedetails/?id=3530589531) | `goosechoosesviolence.empirenames` |
| 144 | [Dlc collaboration - Void universe](https://steamcommunity.com/sharedfiles/filedetails/?id=3587277884) | `hailuan.voiduniverse` |
| 145 | [Don't Mix Cosmetic Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3621282829) | `wara.dontmixcosmeticgenes` |
| 146 | [Don't Say 100% Unless You Mean It!](https://steamcommunity.com/sharedfiles/filedetails/?id=2894636032) | `kathanon.donemeansdone` |
| 147 | [DragSelect](https://steamcommunity.com/sharedfiles/filedetails/?id=2599942235) | `telardo.dragselect` |
| 148 | [Dress for the Weather (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607724833) | `mlie.dressfortheweather` |
| 149 | [Dubs Apparel Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2296697286) | `dubwise.dubsappareltweaks` |
| 150 | [Dune Style Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2993301551) | `dsp.halituisamaricanous` |
| 151 | [Dynamic Carrying Capacity (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2967228692) | `mlie.dynamiccarryingcapacity` |
| 152 | [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506) | `zeracronius.dynamictradeinterface` |
| 153 | [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092) | `mnmr.eprimereadouts` |
| 154 | [Erin Eccentric](https://steamcommunity.com/sharedfiles/filedetails/?id=2967829336) | `erin.storyteller` |
| 155 | [[WG] Evolving Enemy Strongholds](https://steamcommunity.com/sharedfiles/filedetails/?id=3738072776) | `wowgag.evolvingenemystrongholds` |
| 156 | [Expressive Appendages](https://steamcommunity.com/sharedfiles/filedetails/?id=3830976421) | `remi.expressiveappendages` |
| 157 | [Extra Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=1926923167) | `zylle.extraalerts` |
| 158 | [Faction Raid Cooldown (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3547098393) | `mlie.factionraidcooldown` |
| 159 | [Features Gap Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3753339482) | `ferny.featuresgapfix` |
| 160 | [Fire At Will Gizmo - Evolving Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3770020686) | `zohryu.fireatwillgizmo.evolving` |
| 161 | [FirmamentFX](https://steamcommunity.com/sharedfiles/filedetails/?id=3781872865) | `dorbo.firmamentfx` |
| 162 | [Fix Styled Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2957953663) | `kathanon.fixstyledblueprints` |
| 163 | [Float Sub-Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2864015430) | `kathanon.floatsubmenu` |
| 164 | [Food Poisoning Investigator](https://steamcommunity.com/sharedfiles/filedetails/?id=3697891858) | `leo.foodpoisoninginvestigator` |
| 165 | [Forgetful Mech Resurrection](https://steamcommunity.com/sharedfiles/filedetails/?id=3766269434) | `mrhydralisk.forgetfulmechresurrection` |
| 166 | [Fortifications - Medieval - Material](https://steamcommunity.com/sharedfiles/filedetails/?id=2501487160) | `aoba.fortress.medieval.material` |
| 167 | [Fortifications - Neolithic](https://steamcommunity.com/sharedfiles/filedetails/?id=2385960678) | `aoba.fortress.neolithic` |
| 168 | [Friendly Fire Tweaks (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3591533573) | `zal.friendlyfiretweaks` |
| 169 | [Full Armor - Hands and Feet [1.6]](https://steamcommunity.com/sharedfiles/filedetails/?id=3530977354) | `stevezero.fullarmorhandsfeet` |
| 170 | [Gene Bank Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=3265067908) | `leoltron.genebankindicators` |
| 171 | [Ghouls Prefer Twisted Meat](https://steamcommunity.com/sharedfiles/filedetails/?id=3230171961) | `doug.ghoulsprefertwistedmeat` |
| 172 | [Giddy-Up 2 - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861) | `memegoddess.giddyup` |
| 173 | [Glasma's Refugee Expectations](https://steamcommunity.com/sharedfiles/filedetails/?id=3785120443) | `glasma.refugeeexpectations` |
| 174 | [Go Explore!](https://steamcommunity.com/sharedfiles/filedetails/?id=1814100216) | `albion.goexplore` |
| 175 | [Gojis-Misc-Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=3775114861) | `goji.gojismiscgenes` |
| 176 | [Goodwill Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=3695225114) | `bluebird.goodwillpreview` |
| 177 | [Gore Perfected](https://steamcommunity.com/sharedfiles/filedetails/?id=3814447022) | `tiepi.goreperfected` |
| 178 | [Gravship Crew Manifest Saver](https://steamcommunity.com/sharedfiles/filedetails/?id=3641927599) | `lornath.gravshipcrewmanifestsaver` |
| 179 | [Grenade Fix - Rearmed](https://steamcommunity.com/sharedfiles/filedetails/?id=797360572) | `dingo.grenadefixrearmed` |
| 180 | [Guns For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3782391721) | `bambaryla.allbambaguns` |
| 181 | [Hauler's Dream](https://steamcommunity.com/sharedfiles/filedetails/?id=3742459652) | `giwaffed.haulersdream` |
| 182 | [Heat Map (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2552838384) | `syrus.heatmap` |
| 183 | [Hive Protection](https://steamcommunity.com/sharedfiles/filedetails/?id=3536627722) | `jaxe.hiveprotection` |
| 184 | [Hunt for Me](https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720) | `arandomkiwi.huntforme` |
| 185 | [I'm Off (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3251510002) | `zal.imoff` |
| 186 | [I'm Trying To Save You! (Pawn Dressing Rework)](https://steamcommunity.com/sharedfiles/filedetails/?id=3534863993) | `s4.imtryingtosaveyou` |
| 187 | [Ignore Idle Guests](https://steamcommunity.com/sharedfiles/filedetails/?id=2515048728) | `mlie.ignoreidleguests` |
| 188 | [Injured Carry](https://steamcommunity.com/sharedfiles/filedetails/?id=2413690575) | `haecriver.injuredcarry` |
| 189 | [Inspired Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2851390190) | `lm.inspiredconversion` |
| 190 | [Instant Caravan](https://steamcommunity.com/sharedfiles/filedetails/?id=3165744287) | `ferny.easycaravanformation` |
| 191 | [Integrated Creep Joiners](https://steamcommunity.com/sharedfiles/filedetails/?id=3233429182) | `grillmaster.integratedcreepjoiners` |
| 192 | [Interaction Bubbles](https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345) | `jaxe.bubbles` |
| 193 | [Ion Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532877485) | `zal.ionweaponry` |
| 194 | [IVF Equality](https://steamcommunity.com/sharedfiles/filedetails/?id=2922603310) | `kathanon.ivfequality` |
| 195 | [Just Ignore Me Passing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503627342) | `mlie.justignoremepassing` |
| 196 | [Just Put It Over There](https://steamcommunity.com/sharedfiles/filedetails/?id=2856471776) | `mlie.justputitoverthere` |
| 197 | [KanbanStockpileContinue](https://steamcommunity.com/sharedfiles/filedetails/?id=3776851378) | `igora.kanbanstockpilecontinue` |
| 198 | [Keep Converting](https://steamcommunity.com/sharedfiles/filedetails/?id=3461478214) | `linnun.keepconverting` |
| 199 | [Keyz Misc Resources](https://steamcommunity.com/sharedfiles/filedetails/?id=3355560776) | `keyz182.keyzmiscresources` |
| 200 | [Keyz' Allow Utilities](https://steamcommunity.com/sharedfiles/filedetails/?id=3524716849) | `keyz182.keyzallowutilities` |
| 201 | [Kidnapped Pawns Die Less](https://steamcommunity.com/sharedfiles/filedetails/?id=3308076464) | `conit.kpdl` |
| 202 | [Knick Knacks - Let your colonists decorate!](https://steamcommunity.com/sharedfiles/filedetails/?id=3595196942) | `vaguelysexual.modpackageid.goeshere` |
| 203 | [Last Visited (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3507014727) | `zal.lastvisited` |
| 204 | [Leo - Events](https://steamcommunity.com/sharedfiles/filedetails/?id=3730168769) | `leo.eventsexpanded` |
| 205 | [Less Stupid Romance Attempt (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014603702) | `mlie.lessstupidromanceattempt` |
| 206 | [Letter Permanent Injury](https://steamcommunity.com/sharedfiles/filedetails/?id=2592535960) | `buggy.rimworld.letterpermanentinjury` |
| 207 | [Level Up!](https://steamcommunity.com/sharedfiles/filedetails/?id=1701592470) | `krafs.levelup` |
| 208 | [Linkables Info Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3212476920) | `deadmano.linkablesinfoexpanded` |
| 209 | [Little Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=3331281387) | `tyrannidae.littlecritters` |
| 210 | [Living Bonds](https://steamcommunity.com/sharedfiles/filedetails/?id=3708989445) | `bsandyh.livingbonds` |
| 211 | [Load in Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379) | `ferny.loadinbackground` |
| 212 | [Mark of Manhood](https://steamcommunity.com/sharedfiles/filedetails/?id=3646943505) | `maux36.markofmanhood` |
| 213 | [Mech Recharger Standby Mode](https://steamcommunity.com/sharedfiles/filedetails/?id=2879340506) | `heremeus.mechrechargerstandby` |
| 214 | [Mechanoids dont beat fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3252095758) | `fxz.mechbeatfire` |
| 215 | [Megafauna](https://steamcommunity.com/sharedfiles/filedetails/?id=1055485938) | `spino.megafauna` |
| 216 | [Melee Weapons For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3726524693) | `bamba.allbambamelee.tiered` |
| 217 | [Mine Sight](https://steamcommunity.com/sharedfiles/filedetails/?id=3769804600) | `rabiosus.minesight` |
| 218 | [Mo'Events (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2035143365) | `mlie.moevents` |
| 219 | [Mojave Mask - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3781130446) | `ferny.mojavemask` |
| 220 | [Mono Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2997812588) | `cat.monopack` |
| 221 | [More Childhood Nicknames](https://steamcommunity.com/sharedfiles/filedetails/?id=3688519717) | `river.childhoodnames` |
| 222 | [More Persona Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2863308112) | `arquebus.morepersonatraits` |
| 223 | [More Random Seeds](https://steamcommunity.com/sharedfiles/filedetails/?id=1501463043) | `jaxe.morerandomseeds` |
| 224 | [More Slavery Stuff](https://steamcommunity.com/sharedfiles/filedetails/?id=2896845138) | `garryflowers.moreslaverystuff` |
| 225 | [More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=1931453053) | `zylle.morevanillabiomes` |
| 226 | [MutatorWorldIcons](https://steamcommunity.com/sharedfiles/filedetails/?id=3551904193) | `snobi.mutatorworldicons` |
| 227 | [Name Your Entities](https://steamcommunity.com/sharedfiles/filedetails/?id=3229905841) | `ferny.nameyourentities` |
| 228 | [No Backer Pawns](https://steamcommunity.com/sharedfiles/filedetails/?id=3669069738) | `nobackerpawns.nobackerpawns` |
| 229 | [No Disabled Factions In Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=2892125637) | `kathanon.nodisabledfactions` |
| 230 | [No Hemogen Farm Medical Alert](https://steamcommunity.com/sharedfiles/filedetails/?id=2887053053) | `dark.nohemogenfarmmedicalalert` |
| 231 | [No Infestation On Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=3661608882) | `ialways1ose.noinfestationonbuildings` |
| 232 | [No Job Authors](https://steamcommunity.com/sharedfiles/filedetails/?id=2009825774) | `doug.nojobauthors` |
| 233 | [No Modlist on Loading](https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405) | `ferny.nomodlistonloading` |
| 234 | [No Summon Mech Threat Gizmo](https://steamcommunity.com/sharedfiles/filedetails/?id=2889317343) | `canon.nomechsummonergizmo` |
| 235 | [No Terrain Edge Fade (Hard Floors)](https://steamcommunity.com/sharedfiles/filedetails/?id=3753895045) | `fauxzyi.noterrainedgefade` |
| 236 | [No Version Warning](https://steamcommunity.com/sharedfiles/filedetails/?id=2599504692) | `mlie.noversionwarning` |
| 237 | [Nomad Friendly Quests](https://steamcommunity.com/sharedfiles/filedetails/?id=3616175831) | `jaeger972.nomadfreindlyquests` |
| 238 | [Not My Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2870045856) | `vesper.notmyfault` |
| 239 | [Now Playing](https://steamcommunity.com/sharedfiles/filedetails/?id=3754684545) | `dorbo.nowplaying` |
| 240 | [OgreStack](https://steamcommunity.com/sharedfiles/filedetails/?id=1447140290) | `ogre.ogrestack` |
| 241 | [Packs Are Not Belts](https://steamcommunity.com/sharedfiles/filedetails/?id=2509511179) | `turnovus.submod.backpacksarenotbelts` |
| 242 | [Pawn Editor Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113) | `segaswolf.pawneditor.fork` |
| 243 | [Pawn Name Variety (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3548290568) | `mlie.pawnnamevariety` |
| 244 | [Peer Pressure (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3605155621) | `densevoid.peerpressure` |
| 245 | [Persona Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3766728761) | `shunter.personaweaponsunbound` |
| 246 | [Perspective: Eaves (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3346964576) | `mlie.perspectiveeaves` |
| 247 | [Phase Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3222694245) | `det.phaseweaponry` |
| 248 | [Planning Extended](https://steamcommunity.com/sharedfiles/filedetails/?id=2877392159) | `scherub.planningextended` |
| 249 | [Plasma Weapons Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3070942578) | `cat.plasmapack` |
| 250 | [Playwright](https://steamcommunity.com/sharedfiles/filedetails/?id=3753005413) | `rokk.playwright` |
| 251 | [Pollution Spread](https://steamcommunity.com/sharedfiles/filedetails/?id=3692324370) | `turkler.pollutingfactions` |
| 252 | [Positive Connections](https://steamcommunity.com/sharedfiles/filedetails/?id=3003882069) | `cem.positiveconnections` |
| 253 | [Power Indicators](https://steamcommunity.com/sharedfiles/filedetails/?id=899714534) | `erdelf.powerindicators` |
| 254 | [Pregnancy Attitude](https://steamcommunity.com/sharedfiles/filedetails/?id=3530918349) | `carriedactyl.pregnancyattitude` |
| 255 | [Prioritized Kill Dev Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3533940586) | `prioritizedkilltool.programmerlily.com` |
| 256 | [Prisoner Realism](https://steamcommunity.com/sharedfiles/filedetails/?id=3760196312) | `legator.prisonerrealism` |
| 257 | [Progression: Soundtrack](https://steamcommunity.com/sharedfiles/filedetails/?id=3620726714) | `ferny.progressionsoundtrack` |
| 258 | [Proper Slave Gear](https://steamcommunity.com/sharedfiles/filedetails/?id=3770087153) | `kuchiki.properslavegear` |
| 259 | [Prosthetic No Missing Body Parts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2739055353) | `mlie.prostheticnomissingbodyparts` |
| 260 | [Pulse Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3077879089) | `detvisor.pulseweaponry` |
| 261 | [Punch Button](https://steamcommunity.com/sharedfiles/filedetails/?id=2651392585) | `fuu.punchattack` |
| 262 | [Quality Colors (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3513846773) | `dawnsglow.qualcolor` |
| 263 | [QualityBuilder Unofficial 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3512466087) | `hatti.qualitybuilder` |
| 264 | [Quest Generation Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3625994394) | `jaeger972.questgenerationtweaks` |
| 265 | [Questing Meme](https://steamcommunity.com/sharedfiles/filedetails/?id=2826539854) | `sirmashedpotato.questingmeme` |
| 266 | [Raider Approach Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781897112) | `oldmanyoung.raiderapproachlagfix` |
| 267 | [Range Finder](https://steamcommunity.com/sharedfiles/filedetails/?id=1332119637) | `brrainz.rangefinder` |
| 268 | [Readable Numbers](https://steamcommunity.com/sharedfiles/filedetails/?id=3762441264) | `7h0m4s.rimworldreadablenumbers` |
| 269 | [Real Faction Guest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2886929245) | `mlie.realfactionguest` |
| 270 | [Relevant Stats In Description](https://steamcommunity.com/sharedfiles/filedetails/?id=2692669482) | `mlie.relevantstatsindescription` |
| 271 | [Remove Colonies Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=3745014294) | `ferny.removecolonieslimit` |
| 272 | [Remove Tutorial Button](https://steamcommunity.com/sharedfiles/filedetails/?id=781505663) | `merthsoft.removetutorialbutton` |
| 273 | [Replace Stuff - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526354009) | `memegoddess.replacestuff` |
| 274 | [RescueeJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177170403) | `ogliss.rescueejoinsplus` |
| 275 | [Research-Based Raids Difficulty](https://steamcommunity.com/sharedfiles/filedetails/?id=3679008124) | `leo.researchbasedraidsdifficulty` |
| 276 | [Resource Delivery Helper](https://steamcommunity.com/sharedfiles/filedetails/?id=3696859196) | `ferny.resourcedeliveryhelper` |
| 277 | [Restrict Undrawn Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3629753146) | `ferny.restrictundrawnapparel` |
| 278 | [Reverse Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=858744731) | `brrainz.reversecommands` |
| 279 | [Right-Click Gizmo Indicator](https://steamcommunity.com/sharedfiles/filedetails/?id=3750529401) | `cheatereater.rightclickgizmoindicator` |
| 280 | [Rimpsyche - Personality Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3535112473) | `maux36.rimpsyche` |
| 281 | [RimSaves](https://steamcommunity.com/sharedfiles/filedetails/?id=1713367505) | `arandomkiwi.rimsaves` |
| 282 | [Rimsenal - Augmented Vanilla Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=849231601) | `rimsenal.evp` |
| 283 | [Rimsenal - Security Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=736139206) | `rimsenal.security` |
| 284 | [Rimsenal - Storytellers Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725952322) | `rimsenal.storyteller` |
| 285 | [Rimsenal Faction Pack - Federation](https://steamcommunity.com/sharedfiles/filedetails/?id=736172213) | `rimsenal.federation` |
| 286 | [Rimsenal Faction Pack - Feral](https://steamcommunity.com/sharedfiles/filedetails/?id=736207111) | `rimsenal.feral` |
| 287 | [Rimsenal Faction Pack - Spacer](https://steamcommunity.com/sharedfiles/filedetails/?id=3086137468) | `rimsenal.spacer` |
| 288 | [Rimsenal Style Pack - Techist](https://steamcommunity.com/sharedfiles/filedetails/?id=2661828028) | `rimsenal.techist` |
| 289 | [Rimsenal Style Pack - Urbworld](https://steamcommunity.com/sharedfiles/filedetails/?id=2908039338) | `rimsenal.urb` |
| 290 | [Rimsenal Xenotype Pack - Askbarn](https://steamcommunity.com/sharedfiles/filedetails/?id=2918812858) | `rimsenal.askbarn` |
| 291 | [Rimsenal Xenotype Pack - Harana](https://steamcommunity.com/sharedfiles/filedetails/?id=3228637524) | `rimsenal.harana` |
| 292 | [Rimsenal Xenotype Pack - Zohar](https://steamcommunity.com/sharedfiles/filedetails/?id=3504125251) | `rimsenal.zohar` |
| 293 | [Rimsenal: Murder diversified](https://steamcommunity.com/sharedfiles/filedetails/?id=725947920) | `rimsenal.core` |
| 294 | [RimTraits - General Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=2206957172) | `sierra.rt.generaltraits` |
| 295 | [Ritual Size Attenuation](https://steamcommunity.com/sharedfiles/filedetails/?id=3262033797) | `delmaintweaks.ritualsizeattenuation` |
| 296 | [Romina Ready - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3736785369) | `ferny.rominaready` |
| 297 | [Royalty Room Info (Satisfied Title and Upgrade Requirements)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600393808) | `jaeger972.royaltyroominfo` |
| 298 | [Run and Hide (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2022480344) | `mlie.runandhide` |
| 299 | [Satellite Minimap](https://steamcommunity.com/sharedfiles/filedetails/?id=3793766543) | `astryl.satelliteminimap` |
| 300 | [Save File Compression](https://steamcommunity.com/sharedfiles/filedetails/?id=3562729067) | `amch.savefilecompression` |
| 301 | [Save Storyteller Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3744418945) | `vc.savestorytellersettings` |
| 302 | [Scrollable Inspect Panes](https://steamcommunity.com/sharedfiles/filedetails/?id=2889151469) | `kathanon.scrollableinspectpanes` |
| 303 | [Self Dyeing](https://steamcommunity.com/sharedfiles/filedetails/?id=2562859859) | `avilmask.selfdyeing` |
| 304 | [Sensible Bed Ownership](https://steamcommunity.com/sharedfiles/filedetails/?id=3328702448) | `sensiblebedownership.1trickpwnyta` |
| 305 | [Settlement Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=2240524945) | `dninemfive.settlementdescription` |
| 306 | [Shadow Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3797651319) | `antisodium.shadowfix` |
| 307 | [Show Buildable Material Count](https://steamcommunity.com/sharedfiles/filedetails/?id=3245101540) | `bp.showbuildablematerialcount` |
| 308 | [Show Known Techprints](https://steamcommunity.com/sharedfiles/filedetails/?id=2920370783) | `spacemoth.showknowntechprints` |
| 309 | [Show Me Your Hands](https://steamcommunity.com/sharedfiles/filedetails/?id=2475965842) | `mlie.showmeyourhands` |
| 310 | [Show Mod Updates](https://steamcommunity.com/sharedfiles/filedetails/?id=3218690663) | `mlie.showmodupdates` |
| 311 | [Show Possible Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=3338920230) | `mlie.showpossiblematerials` |
| 312 | [Show Weapon Tallies](https://steamcommunity.com/sharedfiles/filedetails/?id=2901520677) | `kathanon.showweapontallies` |
| 313 | [Show work site faction relations](https://steamcommunity.com/sharedfiles/filedetails/?id=3487185451) | `riebi.showfactionrelations` |
| 314 | [Simple Leadership](https://steamcommunity.com/sharedfiles/filedetails/?id=3668307448) | `pb3n.simpleleadership` |
| 315 | [Simple sidearms](https://steamcommunity.com/sharedfiles/filedetails/?id=927155256) | `petetimessix.simplesidearms` |
| 316 | [SimpleCameraSetting](https://steamcommunity.com/sharedfiles/filedetails/?id=3232415388) | `ray1203.simplecamerasetting` |
| 317 | [Skilled Refueling](https://steamcommunity.com/sharedfiles/filedetails/?id=2873628478) | `mrhydralisk.skilledrefueling` |
| 318 | [Skilled Slaughtering](https://steamcommunity.com/sharedfiles/filedetails/?id=2873284405) | `mrhydralisk.skilledslaughtering` |
| 319 | [Smart Medicine - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3526251680) | `memegoddess.smartmedicine` |
| 320 | [Smart Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=1504723424) | `sarg.smartspeed` |
| 321 | [Smarter Construction](https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773) | `dhultgren.smarterconstruction` |
| 322 | [Smarter Deconstruction and Mining (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3261302741) | `mlie.smarterdeconstructionandmining` |
| 323 | [Smarter Visitors](https://steamcommunity.com/sharedfiles/filedetails/?id=3012491521) | `mlie.smartervisitors` |
| 324 | [Spacer Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3247891820) | `det.spacerarsenal` |
| 325 | [Sparkling Worlds - Full Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=1123043922) | `albion.sparklingworlds.full` |
| 326 | [Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3386164757) | `cat.stellicarsenal` |
| 327 | [Stop, Drop, And Roll! [BAL]](https://steamcommunity.com/sharedfiles/filedetails/?id=2362707956) | `balistafreak.stopdropandroll` |
| 328 | [Storage Stats in Description](https://steamcommunity.com/sharedfiles/filedetails/?id=3740185419) | `ferny.storagestatsindescription` |
| 329 | [Style Change Anytime](https://steamcommunity.com/sharedfiles/filedetails/?id=2880145066) | `dra.stylechangeanytime` |
| 330 | [SurgeryImproved](https://steamcommunity.com/sharedfiles/filedetails/?id=3233032071) | `darkshadow44.surgeryimproved` |
| 331 | [TD Enhancement Pack - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3525414162) | `memegoddess.tdpack` |
| 332 | [Text Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3773691016) | `ferny.texttool` |
| 333 | [The Sims Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3604588393) | `goji.thesimstraits` |
| 334 | [Toggleable Shields](https://steamcommunity.com/sharedfiles/filedetails/?id=3261317430) | `owlchemist.toggleableshields` |
| 335 | [Tooltipped Long Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2303452428) | `reiquard.tooltippedlongnames` |
| 336 | [Trade Ships Drop Spot](https://steamcommunity.com/sharedfiles/filedetails/?id=1969732297) | `smashphil.dropspot` |
| 337 | [Trader Show Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3007612546) | `nachotoast.tradershowxenotype` |
| 338 | [Tribal Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=2879649038) | `shenanigans.tribalbackstories1.4` |
| 339 | [Triumphant Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3647618250) | `ferny.triumphantresearch` |
| 340 | [True Non-Senescence](https://steamcommunity.com/sharedfiles/filedetails/?id=3547377353) | `lunardawn.truenonsenescence` |
| 341 | [Ugly Together](https://steamcommunity.com/sharedfiles/filedetails/?id=2884233923) | `uglytogether.leeh` |
| 342 | [Ultimate Colonist Name Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3498692639) | `hol.ultcolnames` |
| 343 | [Unburned](https://steamcommunity.com/sharedfiles/filedetails/?id=3538336699) | `andromeda.unburned` |
| 344 | [Undraft After Tucking](https://steamcommunity.com/sharedfiles/filedetails/?id=2157495459) | `madarauchiha.undraftaftertucking` |
| 345 | [Unique Weapons Unbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3707263149) | `shunter.uniqueweaponsunbound` |
| 346 | [Universal Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3540066516) | `trigger.universalblueprints` |
| 347 | [Use Bedrolls - Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3534169351) | `memegoddess.usebedrolls` |
| 348 | [Vanilla Animals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2871933948) | `vanillaexpanded.vanillaanimalsexpanded` |
| 349 | [Vanilla Animals Expanded — Waste Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=2962126499) | `vanillaexpanded.vaewaste` |
| 350 | [Vanilla Anomaly Expanded - Insanity](https://steamcommunity.com/sharedfiles/filedetails/?id=3342412435) | `vanillaexpanded.vanomalyeinsanity` |
| 351 | [Vanilla Apparel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814987817) | `vanillaexpanded.vappe` |
| 352 | [Vanilla Apparel Expanded — Accessories](https://steamcommunity.com/sharedfiles/filedetails/?id=2521176396) | `vanillaexpanded.vaeaccessories` |
| 353 | [Vanilla Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3273371966) | `det.vanillaarsenal` |
| 354 | [Vanilla Backstories Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2861806869) | `vanillaexpanded.vanillabackstoriesexpanded` |
| 355 | [Vanilla Events Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742) | `vanillaexpanded.vee` |
| 356 | [Vanilla Factions Expanded - Empire](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380) | `oskarpotocki.vfe.empire` |
| 357 | [Vanilla Factions Expanded - Insectoids 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431) | `oskarpotocki.vfe.insectoid2` |
| 358 | [Vanilla Factions Expanded - Medieval 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874) | `oskarpotocki.vfe.medieval2` |
| 359 | [Vanilla Factions Expanded - Settlers](https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119) | `oskarpotocki.vanillafactionsexpanded.settlersmodule` |
| 360 | [Vanilla Factions Expanded - Tribals](https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283) | `oskarpotocki.vfe.tribals` |
| 361 | [Vanilla Furniture Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143) | `vanillaexpanded.vfecore` |
| 362 | [Vanilla Furniture Expanded - Architect](https://steamcommunity.com/sharedfiles/filedetails/?id=2608762624) | `vanillaexpanded.vfearchitect` |
| 363 | [Vanilla Furniture Expanded - Art](https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023) | `vanillaexpanded.vfeart` |
| 364 | [Vanilla Furniture Expanded - Factory](https://steamcommunity.com/sharedfiles/filedetails/?id=3686924415) | `vanillaexpanded.vfefactory` |
| 365 | [Vanilla Furniture Expanded - Farming](https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779) | `vanillaexpanded.vfefarming` |
| 366 | [Vanilla Furniture Expanded - Medical Module](https://steamcommunity.com/sharedfiles/filedetails/?id=1718191613) | `vanillaexpanded.vfemedical` |
| 367 | [Vanilla Furniture Expanded - Power](https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477) | `vanillaexpanded.vfepower` |
| 368 | [Vanilla Furniture Expanded - Production](https://steamcommunity.com/sharedfiles/filedetails/?id=1880253632) | `vanillaexpanded.vfeproduction` |
| 369 | [Vanilla Furniture Expanded - Props and Decor](https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149) | `vanillaexpanded.vfepropsanddecor` |
| 370 | [Vanilla Furniture Expanded - Security](https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007) | `vanillaexpanded.vfesecurity` |
| 371 | [Vanilla Furniture Expanded - Spacer Module](https://steamcommunity.com/sharedfiles/filedetails/?id=2028381079) | `vanillaexpanded.vfespacer` |
| 372 | [Vanilla Gravship Expanded - Chapter 1](https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606) | `vanillaexpanded.gravship` |
| 373 | [Vanilla Helixien Gas Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2877699803) | `vanillaexpanded.helixiengas` |
| 374 | [Vanilla Ideology Expanded - Hats and Rags](https://steamcommunity.com/sharedfiles/filedetails/?id=2567387768) | `vanillaexpanded.viehar` |
| 375 | [Vanilla Ideology Expanded - Icons and Symbols](https://steamcommunity.com/sharedfiles/filedetails/?id=2552609458) | `vanillaexpanded.ideo.iconsandsymbols` |
| 376 | [Vanilla Ideology Expanded - Relics and Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2564895018) | `vanillaexpanded.ideo.relicsandartifacts` |
| 377 | [Vanilla Ideology Expanded - Sophian Style](https://steamcommunity.com/sharedfiles/filedetails/?id=3194606539) | `vanillaexpanded.ideo.sophianstyle` |
| 378 | [Vanilla Landmarks Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229) | `vanillaexpanded.vexploratione` |
| 379 | [Vanilla Loot Beams](https://steamcommunity.com/sharedfiles/filedetails/?id=3746929144) | `astryl.vanillalootbeams` |
| 380 | [Vanilla Nutrient Paste Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763) | `vanillaexpanded.vnutriente` |
| 381 | [Vanilla Plants Expanded - Succulents](https://steamcommunity.com/sharedfiles/filedetails/?id=2198652536) | `vanillaexpanded.vplantsesucculents` |
| 382 | [Vanilla Quests Expanded - Ancients](https://steamcommunity.com/sharedfiles/filedetails/?id=3618306875) | `vanillaquestsexpanded.ancients` |
| 383 | [Vanilla Quests Expanded - The Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3411401573) | `vanillaquestsexpanded.generator` |
| 384 | [Vanilla Races Expanded - Custom Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2917311689) | `vanillaracesexpanded.customicons` |
| 385 | [Vanilla Races Expanded - Fungoid](https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053) | `vanillaracesexpanded.fungoid` |
| 386 | [Vanilla Races Expanded - Genie](https://steamcommunity.com/sharedfiles/filedetails/?id=2901424072) | `vanillaracesexpanded.genie` |
| 387 | [Vanilla Races Expanded - Highmate](https://steamcommunity.com/sharedfiles/filedetails/?id=2995385834) | `vanillaracesexpanded.highmate` |
| 388 | [Vanilla Races Expanded - Hussar](https://steamcommunity.com/sharedfiles/filedetails/?id=2893586390) | `vanillaracesexpanded.hussar` |
| 389 | [Vanilla Races Expanded - Lycanthrope](https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100) | `vanillaracesexpanded.lycanthrope` |
| 390 | [Vanilla Races Expanded - Phytokin](https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805) | `vanillaracesexpanded.phytokin` |
| 391 | [Vanilla Races Expanded - Pigskin](https://steamcommunity.com/sharedfiles/filedetails/?id=3164798809) | `vanillaracesexpanded.pigskin` |
| 392 | [Vanilla Races Expanded - Sanguophage](https://steamcommunity.com/sharedfiles/filedetails/?id=2963116383) | `vanillaracesexpanded.sanguophage` |
| 393 | [Vanilla Races Expanded - Saurid](https://steamcommunity.com/sharedfiles/filedetails/?id=2880990495) | `vanillaracesexpanded.saurid` |
| 394 | [Vanilla Races Expanded - Starjack](https://steamcommunity.com/sharedfiles/filedetails/?id=3531912428) | `vanillaracesexpanded.starjack` |
| 395 | [Vanilla Races Expanded - Waster](https://steamcommunity.com/sharedfiles/filedetails/?id=2983471725) | `vanillaracesexpanded.waster` |
| 396 | [Vanilla Recycling Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3155781848) | `vanillaexpanded.recycling` |
| 397 | [Vanilla Skills Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3400246558) | `vanillaexpanded.skills` |
| 398 | [Vanilla Social Interactions Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083) | `vanillaexpanded.vanillasocialinteractionsexpanded` |
| 399 | [Vanilla Storytellers Expanded - Perry Persistent](https://steamcommunity.com/sharedfiles/filedetails/?id=2149702069) | `vse.perrypersistent` |
| 400 | [Vanilla Temperature Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258) | `vanillaexpanded.temperature` |
| 401 | [Vanilla Traits Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655) | `vanillaexpanded.vanillatraitsexpanded` |
| 402 | [Vanilla Weapons Expanded - Bioferrite](https://steamcommunity.com/sharedfiles/filedetails/?id=3287469135) | `vanillaexpanded.vwebf` |
| 403 | [Vanilla Weapons Expanded - Coilguns](https://steamcommunity.com/sharedfiles/filedetails/?id=2375692535) | `vanillaexpanded.vwec` |
| 404 | [Vanilla Weapons Expanded - Grenades](https://steamcommunity.com/sharedfiles/filedetails/?id=2194472657) | `vanillaexpanded.vweg` |
| 405 | [Vanilla Weapons Expanded - Makeshift](https://steamcommunity.com/sharedfiles/filedetails/?id=2419690698) | `vanillaexpanded.vwems` |
| 406 | [Vanilla Weapons Expanded - Non-Lethal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918354) | `vanillaexpanded.vwenl` |
| 407 | [Vanilla Weapons Expanded - Tribal](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918552) | `vanillaexpanded.vwetb` |
| 408 | [Vanometric Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3041187151) | `hlx.vanometricgenerator` |
| 409 | [Various Space Ship Chunk (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2014616487) | `mlie.variousspaceshipchunk` |
| 410 | [Venerated Xenotype Flexibility (Revisited)](https://steamcommunity.com/sharedfiles/filedetails/?id=3586153375) | `dcszar.veneratedxenotypeflexibility` |
| 411 | [VFE Pack for Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3612525913) | `cantaloupetheclown.vanillafurnitureexpandedpack` |
| 412 | [Victor Veto - Progression Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3745138223) | `ferny.victorveto` |
| 413 | [Volt Weaponry](https://steamcommunity.com/sharedfiles/filedetails/?id=3551721422) | `det.voltweaponry` |
| 414 | [VV - New Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=3448458106) | `vvenchov.vvnewharvest` |
| 415 | [WanderJoinsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=2177016016) | `ogliss.wanderjoinsplus` |
| 416 | [We Are United](https://steamcommunity.com/sharedfiles/filedetails/?id=3030804445) | `sl4vp0wer.weareunited` |
| 417 | [Weather Duration](https://steamcommunity.com/sharedfiles/filedetails/?id=2822836257) | `kathanon.weatherduration` |
| 418 | [What's Missing?](https://steamcommunity.com/sharedfiles/filedetails/?id=3231090162) | `revolus.whatsmissing` |
| 419 | [What's That Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2258431182) | `co.uk.epicguru.whatsthatmod` |
| 420 | [Who shot my leg off?](https://steamcommunity.com/sharedfiles/filedetails/?id=3491552121) | `tixiv.whoshotmylegoff` |
| 421 | [Wololoo - Better Conversion and Recruitment](https://steamcommunity.com/sharedfiles/filedetails/?id=3108763487) | `redmattis.betterconversion` |
| 422 | [Work History](https://steamcommunity.com/sharedfiles/filedetails/?id=3727287885) | `lebek.workhistory` |
| 423 | [Xenotype Backstories](https://steamcommunity.com/sharedfiles/filedetails/?id=3787666264) | `ceramic.xenotypebackstories` |
| 424 | [Xenotype Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832924427) | `lakinmanheim.xenotypedescriptions` |
| 425 | [Xenotype Summary](https://steamcommunity.com/sharedfiles/filedetails/?id=3710012135) | `zhuma.xenotypesummary` |
| 426 | [[AV] Ancient Spacer Xenos](https://steamcommunity.com/sharedfiles/filedetails/?id=3241589828) | `veltaris.xenos` |
| 427 | [[AV] Kosmian Xenotype](https://steamcommunity.com/sharedfiles/filedetails/?id=3774474578) | `veltaris.kosmian` |
| 428 | [[AV] Mechanoid orbital traders](https://steamcommunity.com/sharedfiles/filedetails/?id=3172198798) | `veltaris.mechcrate` |
| 429 | [[AV] Mechtech](https://steamcommunity.com/sharedfiles/filedetails/?id=3339795890) | `veltaris.mechtech` |
| 430 | [[AV] Toxic Waste Disposer](https://steamcommunity.com/sharedfiles/filedetails/?id=3440190384) | `veltaris.wastecollector` |
| 431 | [[BTD] Gravship Blueprints](https://steamcommunity.com/sharedfiles/filedetails/?id=3575162262) | `btd.remix.gravshipblueprints` |
| 432 | [[ETF] Butchering Now Yields Hemogen!](https://steamcommunity.com/sharedfiles/filedetails/?id=3688485013) | `echotidefish.butcheringnowyieldshemogen` |
| 433 | [[LFS] UI Rethemed - Factions](https://steamcommunity.com/sharedfiles/filedetails/?id=3832850311) | `lazyfridaystudio.uirethemed.factions` |
| 434 | [[LFS] UI Rethemed - Ideology](https://steamcommunity.com/sharedfiles/filedetails/?id=3834172773) | `lazyfridaystudio.uirethemed.ideology` |
| 435 | [[NL] Facial Animation - Experimentals](https://steamcommunity.com/sharedfiles/filedetails/?id=2581693737) | `nals.facialanimationexperimentals` |
| 436 | [Achtung!](https://steamcommunity.com/sharedfiles/filedetails/?id=730936602) | `brrainz.achtung` |
| 437 | [Alpha Props - Parks and Gardens](https://steamcommunity.com/sharedfiles/filedetails/?id=3146268928) | `sarg.alphapropsparks` |
| 438 | [Alpha Skills](https://steamcommunity.com/sharedfiles/filedetails/?id=3448953006) | `sarg.alphaskills` |
| 439 | [Biomes! Core](https://steamcommunity.com/sharedfiles/filedetails/?id=2038000893) | `biomesteam.biomescore` |
| 440 | [Capture Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787736781) | `ferny.captureexpansion` |
| 441 | [Coilguns Addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3788730287) | `aqued.coilguns` |
| 442 | [Colony Manager Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=3310027356) | `ilyvion.colonymanagerredux` |
| 443 | [Decor into Styles](https://steamcommunity.com/sharedfiles/filedetails/?id=3771249138) | `ferny.decorintostyles` |
| 444 | [Expanded Prosthetics and Organ Engineering - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=1949064302) | `vat.epoeforked` |
| 445 | [FEB's Biopunk Livestock](https://steamcommunity.com/sharedfiles/filedetails/?id=3556058978) | `feb.biopunklivestock` |
| 446 | [Fix Multi Stack Hoppers](https://steamcommunity.com/sharedfiles/filedetails/?id=3534591980) | `zokradonh.supporthoppermultistacks` |
| 447 | [Flourish - Everyday Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3810518822) | `astryl.flourisheveryday` |
| 448 | [Fortifications - Industrial](https://steamcommunity.com/sharedfiles/filedetails/?id=2561619583) | `aoba.fortress.industrial` |
| 449 | [Gerrymon's Misc Props: Dungeon](https://steamcommunity.com/sharedfiles/filedetails/?id=3772571876) | `gmmp.dungeon` |
| 450 | [Gravship Fleet](https://steamcommunity.com/sharedfiles/filedetails/?id=3787239201) | `asunib.gravshipfleet` |
| 451 | [Gravship Raids](https://steamcommunity.com/sharedfiles/filedetails/?id=3767338163) | `sk.gravshipraids` |
| 452 | [Helixien Gas Brazier](https://steamcommunity.com/sharedfiles/filedetails/?id=2770815124) | `cixwow.helixienbrazier` |
| 453 | [Helixien Gas Production](https://steamcommunity.com/sharedfiles/filedetails/?id=2829208414) | `cixwow.helixiengasproduction` |
| 454 | [IMPACT - Combat Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=3812370282) | `astryl.impact` |
| 455 | [Improved Map Search](https://steamcommunity.com/sharedfiles/filedetails/?id=3547866455) | `kathanon.groupedmapsearch` |
| 456 | [Junkie Knick Knacks](https://steamcommunity.com/sharedfiles/filedetails/?id=3607387774) | `ucp.junkieprops` |
| 457 | [More Phytokin Xenotypes](https://steamcommunity.com/sharedfiles/filedetails/?id=3813857865) | `pumpkin.morephytokinxenotypes` |
| 458 | [Nice Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3328729902) | `andromeda.nicehealthtab` |
| 459 | [Nice Research Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3773496046) | `andromeda.niceresearchtab` |
| 460 | [Permits: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3521089734) | `smxrez.permitsreexamined` |
| 461 | [Power: Re-Examined](https://steamcommunity.com/sharedfiles/filedetails/?id=3590186274) | `smxrez.powerreexamined` |
| 462 | [Reel's Expanded Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3237638097) | `reel.expanded.storage` |
| 463 | [Searchable Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2928608119) | `kathanon.searchablemenus` |
| 464 | [Show Me Your Tools - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3774821567) | `meathax.showmeyourtools` |
| 465 | [Sleek Work Schedule Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3742689571) | `squishyjellyfish.nicescheduletab` |
| 466 | [Trait and Backstory Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=2873494547) | `superniquito.traiticons` |
| 467 | [Universal Blueprints Alpha Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3573901897) | `trigger.universalblueprints.alphapack` |
| 468 | [Vanilla Armour Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814988282) | `vanillaexpanded.varme` |
| 469 | [Vanilla Chemfuel Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2792917473) | `vanillaexpanded.vchemfuele` |
| 470 | [Vanilla Cooking Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519) | `vanillaexpanded.vcooke` |
| 471 | [Vanilla Expanded: Hero Backgrounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3313700572) | `ferny.vanillaexpandedherobackgrounds` |
| 472 | [Vanilla Factions Expanded - Deserters](https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377) | `oskarpotocki.vfe.deserters` |
| 473 | [Vanilla Fishing Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942) | `vanillaexpanded.vcef` |
| 474 | [Vanilla Persona Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2826922787) | `vanillaexpanded.vpersonaweaponse` |
| 475 | [Vanilla Psycasts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659) | `vanillaexpanded.vpsycastse` |
| 476 | [Vanilla Skills Rexamined](https://steamcommunity.com/sharedfiles/filedetails/?id=3235834179) | `gravenwitch.vsrexamined` |
| 477 | [Vanilla Weapons Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360) | `vanillaexpanded.vwe` |
| 478 | [VFE - Insectoids 2 Deconstructable Infested Ship Chunks](https://steamcommunity.com/sharedfiles/filedetails/?id=3641480952) | `decinfshipchunkspatch.vfe.insectoid2` |
| 479 | [VSIE - Rational Trait Development](https://steamcommunity.com/sharedfiles/filedetails/?id=2916405546) | `stagz.vsierationaltraitdevelopment` |
| 480 | [Zalcore Prefabs](https://steamcommunity.com/sharedfiles/filedetails/?id=3104631106) | `zal.prefabs` |
| 481 | [[AV] Mechanoid Spots](https://steamcommunity.com/sharedfiles/filedetails/?id=3745387929) | `veltaris.mechstations` |
| 482 | [[BTD] Ship Pack: Community Designs](https://steamcommunity.com/sharedfiles/filedetails/?id=3832496468) | `btd.gbp.shippack.communitydesigns` |
| 483 | [[FSF] Advanced Bionics Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2006925330) | `frozensnowfox.advancedbionicsexpansion` |
| 484 | [Biomes! Oasis](https://steamcommunity.com/sharedfiles/filedetails/?id=2538518381) | `biomesteam.oasis` |
| 485 | [Busywork](https://steamcommunity.com/sharedfiles/filedetails/?id=3775253009) | `andromeda.busywork` |
| 486 | [Cat's Boots and Gloves](https://steamcommunity.com/sharedfiles/filedetails/?id=3552813398) | `catlover366.bootsandgloves` |
| 487 | [Clean Command Bar](https://steamcommunity.com/sharedfiles/filedetails/?id=3688400757) | `leo.cleancommandbar` |
| 488 | [EPOE-Forked: Royalty DLC expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=2008970276) | `vat.epoeforkedroyalty` |
| 489 | [Farming Hysteresis](https://steamcommunity.com/sharedfiles/filedetails/?id=2431191516) | `ilyvion.farminghysteresis` |
| 490 | [Fortification Industrial - Citadel](https://steamcommunity.com/sharedfiles/filedetails/?id=2887532326) | `aoba.fortress.industrial.citadel` |
| 491 | [Fortification Industrial - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=3093455596) | `aoba.fortress.frontier` |
| 492 | [Fortification Industrial -Nuclear Dawn](https://steamcommunity.com/sharedfiles/filedetails/?id=2733185331) | `aoba.fortress.industrial.nucleardawn` |
| 493 | [Genderbending Corrupted Obelisk](https://steamcommunity.com/sharedfiles/filedetails/?id=3350774891) | `abraxas.genderbending.corrupted.obelisk` |
| 494 | [Genetic Biotech for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317537) | `sd.fa.geneticheadsbiotech` |
| 495 | [Genetic Mods for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3501317734) | `sd.fa.geneticheadsmods` |
| 496 | [Industrial Hardleather, Concrete and Cheese](https://steamcommunity.com/sharedfiles/filedetails/?id=3535926848) | `talias.vfehardleatherconcrete` |
| 497 | [Legendary Boss](https://steamcommunity.com/sharedfiles/filedetails/?id=3538894636) | `andromeda.legendaryboss` |
| 498 | [Modern Psycasts UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3745137736) | `astryl.modernpsycastsui` |
| 499 | [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) | `andromeda.nicebilltab` |
| 500 | [Nice Plants Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3685058533) | `andromeda.niceplantsmenu` |
| 501 | [Nicer Health Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3701712250) | `that0neperson.nhtprostheticsextended` |
| 502 | [ReGrowth 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569) | `regrowth.botr.core` |
| 503 | [Useful Marks in Colony Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3720856279) | `truemogician.usefulmarksincolonygroups` |
| 504 | [Vanilla Fishing Expanded - Fishing Treasures AddOn](https://steamcommunity.com/sharedfiles/filedetails/?id=2468543398) | `vanillaexpanded.vcefaddon` |
| 505 | [Vanilla Psycasts Expanded - Hemosage](https://steamcommunity.com/sharedfiles/filedetails/?id=2990596478) | `vanillaexpanded.vpe.hemosage` |
| 506 | [Vanilla Textures Expanded - [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=2816938779) | `vanillaexpanded.vtexe.facialanims` |
| 507 | [Vanilla Weapons Expanded - Heavy Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2329126791) | `vanillaexpanded.vwehw` |
| 508 | [Vanilla Weapons Expanded - Laser](https://steamcommunity.com/sharedfiles/filedetails/?id=1989352844) | `vanillaexpanded.vwel` |
| 509 | [Vanilla Weapons Expanded - Quickdraw](https://steamcommunity.com/sharedfiles/filedetails/?id=1906780517) | `vanillaexpanded.vweq` |
| 510 | [A Dog Said... Animal Prosthetics 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3238353862) | `sambucher.adogsaidanimalprosthetics2` |
| 511 | [Biomes! Polluted Lands](https://steamcommunity.com/sharedfiles/filedetails/?id=3390196656) | `biomesteam.biomespollutedlands` |
| 512 | [Bionic icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1677616980) | `automatic.bionicicons` |
| 513 | [Bionic Irises for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3828692274) | `neztech.fabioniciris` |
| 514 | [Drafted Auto-Combat](https://steamcommunity.com/sharedfiles/filedetails/?id=3546270076) | `redmattis.autocombat` |
| 515 | [Glitter Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3768428526) | `zal.glitterweaponry` |
| 516 | [Gulden Biome (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3607066070) | `pphhyy.guldennew` |
| 517 | [More and Retextured Head Shapes for VTE for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3407049972) | `sd.fa.vte.moreheadshapes` |
| 518 | [Nice Bill Tab - Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3721023311) | `hicon.nicebilltabexpansion` |
| 519 | [Stem's VWE Industrial Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3787566245) | `stem.vweindustrialexpansion` |
| 520 | [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948) | `oskarpotocki.vfe.pirates` |
| 521 | [Vanilla Plants Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2134308522) | `vanillaexpanded.vplantse` |
| 522 | [Vanilla Weapons Expanded - Frontier](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918139) | `vanillaexpanded.vweft` |
| 523 | [WVC - Work Modes](https://steamcommunity.com/sharedfiles/filedetails/?id=2888380373) | `wvc.sergkart.biotech.moremechanoidsworkmodes` |
| 524 | [Altered Carbon 2: ReSleeved](https://steamcommunity.com/sharedfiles/filedetails/?id=2196278117) | `hlx.ultratechalteredcarbon` |
| 525 | [Eltex Weaponry (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532192464) | `zal.eltexweaponry` |
| 526 | [Progression: Storytellers](https://steamcommunity.com/sharedfiles/filedetails/?id=3361425712) | `ferny.progressionstorytellers` |
| 527 | [Unique Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=3776637622) | `shunter.uniquemeleeweapons` |
| 528 | [Vanilla Plants Expanded - More Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667) | `vanillaexpanded.vplantsemore` |
| 529 | [Vanilla Plants Expanded - Mushrooms](https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281) | `vanillaexpanded.vplantsemushrooms` |
| 530 | [Vanilla Races Expanded - Insector](https://steamcommunity.com/sharedfiles/filedetails/?id=3260509684) | `vanillaracesexpanded.insector` |
| 531 | [Alpha Animals](https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856) | `sarg.alphaanimals` |
| 532 | [Vanilla Gravship Expanded - Chapter 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3799737423) | `vanillaexpanded.gravship2` |
| 533 | [Vanilla Storytellers Expanded - Igor Invader (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3582195822) | `zal.vseigorinvader` |
| 534 | [Alpha Genes](https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502) | `sarg.alphagenes` |
| 535 | [[FSF] Better Exploration Loot](https://steamcommunity.com/sharedfiles/filedetails/?id=3526957922) | `frozensnowfox.betterexplorationloot` |
| 536 | [Alpha Genes Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3751293981) | `lucius.alphagenesfa` |
| 537 | [Alpha Genes Integrated](https://steamcommunity.com/sharedfiles/filedetails/?id=3538704272) | `kupa.alphagenesintegrated` |
| 538 | [Keyz Reference Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3689557041) | `keyz182.keyzreferencetools` |
| 539 | [Bullet Sounds](https://steamcommunity.com/sharedfiles/filedetails/?id=3806470380) | `senilia.bulletsounds` |
| 540 | [Cherry picker scroll bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3781285156) | `lucius.cherryscroll` |
| 541 | [Thunderstorm](https://steamcommunity.com/sharedfiles/filedetails/?id=3278460897) | `neko.thunderstorm` |
| 542 | [Shortcuts (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3503701770) | `mlie.shortcuts` |
| 543 | [Big Little Mod Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2710382569) | `daria40k.biglittlemodpatch` |
| 544 | [Muzzle Flash](https://steamcommunity.com/sharedfiles/filedetails/?id=2917732219) | `issaczhuang.muzzleflash` |
| 545 | [Turrets For All Post-Medieval Tech Levels](https://steamcommunity.com/sharedfiles/filedetails/?id=3800286808) | `bambaryla.allbambaturrets` |
| 546 | [Trystan Traveller's Shuttle Schematics](https://steamcommunity.com/sharedfiles/filedetails/?id=3655212016) | `trouperton.trystantravellersshuttleschematics` |
| 547 | [Help me build](https://steamcommunity.com/sharedfiles/filedetails/?id=3534699220) | `andromeda.helpmebuild` |
| 548 | [Alpha Books](https://steamcommunity.com/sharedfiles/filedetails/?id=3403180654) | `sarg.alphabooks` |
| 549 | [Progression: Scenarios](https://steamcommunity.com/sharedfiles/filedetails/?id=3378384387) | `ferny.progressionscenarios` |
| 550 | [Tunnelers ARE deep drills - Resource Filter Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3828716806) | `stainum.tunnelersaredeepdrills.resourcefilterpatch` |
| 551 | [Vanilla Base Generation Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3209927822) | `vanillaexpanded.basegeneration` |
| 552 | [[KCB] Liftoff Effects (이륙 이펙트)](https://steamcommunity.com/sharedfiles/filedetails/?id=3837981505) | `kimchibok.rocketpodlaunch` |
| 553 | [Geological Landforms](https://steamcommunity.com/sharedfiles/filedetails/?id=2773943594) | `m00nl1ght.geologicallandforms` |
| 554 | [Map Preview](https://steamcommunity.com/sharedfiles/filedetails/?id=2800857642) | `m00nl1ght.mappreview` |
| 555 | [Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206) | `xmb.ancienturbanruins.mo` |
| 556 | [Biomes! Caverns](https://steamcommunity.com/sharedfiles/filedetails/?id=2969748433) | `biomesteam.biomescaverns` |
| 557 | [More Prop Categories](https://steamcommunity.com/sharedfiles/filedetails/?id=3167021055) | `ferny.propscore` |
| 558 | [Decor into Props](https://steamcommunity.com/sharedfiles/filedetails/?id=3771248988) | `ferny.decorintoprops` |
| 559 | [Vanilla Vehicles Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3014906877) | `oskarpotocki.vanillavehiclesexpanded` |
| 560 | [Vanilla Vehicles Expanded - Tier 3](https://steamcommunity.com/sharedfiles/filedetails/?id=3047892432) | `oskarpotocki.vanillavehiclesexpandedtier3` |
| 561 | [Vanilla Vehicles Expanded - Upgrades](https://steamcommunity.com/sharedfiles/filedetails/?id=3302208420) | `oskarpotocki.vanillavehiclesexpandedupgrades` |
| 562 | [[LFS] UI Rethemed - Relationships](https://steamcommunity.com/sharedfiles/filedetails/?id=3840876824) | `lazyfridaystudio.uirethemed.relationships` |
| 563 | [[WG] Event Image Popup](https://steamcommunity.com/sharedfiles/filedetails/?id=3758529396) | `wowgag.eventimagepopup` |
| 564 | [倒地不散落物品 / NonUnoPinata (1.7 Unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=3820019852) | `nova.nonunopinata.waylith17` |
| 565 | [Hospitality (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3509486825) | `orion.hospitality` |
| 566 | [Bradson's Main Button Icons (Forked + Expanded)](https://steamcommunity.com/sharedfiles/filedetails/?id=3532359201) | `bs.mbifvte` |
| 567 | [Hospitality - Invite to Stay](https://steamcommunity.com/sharedfiles/filedetails/?id=3783522506) | `gatoviejo.hospitality.invitetostay` |
| 568 | [Hospitality: Casino](https://steamcommunity.com/sharedfiles/filedetails/?id=2939292644) | `adamas.hospitalitycasino` |
| 569 | [Hospitality: Vending machines](https://steamcommunity.com/sharedfiles/filedetails/?id=3014885065) | `adamas.vendingmachines` |
| 570 | [RimQuest (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2263331727) | `mlie.rimquest` |
| 571 | [Vanilla Textures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324) | `vanillaexpanded.vtexe` |
| 572 | [Food Texture Variety Core](https://steamcommunity.com/sharedfiles/filedetails/?id=3354455179) | `goat.food.texture.variety.core` |
| 573 | [More Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=2707120862) | `tidal.morevanilla.textures` |
| 574 | [Power Tab 2 - Fixed Fork (1.6/1.7)](https://steamcommunity.com/sharedfiles/filedetails/?id=3528086894) | `owlchemist.powertab.forked.runningbugs` |
| 575 | [Stylish Dialogs](https://steamcommunity.com/sharedfiles/filedetails/?id=3827006806) | `costel.stylishdialogs` |
| 576 | [Battery Charge Display](https://steamcommunity.com/sharedfiles/filedetails/?id=3839412450) | `shamlan.batterychargedisplay` |
| 577 | [What Can I Sell?](https://steamcommunity.com/sharedfiles/filedetails/?id=3838492956) | `luissousa.whatcanisell` |
| 578 | [Quiet Wonders](https://steamcommunity.com/sharedfiles/filedetails/?id=3822357701) | `choco.quietwonders` |
| 579 | [Vanilla Outposts Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2688941031) | `vanillaexpanded.outposts` |
| 580 | [Vanilla Ideology Expanded - Dryads](https://steamcommunity.com/sharedfiles/filedetails/?id=2720631512) | `vanillaexpanded.ideo.dryads` |
| 581 | [Vanilla Races Expanded - Archon](https://steamcommunity.com/sharedfiles/filedetails/?id=3067715093) | `vanillaracesexpanded.archon` |
| 582 | [Circinus Performance Analyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=3773680130) | `astryl.circinus` |
| 583 | [PAWN DEPTH ORDER](https://steamcommunity.com/sharedfiles/filedetails/?id=3839580086) | `local.pawndepth` |
| 584 | [Overlord Warcasket](https://steamcommunity.com/sharedfiles/filedetails/?id=3841078351) | `shunter.overlordwarcasket` |
| 585 | [Infestations Spawn in Darkness (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3551839891) | `koth.infestationsspawnindarkness` |
| 586 | [Flourish - Combat Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3793154978) | `astryl.flourish` |
| 587 | [Vanilla Outposts Expanded: Additional Outposts](https://steamcommunity.com/sharedfiles/filedetails/?id=2873841790) | `mrhydralisk.voeadditionaloutposts` |
| 588 | [Vanilla Outposts Expanded: Power Grid](https://steamcommunity.com/sharedfiles/filedetails/?id=2915686437) | `mrhydralisk.voepowergrid` |
| 589 | [Vanilla Outposts Expanded: Delivery Logistics](https://steamcommunity.com/sharedfiles/filedetails/?id=3006726393) | `mrhydralisk.voedeliverylogistics` |
| 590 | [Vanilla Outposts Expanded: Prisoner Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3002936071) | `mrhydralisk.voeprisonerpatch` |
| 591 | [Common Sense](https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193) | `avilmask.commonsense` |
| 592 | [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) | `neronix17.tweaksgalore` |
| 593 | [Vanilla Ideology Expanded - Memes and Structures](https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500) | `vanillaexpanded.vmemese` |
| 594 | [Auto-Cast Specialist Commands](https://steamcommunity.com/sharedfiles/filedetails/?id=3459647882) | `linnun.autocastspecialistcommands` |
| 595 | [Reel's Insector Faction](https://steamcommunity.com/sharedfiles/filedetails/?id=3309022698) | `reel.insectorfaction` |
| 596 | [Tastier Vanilla Clothes](https://steamcommunity.com/sharedfiles/filedetails/?id=2808554143) | `al9000.tvc` |
| 597 | [RimWorld: Living Waters](https://steamcommunity.com/sharedfiles/filedetails/?id=3834245888) | `patrickschwarzer.rivers` |
| 598 | [Alpha Mechs](https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158) | `sarg.alphamechs` |
| 599 | [[FSF] Complex Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=2069684319) | `frozensnowfox.complexjobs` |
| 600 | [Apex Mechanoids](https://steamcommunity.com/sharedfiles/filedetails/?id=3792325789) | `mortis.apexmechanoids` |
| 601 | [Linkable Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3659197870) | `linnun.linkablegroups` |
| 602 | [Enemy Mechanitors](https://steamcommunity.com/sharedfiles/filedetails/?id=3793204027) | `asunib.enemymechanitors` |
| 603 | [Mechanoid Caravans: Rewritten](https://steamcommunity.com/sharedfiles/filedetails/?id=3807991902) | `asunib.mechanoidcaravans` |
| 604 | [Rimsenal Xenotype Pack - Dioktri](https://steamcommunity.com/sharedfiles/filedetails/?id=3823256718) | `rimsenal.dioktri` |
| 605 | [Rimsenal Factions (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3530301107) | `bonible.rimsenalfactions` |
| 606 | [Show Me Your Hands · In Style!](https://steamcommunity.com/sharedfiles/filedetails/?id=3840308859) | `rhajiit.smyh.stylepositions` |
| 607 | [Rimsenal - Plants Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3842797428) | `rimsenal.plants` |
| 608 | [Catalog Tab - Test Fire, Compare & Craft](https://steamcommunity.com/sharedfiles/filedetails/?id=3840815505) | `doorajp.gearcatalog` |
| 609 | [Auto links](https://steamcommunity.com/sharedfiles/filedetails/?id=2059389912) | `automatic.autolinks` |
| 610 | [RimBridgeServer](https://steamcommunity.com/sharedfiles/filedetails/?id=3727949765) | `brrainz.rimbridgeserver` |
| 611 | [[LFS] UI Rethemed - Work](https://steamcommunity.com/sharedfiles/filedetails/?id=3835296643) | `lazyfridaystudio.uirethemed.work` |
| 612 | [[LFS] UI Rethemed - Info Card](https://steamcommunity.com/sharedfiles/filedetails/?id=3843294270) | `lazyfridaystudio.uirethemed.infocard` |
| 613 | [Weather Reimagined](https://steamcommunity.com/sharedfiles/filedetails/?id=3838111458) | `evileyeguy.weatherreimagined` |
| 614 | [RimWorld: Waves](https://steamcommunity.com/sharedfiles/filedetails/?id=3831078430) | `patrickschwarzer.waves` |
| 615 | [Hospitality + Ideology Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3789536584) | `fumblesneeze.guestbedgizmo` |
| 616 | [VV - New Harvest: Compatibility Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3570411438) | `octodecoy.vvnewharvest.compatpatches` |
| 617 | [[WYD] Better Campsites](https://steamcommunity.com/sharedfiles/filedetails/?id=3546818262) | `wyr3d.bettercampsites` |
| 618 | [Art on Neolithic Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2556397158) | `dizzy.neolithicweaponart` |
| 619 | [Balanced Eclipse and Solar Flare - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=2929407615) | `skelly.neceros.balancedeclipseandsolarflare.forked` |
| 620 | [Dimorphic Neanderthals](https://steamcommunity.com/sharedfiles/filedetails/?id=3612381568) | `zoid.femneanderthals` |
| 621 | [DLG.Mutations behind](https://steamcommunity.com/sharedfiles/filedetails/?id=3637525997) | `dlg.mutation.behind` |
| 622 | [High Life Drug Cooking Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=3183941628) | `bigtortoise.drugcookingspeed` |
| 623 | [Less Rebuff](https://steamcommunity.com/sharedfiles/filedetails/?id=774543761) | `dingo.lessrebuff` |
| 624 | [More Descriptive Words and Names](https://steamcommunity.com/sharedfiles/filedetails/?id=2180384246) | `hol.words` |
| 625 | [Cultural names: Stellic](https://steamcommunity.com/sharedfiles/filedetails/?id=3161247581) | `hawkwood.culturestellic` |
| 626 | [No Headstone Till Buried](https://steamcommunity.com/sharedfiles/filedetails/?id=2845934338) | `wanzi.noheadstone` |
| 627 | [Smaller radius for Anima Trees, Shrines and Animus Stones](https://steamcommunity.com/sharedfiles/filedetails/?id=2812513517) | `longman.smallerradiusforanimatreesandshrines` |
| 628 | [Soil Contrast](https://steamcommunity.com/sharedfiles/filedetails/?id=1561838700) | `wemd.soilcontrast` |
| 629 | [Det's Xenotypes for [NL] Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3565987465) | `det.facialanimation` |
| 630 | [Harana chirps quietly](https://steamcommunity.com/sharedfiles/filedetails/?id=3264370565) | `amegakull.haranachirpsquietly` |
| 631 | [Medieval Backstories Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3170653412) | `grasstypefire.medieval.backstoriespatch` |
| 632 | [Normal Stone for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=2968173847) | `mman0385.abnormalstone` |
| 633 | [Thumper - Phercore Descriptions](https://steamcommunity.com/sharedfiles/filedetails/?id=3497174635) | `bb.thumper.pherocore.desc.clarify` |
| 634 | [VE Helixien Gas Space Maps Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3262619470) | `ionfrigate12345.sos2vehelixienpatch` |
| 635 | [VFE - Empire: Frugal Nobles](https://steamcommunity.com/sharedfiles/filedetails/?id=3573280041) | `cn.vfeempirelessrequirement` |
| 636 | [VTE - Proxy Heat Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3439148119) | `ewolfy.vtepatch` |
| 637 | [[NL] Facial Animation - 1.7 Compatibility Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3821248395) | `local.facialanimation.compat17` |
| 638 | [Reduce Skipdoor Distortion Effects](https://steamcommunity.com/sharedfiles/filedetails/?id=3111751800) | `rabiosus.reduceskipdoordistortioneffect` |
| 639 | [Hospitality: Vending Machines (Adaptive Storage Patch)](https://steamcommunity.com/sharedfiles/filedetails/?id=3319084133) | `katsukiri.hvendingmachineadaptivepatch` |
| 640 | [ReGrowth 2 World Map Beautification for Alpha Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679302) | `noxilie.regrow.wmb.alphabiomes` |
| 641 | [ReGrowth 2 World Map Beautification for More Vanilla Biomes](https://steamcommunity.com/sharedfiles/filedetails/?id=3564679624) | `noxilie.regrow.wmb.morevanillabiomes` |
| 642 | [Simple Immersion - Regrowth Rimclaw Rename](https://steamcommunity.com/sharedfiles/filedetails/?id=3789160274) | `nyr.siregrowthrimclaw` |
| 643 | [Vanilla plus loot expirence for ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3536236647) | `bb.generic.loot.exp.anc.urban.ruins` |
| 644 | [VFE Pirates Warcaskets Vacuum Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3543210594) | `why.vfe.warcasketsvacuumpatch` |
| 645 | [Warcasket weaponry hyperlinks](https://steamcommunity.com/sharedfiles/filedetails/?id=2819075870) | `d0b1k.warcaskethyperlinks` |
| 646 | [Cyanobot's Plant Gen Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3218426325) | `cyanobot.plantgenpatch` |
| 647 | [Phoebe Is Just A Chill Gal \| Progression: Storytellers Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3530139996) | `koberiddle.phoebeisjustachillgal` |
| 648 | [Warcasket Weapon Quality](https://steamcommunity.com/sharedfiles/filedetails/?id=3766043780) | `danzinagri.warcasketweaponquality` |
| 649 | [Anomaly Fleshbeasts or Sarg Fleshbeasts?](https://steamcommunity.com/sharedfiles/filedetails/?id=3234422589) | `ferny.fleshbeasts` |
| 650 | [Muzzle Flash for Stellic Arsenal](https://steamcommunity.com/sharedfiles/filedetails/?id=3451641816) | `stellicarsenal.mf.nihao` |
| 651 | [Wood's Muzzle Flash for Ancient urban ruins](https://steamcommunity.com/sharedfiles/filedetails/?id=3442021825) | `charlie.muzzle.flash.for.ancientruins` |
| 652 | [Alpha Animals Patch for Outposts Mines](https://steamcommunity.com/sharedfiles/filedetails/?id=2704392053) | `daria40k.alphaanimalspatchoutposts` |
| 653 | [Vanilla Mining Outpost Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=2968491314) | `kuhlyus.modpatch.vanillaoutpost` |
| 654 | [The Vanity Project - Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3143138787) | `thevanityproject.beards` |
| 655 | [Better Trees: More Djeeshka Like Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3543705507) | `maal.bettertreesmod` |
| 656 | [D12 Dungeon and Night Hairs HD](https://steamcommunity.com/sharedfiles/filedetails/?id=3620959277) | `d12caedro.dynhairshd` |
| 657 | [Erin's Baldur's Gate 3 Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3069933015) | `erin.bg3.hair` |
| 658 | [Erin's Hairstyles - Redux](https://steamcommunity.com/sharedfiles/filedetails/?id=2361911135) | `erin.hairredux` |
| 659 | [Erin's Hairstyles 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2849477421) | `erin.hair2` |
| 660 | [Hard Times: Hair and Beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3092175321) | `botchjob.hthair` |
| 661 | [Nice Short Female Hairs](https://steamcommunity.com/sharedfiles/filedetails/?id=3316363751) | `andromeda.nicehairs` |
| 662 | [Pumpkin Hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=3783859385) | `pumpkin.pumpkinhairs2` |
| 663 | [Roo's HD Hairstyles (continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3559870636) | `rooboid.hdhair.continued` |
| 664 | [Rusty's Foliage Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=3646973576) | `rustys.foliage.hairs` |
| 665 | [Sin's Softer Hair Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=3790361865) | `sinner.softerhaircolors` |
| 666 | [Sovs hairs and beards](https://steamcommunity.com/sharedfiles/filedetails/?id=3368700799) | `sov.hairbeardandstuff` |
| 667 | [Vanilla Hair Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256) | `vanillaexpanded.vhe` |
| 668 | [Won hair_men](https://steamcommunity.com/sharedfiles/filedetails/?id=1879950403) | `won.hair` |
| 669 | [Won hair_women](https://steamcommunity.com/sharedfiles/filedetails/?id=1886150617) | `won2.hair` |
| 670 | [Xeva's Rimhair](https://steamcommunity.com/sharedfiles/filedetails/?id=705924057) | `orion.xevarimhair` |
| 671 | [AFU女士发型_Women's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2020779666) | `jintuzilamian.hairstyle.afuwomenshairstyles` |
| 672 | [Rimsenal - Hair pack](https://steamcommunity.com/sharedfiles/filedetails/?id=725949967) | `rimsenal.hair` |
| 673 | [Absolute Legends: Hair](https://steamcommunity.com/sharedfiles/filedetails/?id=2195744587) | `absolute.legends.hair` |
| 674 | [AFU男士发型_Men's hairstyles](https://steamcommunity.com/sharedfiles/filedetails/?id=2072106893) | `jintuzilamian.hairstyle.afumenshairstyles` |
| 675 | [Useless Hair (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3044216509) | `zal.uselesshair` |
| 676 | [Better Scroll Bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3579237751) | `dillyo.betterscrollbars` |
| 677 | [[CM] Ideology DLC Texture Upscale](https://steamcommunity.com/sharedfiles/filedetails/?id=2603637629) | `cm.ideology.upscaled` |
| 678 | [[sbz] Mini Band Node](https://steamcommunity.com/sharedfiles/filedetails/?id=3646711814) | `sbz.minibandnode` |
| 679 | [[sbz] Mini Mech Booster](https://steamcommunity.com/sharedfiles/filedetails/?id=3547537902) | `sbz.minimechbooster` |
| 680 | [[sbz] Mini Wastepack Atomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3668590852) | `sbz.miniwastepackatomizer` |
| 681 | [Childrens Drawings](https://steamcommunity.com/sharedfiles/filedetails/?id=2918817041) | `zylle.childrensdrawings` |
| 682 | [FEB's Colored Gravship Lights](https://steamcommunity.com/sharedfiles/filedetails/?id=3577698779) | `feb.coloredgravlights` |
| 683 | [Injury Texture Variety](https://steamcommunity.com/sharedfiles/filedetails/?id=3561610873) | `keshash.injurytextures` |
| 684 | [Prothesis Texture](https://steamcommunity.com/sharedfiles/filedetails/?id=3236905283) | `prothesis.texture` |
| 685 | [Vanilla Genes Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=2905707100) | `redmattis.vanillagenesrebalanced` |
| 686 | [Vanilla Hair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2748834409) | `neronix17.retexture.vanillahair` |
| 687 | [Vanilla Pawns Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=2275310562) | `neronix17.hd.pawns` |
| 688 | [EyeGenes3](https://steamcommunity.com/sharedfiles/filedetails/?id=3745223213) | `lucius.eyegenes3` |
| 689 | [Character Development](https://steamcommunity.com/sharedfiles/filedetails/?id=3770112044) | `ferny.characterdevelopment` |
| 690 | [Vanilla Experimentals for Facial Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=3753978140) | `sd.fa.movingvanillaeyes` |
| 691 | [Vanilla Upscaled Floors](https://steamcommunity.com/sharedfiles/filedetails/?id=3524080368) | `adagio.vanilla.upscaled.floors` |
| 692 | [Vanilla Upscaled Terrain](https://steamcommunity.com/sharedfiles/filedetails/?id=3525395044) | `adagio.vanilla.upscaled.terrain` |
| 693 | [World Map Enhanced (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3599967849) | `zal.worldmapenhanced` |
| 694 | [[CF] VRE - Lycanthrope Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3578135470) | `cf.lycanthrope` |
| 695 | [[CF] VRE - Sanguophage Better Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3165181669) | `vres.betterparts` |
| 696 | [[CF] VRE - Saurid Better Head Parts](https://steamcommunity.com/sharedfiles/filedetails/?id=3539916610) | `cf.vresaurid` |
| 697 | [Japester's Upscaled Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3788774863) | `japester.upscaledtextures.4x` |
| 698 | [Nice Hands Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3302328100) | `andromeda.nicehands` |
| 699 | [Optional Icons for Architect Icons](https://steamcommunity.com/sharedfiles/filedetails/?id=1966995052) | `proxyer.optionalicons4ai` |
| 700 | [Pumpkinlass Visible Pants Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3772689105) | `pumpkinlass.pantsretexture` |
| 701 | [Vanilla Textures Expanded - Variations](https://steamcommunity.com/sharedfiles/filedetails/?id=2493234474) | `vanillaexpanded.vtexvariations` |
| 702 | [ReBuild: Doors and Corners](https://steamcommunity.com/sharedfiles/filedetails/?id=3262718980) | `rebuild.cotr.doorsandcorners` |
| 703 | [Shelf Stack Logistics Reworked](https://steamcommunity.com/sharedfiles/filedetails/?id=3724884785) | `redmattis.shelflogistics` |
| 704 | [Loading Screen Repository](https://steamcommunity.com/sharedfiles/filedetails/?id=3567860027) | `ferny.loadingscreenrepository` |
| 705 | [Mechanitor Encounters](https://steamcommunity.com/sharedfiles/filedetails/?id=3417287863) | `makinglovetomy.warqueenmechwife.allnightlong` |
| 706 | [MSS Tweaks and Fun](https://steamcommunity.com/sharedfiles/filedetails/?id=3379574408) | `mss.flavourpack` |
| 707 | [[HRK] Vanilla Expanded Extra Embrasures](https://steamcommunity.com/sharedfiles/filedetails/?id=2763411067) | `harkon.veaddon.vanillaexpandedextraembrasures` |
| 708 | [[FSF] FrozenSnowFox Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2893432492) | `frozensnowfox.frozensnowfoxtweaks` |
| 709 | [Better Architect Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3563882422) | `ferny.betterarchitect` |
| 710 | [ReBuild: Cell Door Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3598547615) | `rebuild.celldoorfix` |
| 711 | [Too Many Mods - Compats and Rebalances](https://steamcommunity.com/sharedfiles/filedetails/?id=3250762483) | `wara.toomanymods` |
| 712 | [Temporary Patches](https://steamcommunity.com/sharedfiles/filedetails/?id=3424105991) | `ferny.temporarypatches` |
| 713 | [Wild Crops, a fork of Wild Spawn](https://steamcommunity.com/sharedfiles/filedetails/?id=3776658466) | `somethingsomething.plantsandstuff` |
| 714 | [VFE - Settlers textures revamped](https://steamcommunity.com/sharedfiles/filedetails/?id=3685232860) | `regen.vfestexturesrevamped` |
| 715 | [Xeva's Rimhair Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3132223992) | `neronix17.retexture.xevarimhair` |
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
| 730 | [ATH's Retexture Female Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3145326932) | `anthitei.athsretexturefemalapparel.retexture` |
| 731 | [Biotech Mechanoid Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3164022710) | `el.biotechmechrt` |
| 732 | [Erin's Body Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2662457442) | `erin.body.texture` |
| 733 | [Gerrymon's Upscaled Vanilla Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3276562906) | `gerrymon.uvt` |
| 734 | [Gerrymon's Misc Props: Graveyard](https://steamcommunity.com/sharedfiles/filedetails/?id=3780458386) | `gmmp.graveyard` |
| 735 | [Sexier Visage Masks](https://steamcommunity.com/sharedfiles/filedetails/?id=3638159640) | `gerrymon.sexymasks` |
| 736 | [Mechanoid Cluster Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3523231836) | `happycam.conditioncauserretexture` |
| 737 | [Van's Retexture : Mechanitor](https://steamcommunity.com/sharedfiles/filedetails/?id=2943977908) | `sirvan.mechanitorretexture` |
| 738 | [Van's Retexture : Melee Weapons](https://steamcommunity.com/sharedfiles/filedetails/?id=2922441211) | `sirvan.mwretextured` |
| 739 | [Van's Retexture : Steel](https://steamcommunity.com/sharedfiles/filedetails/?id=2955560866) | `sirvan.steelretexture` |
| 740 | [Vanilla Mechanoid Slag Chunks Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3228786091) | `auntydonna.mechanoidslagchunksretextured` |
| 741 | [VFE Pirates Custom Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3755883834) | `yourmother.vfepirates.retexture` |
| 742 | [Food Texture Variety - Babies](https://steamcommunity.com/sharedfiles/filedetails/?id=3366505975) | `goat.food.texture.variety.babies` |
| 743 | [Food Texture Variety - Beer](https://steamcommunity.com/sharedfiles/filedetails/?id=3361892196) | `goat.food.texture.variety.beer` |
| 744 | [Food Texture Variety - Chocolate](https://steamcommunity.com/sharedfiles/filedetails/?id=3362958345) | `goat.food.texture.variety.chocolate` |
| 745 | [Food Texture Variety - Nutrient Paste](https://steamcommunity.com/sharedfiles/filedetails/?id=3357299556) | `goat.food.texture.variety.paste` |
| 746 | [Food Texture Variety - Replacement](https://steamcommunity.com/sharedfiles/filedetails/?id=3366513037) | `goat.food.texture.variety.replacement` |
| 747 | [Food Texture Variety - Vanilla Expanded Cooking](https://steamcommunity.com/sharedfiles/filedetails/?id=3388883044) | `goat.food.texture.variety.vecooking` |
| 748 | [H.C_AnimalReskins_Complete2 (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3600202032) | `hc.animalreskins.complete2` |
| 749 | [Pointed Elf Ears Refined](https://steamcommunity.com/sharedfiles/filedetails/?id=2886966525) | `hysila.refinedelfears` |
| 750 | [VFME - Caravan Packs!](https://steamcommunity.com/sharedfiles/filedetails/?id=3609057140) | `vfme.caravanpacks` |
| 751 | [Alpha Animals Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3536598972) | `ks.aaretextured` |
| 752 | [Charge Lance Restyle](https://steamcommunity.com/sharedfiles/filedetails/?id=3697662862) | `regen.chargelancerestyle` |
| 753 | [UI Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2978831421) | `katsudon.uiretexture` |
| 754 | [Consistent Textures](https://steamcommunity.com/sharedfiles/filedetails/?id=3586064345) | `ferny.consistenttextures` |
| 755 | [Van's Retexture : Organ jars](https://steamcommunity.com/sharedfiles/filedetails/?id=3541295970) | `sirvan.organretexture` |
| 756 | [Visual Progression: Van's Organ Jars](https://steamcommunity.com/sharedfiles/filedetails/?id=3838085030) | `groovy.visualprogression.vansorganjars` |
| 757 | [Toymaker's Additional Outposts Retextured](https://steamcommunity.com/sharedfiles/filedetails/?id=3020739638) | `toymaker.voe.ao.retextured` |
| 758 | [[sbz] Retexture Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2848805038) | `jgh.retexturepack` |
| 759 | [Pacing Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3613142246) | `ferny.pacingmanager` |
| 760 | [Float Menu Performance Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3784467506) | `vorsel.floatmenuperformanceoptimizer` |
| 761 | [Save Your Storage and Tick Every Minute](https://steamcommunity.com/sharedfiles/filedetails/?id=3795549633) | `nanaloveyuki.saveyourstorageandtickeveryminute` |
| 762 | [Architect Menu Optimizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3697920753) | `mrk.architectmenuoptimizer` |
| 763 | [Facial Animation Performance Patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3790129900) | `astryl.facialanimationperformancepatch` |
| 764 | [Dubs Performance Analyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=2038874626) | `dubwise.dubsperformanceanalyzer.steam` |
| 765 | [Consistent Text](https://steamcommunity.com/sharedfiles/filedetails/?id=3578462306) | `ferny.consistenttext` |
| 766 | [Local Tweaks (sammael)](https://github.com/hugalafutro/LocalTweaks) | `sammael.localtweaks` |
<!-- MODLIST:END -->
