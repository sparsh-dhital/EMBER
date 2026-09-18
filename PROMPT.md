# PROJECT EMBER — FULL GAME POLISH + GAMEPLAY IMPLEMENTATION

You are the lead Unity gameplay engineer, technical designer, level designer, and polish engineer working directly on my existing Unity project through the connected Unity MCP.

I am a beginner at Unity and already have a basic version of this game working. DO NOT assume the project should be rebuilt from scratch.

Your first responsibility is to understand the existing project, identify what is already useful, preserve it where practical, and then transform it into a polished, functional, atmospheric 3D survival game.

The target is NOT a large complicated survival game.

The target is:

**A small, beautiful, tense, highly replayable 3D survival game with a very clear gameplay loop:**

> Explore the darkness → manage lantern fuel → find fuel → collect radio parts → survive vampires → find the sacred locket → use prayer when the flame dies → return to the radio centre → call for help → escape alive.

The game should feel intentionally designed by a human game-development team, not like a collection of generic AI-generated mechanics.

---

# 1. IMPORTANT WORKING RULES

Before changing anything:

1. Inspect the entire Unity project using the available MCP tools.
2. Inspect the active scenes, hierarchy, existing scripts, prefabs, materials, lights, cameras, input setup, packages and build settings.
3. Inspect the Unity Console and identify existing errors/warnings.
4. Determine which parts of the current prototype can be reused.
5. Do not delete or replace functional systems just because you would architect them differently.
6. Do not destroy my existing scene unless absolutely necessary.
7. Keep the project compatible with Unity 6.
8. Use the New Input System where available.
9. Keep Android and Web builds in mind from the beginning.
10. Use MCP to inspect and modify the Unity project wherever the connected MCP supports it.
11. After each major implementation phase, compile/check the Unity Console and fix errors before continuing.
12. Do not create a huge interconnected architecture all at once. Implement in logical milestones and validate each milestone.
13. Prefer simple, reliable systems over clever or unnecessarily complex systems.
14. Do not introduce expensive systems such as complex behaviour trees, procedural world generation, multiplayer, inventories, crafting trees, multiple biomes, or unnecessary RPG systems.
15. If an asset or feature is unavailable, use a clean placeholder or primitive and continue rather than blocking the build.
16. Do not manually modify Unity `.meta` files.
17. Do not touch `Library`, `Temp`, `Logs`, or generated build folders unnecessarily.
18. Keep source code inside a clean `Assets/Scripts/` structure.
19. Preserve Git/version-control safety.
20. Never leave the project in a state where the Console contains unresolved compile errors.

When a design decision is required, choose the simplest implementation that produces the strongest player experience.

---

# 2. GAME VISION

Create a dark atmospheric 3D survival game.

The player controls a human character exploring a dangerous abandoned environment at night.

The character carries a physical lantern.

The lantern is the central survival mechanic.

The lantern has fuel.

Fuel continuously decreases.

As fuel decreases:

* lantern intensity decreases
* lantern radius decreases
* darkness becomes more threatening
* vampires become increasingly aggressive
* atmosphere becomes more stressful
* heartbeat/audio tension increases
* the player becomes increasingly motivated to search for fuel

The player must search the environment for fuel pickups while simultaneously collecting several radio components required to request rescue.

The player must also find a sacred locket.

The locket unlocks a one-time emergency prayer ability.

When the lantern reaches zero:

* the flame goes out
* the environment becomes extremely dark
* vampires stop fearing the lantern
* vampires become highly aggressive
* the player can use the prayer ability only if the locket has already been collected
* prayer creates temporary supernatural protection for approximately 60 seconds
* this protection does not restore fuel
* it buys the player enough time to complete the objective and reach the radio centre

The final objective is:

**Collect all required radio parts → return to the designated radio centre → repair/activate the radio → call for help → survive the final sequence.**

The game should feel tense but fair.

The player should always understand:

* what they need to collect
* how much fuel remains
* whether vampires are becoming dangerous
* where they are going
* what happens when the lantern dies

---

# 3. FIRST-PERSON/THIRD-PERSON PRESENTATION

Use a polished third-person 3D presentation unless the existing project already has a strong first-person implementation that is worth preserving.

Preferred camera:

* over-the-shoulder / third-person
* camera positioned behind and slightly above the character
* smooth follow
* subtle camera damping
* gentle rotation smoothing
* no harsh snapping
* modest cinematic FOV
* character should remain visible at all times
* lantern must remain visually prominent

Use Cinemachine if it is already installed or can be safely used.

Do not make the camera feel like a default Unity template.

---

# 4. PLAYER CHARACTER

Use a proper 3D humanoid character model.

The character must have:

* idle animation
* walk animation
* run animation
* turning animation/transition
* hit/damage reaction
* death animation
* prayer animation
* lantern-carrying pose
* natural upper-body movement
* proper Animator Controller

Prefer Mixamo-compatible animations or existing properly rigged assets.

The character must physically hold the lantern in the correct hand.

The lantern must move naturally with the character's hand/body animation.

Do not simply float the lantern beside the character.

The lantern should visually cast light from its actual position.

---

# 5. PLAYER PHYSICS AND MOVEMENT

Implement genuine 3D movement and gravity.

The player must:

* remain grounded correctly
* fall when walking off edges
* collide with physical environment
* respond correctly to slopes/stairs
* not float
* not slide uncontrollably
* not walk through walls
* not become stuck easily

Use Unity's physics/collision systems appropriately.

Use a reliable character controller architecture rather than a fake transform-only movement system.

Gravity should be based on Unity physics/gravity values, not simply changing transform Y positions.

Implement ground checks.

Implement slope handling.

Implement sensible movement acceleration/deceleration.

Avoid excessive movement inertia.

Target movement should feel responsive on both keyboard and touchscreen.

---

# 6. CONTROLS

## Android

Implement:

* left virtual joystick = movement
* right-side drag/swipe = camera rotation
* contextual interact button
* contextual prayer button
* optional sprint button only if it improves the existing game without adding another resource-management system

Controls must respect Android safe areas.

UI must work on different aspect ratios.

Do not make buttons enormous and ugly.

Use clean, minimal, translucent controls.

## Web/Desktop

Implement:

* WASD / arrow keys = movement
* mouse movement/drag = camera
* E = interact
* P or Space = prayer when available
* Esc = pause

All control logic must use the same underlying input abstraction so Android and Web do not require duplicated gameplay code.

---

# 7. LANTERN + FUEL SYSTEM

This is the most important system in the entire game.

Create a dedicated lantern/fuel system.

Recommended baseline:

* max fuel = 100
* fuel starts full
* fuel continuously drains
* expose drain rates in Inspector
* use an AnimationCurve or clearly exposed tuning parameters for fuel-to-light behaviour

The player should always have a visible fuel counter.

Recommended behaviour:

100–60% fuel:

* strong warm light
* good visibility
* vampires strongly avoid the player
* calm atmosphere

60–30% fuel:

* noticeably weaker light
* slightly reduced radius
* occasional subtle flicker
* vampires become more willing to approach
* heartbeat becomes more noticeable

30–10% fuel:

* very dim light
* stronger flicker
* much smaller visibility radius
* vampires begin actively stalking/approaching
* heartbeat becomes faster
* audio becomes tense

10–0%:

* extremely weak light
* intense flicker
* very small radius
* vampires become highly aggressive
* warning audio
* visual urgency

0%:

* lantern extinguishes
* lantern light turns off
* emergency state begins

Never allow fuel to visually display impossible negative values.

Clamp fuel correctly.

Fuel pickups should restore a meaningful amount without instantly trivializing the game.

Add diminishing visual/audio tension immediately when fuel is collected.

---

# 8. LANTERN VISUAL QUALITY

The lantern must look like a real physical game object.

Implement:

* warm point light
* flame visual
* subtle particle effect
* light flicker
* light intensity based on fuel
* light radius based on fuel
* subtle flicker speed increase at low fuel
* subtle emissive glow
* believable interaction between lantern and environment

Do not make the lantern look like a generic glowing sphere.

Prefer a physical lantern model with:

* glass
* metal frame
* visible flame
* subtle emissive material

When fuel is low, the flame should visibly become smaller/weaker.

When fuel reaches zero, the flame should disappear.

The light should feel like the player's only real source of safety.

---

# 9. FUEL PICKUPS

Create fuel pickups distributed around the level.

Each fuel pickup should:

* be physically placed in the world
* have a recognizable silhouette
* have a subtle visual glow
* have subtle idle animation
* trigger pickup sound
* trigger small particle burst
* increase fuel
* disappear/disable after pickup

Do not spam pickups everywhere.

Fuel placement should create meaningful movement decisions.

Some fuel should be relatively safe.

Some should require the player to leave the comfort of the centre.

Some should be near radio components.

Never spawn a vampire directly on top of a pickup or directly on the player.

---

# 10. RADIO MISSION

Add a proper mission objective rather than making the game only about surviving.

Create approximately 5 radio parts.

Use a counter such as:

`RADIO PARTS 0/5`

Each radio part should be placed in a distinct environmental location.

Examples:

* abandoned cabin
* ruined structure
* broken vehicle
* watch post
* cave entrance
* forest clearing
* old equipment site

Do not create multiple huge environments.

Use one compact but visually rich level.

The radio parts should have subtle clues:

* faint electronic beeping
* blinking indicator
* subtle visual effect
* audio becomes stronger when approaching

Avoid giant glowing markers that destroy immersion.

The player should feel like they are actually searching.

---

# 11. RADIO CENTRE / EXTRACTION OBJECTIVE

Create one clearly recognizable central communication point.

Possible visual:

* old radio tower
* emergency communication station
* radio cabin
* generator + antenna
* abandoned field communications centre

This location should be visually memorable.

The player begins near it, leaves to collect the required parts, then eventually returns.

Before all parts are collected:

* radio is inactive
* interaction explains that parts are missing

After all parts are collected:

* radio becomes active
* objective changes to return to the centre
* centre gets subtle visual/audio activation
* final interaction becomes available

The player must return physically to the centre.

Do not complete the mission remotely through UI.

Once the player reaches the centre:

* show interact prompt
* player holds/interacts with radio
* short repair/call sequence plays
* radio static begins
* signal sound builds
* player calls for rescue

Then trigger a cinematic but lightweight survival ending:

* radio transmission
* signal/flare/communication effect
* environmental audio shifts
* sunrise or rescue implication
* victory screen

The ending should feel earned.

---

# 12. VAMPIRE SYSTEM

Only one vampire enemy type is required.

Make that one enemy feel polished.

The vampire should be a clearly visible red/dark-crimson 3D creature.

Visual direction:

* dark red body/material
* stronger red accents around face/eyes
* subtle emissive red eyes
* distinctive silhouette
* threatening but readable
* not cartoonish
* not overly detailed
* optimized for mobile

The player must be able to see vampires.

Do not make them completely invisible.

---

# 13. VAMPIRE AI STATE MACHINE

Build a simple, reliable state machine.

States:

### IDLE / HIDDEN

Used when far from the player or outside meaningful engagement range.

### FLEE

When the lantern is strong, vampires are afraid.

They should:

* retreat from the player
* avoid the lantern
* maintain distance
* move away rather than simply freeze

### STALK / APPROACH

When fuel/light becomes weaker:

* stop fleeing
* slowly approach
* circle or reposition where appropriate
* remain threatening

### AGGRESSIVE

When fuel becomes critically low:

* move faster
* pursue player
* shorten attack distance
* become much more dangerous

### ATTACK

When close enough:

* play attack animation
* play attack sound
* briefly slow/lock movement appropriately
* damage or kill the player depending on the final tuned combat model

Prefer a simple attack wind-up and cooldown rather than rapid instant damage every frame.

---

# 14. LIGHT-BASED VAMPIRE BEHAVIOUR

This interaction is critical.

The vampires should respond to the actual lantern state, not simply to an arbitrary timer.

Use the lantern's normalized intensity/fuel state.

Example tuning:

Strong light:
`fuel > 55%`

Vampires strongly flee.

Medium light:
`25%–55%`

Vampires stalk and slowly approach.

Critical light:
`< 25%`

Vampires aggressively approach.

Fuel = 0:

Vampires become highly aggressive.

Use hysteresis or sensible state thresholds so vampires do not rapidly switch between FLEE and APPROACH every frame.

The reaction should be visibly understandable to the player.

The player should be able to observe:

"Bright lantern = vampires run away."

"Weak lantern = vampires approach."

This is the signature gameplay mechanic.

---

# 15. VAMPIRE SPAWNING

Create a controlled enemy director/spawner.

Do not randomly instantiate unlimited enemies.

Spawn enemies:

* outside the player's current effective light radius
* outside the immediate safe area
* not directly in the player's camera
* not directly on pickups
* not directly inside geometry

Enemy count should scale gradually with:

* time survived
* fuel level
* mission progress

Example:

Early game:
1–2 enemies

Middle:
2–4 enemies

Late game:
3–6 enemies

Keep a hard maximum suitable for mobile performance.

Use object pooling if practical.

The goal is tension, not enemy spam.

---

# 16. PRAYER / LOCKET MECHANIC

Add a collectible sacred locket.

The locket must be physically placed somewhere in the level and must be collected before the prayer mechanic is available.

Before collecting it:

`PRAYER LOCKED`

After collecting it:

`PRAYER READY`

Prayer can be used only once per run.

Important gameplay rule:

Prayer becomes available only after fuel reaches zero.

When the player activates prayer:

* play prayer animation
* temporarily prevent normal movement if necessary during the activation
* display a luminous Christian cross / Jesus-themed symbol above or near the character
* emit strong warm/holy light
* create a clear supernatural visual effect
* significantly increase the vampire fear radius
* vampires flee from the player
* prayer does NOT restore lantern fuel
* prayer lasts approximately 60 seconds
* show a subtle countdown

The symbol should look beautiful and respectful, not like a random emoji.

The effect should be recognizable immediately.

Vampires should visibly react to it.

During prayer protection, vampires should retreat aggressively.

When the 60 seconds end:

* symbol fades
* supernatural light fades
* protection ends
* vampires become dangerous again

The locket is one-use.

This mechanic should create a dramatic final escape opportunity.

---

# 17. IMPORTANT WIN/LOSE LOGIC

Create a clear GameManager state system.

States should include at least:

* Main Menu
* Playing
* Paused
* Prayer Emergency
* Victory
* Defeat

WIN:

1. Collect every required radio part.
2. Return to radio centre.
3. Complete radio call interaction.
4. Survive the final activation sequence.
5. Show victory.

LOSE:

* player is killed by vampires
* prayer expires while the player remains unable to complete survival objective
* player is overwhelmed by the final danger state

Avoid arbitrary instant death that players cannot understand.

Every death should feel caused by the player's decisions.

---

# 18. GAMEPLAY ENGAGEMENT

Do NOT add dozens of mechanics.

Instead improve engagement through pacing.

The intended loop is:

START SAFE
↓
Explore
↓
Find first fuel
↓
Find first radio part
↓
See vampire flee from lantern
↓
Continue deeper
↓
Fuel becomes weaker
↓
Vampires begin approaching
↓
Search for fuel
↓
Collect more radio parts
↓
Find locket
↓
Fuel becomes critical
↓
Vampires become aggressive
↓
Lantern dies
↓
Prayer emergency
↓
60-second escape
↓
Reach radio centre
↓
Call for help
↓
SURVIVE

This progression should create escalation without adding unnecessary systems.

---

# 19. LEVEL DESIGN

Keep the world compact.

Use one main map.

Target approximately 50x50 to 60x60 metres unless the current prototype already has a suitable layout.

The level should have:

* central radio centre
* branching paths
* recognizable landmarks
* open area
* narrow area
* one or two risky areas
* several environmental silhouettes
* fuel pickup locations
* radio part locations
* locket location
* believable navigation

Do NOT make a giant empty map.

Do NOT make everything perfectly symmetrical.

Use visual landmarks so players can remember where they are.

The darkness should hide imperfections while lighting and composition make the important locations attractive.

---

# 20. VISUAL DIRECTION

Make this look like a small polished indie horror/survival game.

Overall visual direction:

* dark
* atmospheric
* cinematic
* grounded
* mysterious
* slightly supernatural
* realistic enough to feel immersive
* stylized enough to remain achievable by a small beginner team

Avoid:

* excessive neon
* excessive UI
* generic "AI futuristic" styling
* giant floating labels
* random glowing objects everywhere
* oversaturated colours
* excessive particles
* huge unnecessary HUD elements

Use darkness intentionally.

Use:

* URP
* fog
* subtle bloom
* vignette
* good shadowing
* strong lantern contrast
* moonlight/rim lighting
* silhouettes
* ambient darkness
* controlled colour grading

The player's lantern should become the visual focus.

---

# 21. ATMOSPHERE

Make the environment feel alive.

Add subtle:

* wind
* fog movement
* trees/foliage movement where available
* ambient particles
* distant sounds
* radio static
* insect sounds
* footsteps
* lantern crackle
* vampire vocalizations
* heartbeat

Do not fill every second with sound.

Silence should also be used as tension.

---

# 22. AUDIO DESIGN

Create a simple AudioManager.

Required:

* footsteps
* fuel pickup
* radio pickup
* locket pickup
* interaction
* low fuel warning
* lantern extinguish
* vampire proximity
* vampire attack
* player damage/death
* prayer activation
* prayer countdown warning
* radio transmission
* victory
* defeat
* ambient loop

Heartbeat behaviour:

High fuel:
slow/subtle

Low fuel:
faster

Critical fuel:
stronger/faster

Prayer:
heartbeat should change to a more intense but hopeful soundscape

Use spatial audio where appropriate.

---

# 23. UI

Do not create a giant HUD.

Primary HUD elements:

Top/side:
`FUEL: 72%`

Mission:
`RADIO PARTS 3/5`

Optional objective:
`Find the remaining radio parts`

When all parts are collected:
`RETURN TO THE RADIO CENTRE`

When locket is found:
`PRAYER READY`

When fuel reaches zero:
large but tasteful:
`THE FLAME IS OUT`

Then:
`PRAY — 60`

Prayer countdown should be clear but not visually dominant.

Use smooth transitions and subtle animation.

No health-bar-heavy RPG interface.

The lantern/fuel system should remain the primary visual metaphor.

---

# 24. GAME FEEL

Everything should have feedback.

When collecting fuel:

* pickup sound
* small light burst
* UI number animation
* lantern flame briefly strengthens
* subtle controller/mobile feedback where supported

When fuel becomes low:

* heartbeat
* subtle vignette
* flicker
* audio tension

When vampire attacks:

* hit reaction
* sound
* camera impact
* brief screen effect

When prayer starts:

* camera subtle push-in
* symbol appears
* sound swell
* vampires react immediately

When winning:

* music/audio release
* environment becomes calmer
* clear success screen

Use tweening/interpolation for UI and effects where available.

---

# 25. CODE ARCHITECTURE

Keep systems separated and beginner-readable.

Use classes similar to:

`GameManager`
`PlayerController`
`PlayerInteractor`
`LanternFuelSystem`
`LanternVisualController`
`FuelPickup`
`RadioPart`
`RadioMissionSystem`
`RadioCentre`
`LocketPickup`
`PrayerSystem`
`VampireAI`
`VampireSpawner`
`AudioManager`
`HUDController`
`CameraController`

Do not put the entire game into one giant script.

Use serialized Inspector fields for tuning.

Avoid excessive singletons.

Use clear events for major gameplay changes such as:

* FuelChanged
* FuelEmpty
* RadioPartCollected
* LocketCollected
* PrayerStarted
* PrayerEnded
* MissionCompleted
* PlayerDied
* PlayerWon

Use simple code that a beginner can understand later.

Add comments only where they actually help.

---

# 26. SAVE / SCORE

Implement a lightweight run score if practical.

Possible score:

* survival time
* radio parts
* completion bonus
* fuel efficiency

A simple best survival time/high score system is enough.

Do NOT create a large save system.

---

# 27. PERFORMANCE

The game must target mobile performance.

Priorities:

* limited real-time lights
* one main dynamic lantern light
* reasonable shadow settings
* optimized materials
* low/medium polygon assets
* LODs where useful
* limited particle counts
* pooled vampires
* avoid expensive Update calls where possible
* avoid unnecessary physics checks
* avoid dozens of dynamic objects
* avoid excessive post-processing

Target a stable experience on a mid-range Android device.

Web build should remain playable.

Use efficient collision layers.

Do not make every object interact with every other object.

---

# 28. ANDROID + WEB REQUIREMENTS

The game must support:

Android:

* touch joystick
* touch camera
* interaction button
* prayer button
* landscape orientation
* responsive UI
* safe-area handling

Web/Desktop:

* keyboard movement
* mouse camera
* interact
* prayer
* pause

Do not create separate gameplay logic for each platform.

Only input/output presentation should differ.

---

# 29. UI RESPONSIVENESS

Test:

* 16:9
* 18:9
* tall Android aspect ratios
* desktop browser window

The HUD must not overlap.

Touch controls must not block important gameplay UI.

Keep controls near screen edges and HUD away from thumb zones.

---

# 30. MISSION MARKERS

Do not add a huge minimap.

Prefer:

* subtle directional indicator
* audio clues
* environmental landmarks
* minimal objective text

The game should encourage exploration rather than turning into a waypoint simulator.

---

# 31. START SCREEN

Create a polished but simple title screen.

Suggested presentation:

`EMBER`

Subtitle:

`KEEP THE LIGHT ALIVE`

Buttons:

`START`
`HOW TO PLAY`
`QUIT / EXIT`

Keep the menu atmospheric.

Use the same visual language as the actual game.

---

# 32. HOW TO PLAY

A very short tutorial.

Only teach the essentials:

`Your lantern protects you.`

`Find fuel before the flame dies.`

`Vampires fear strong light.`

`Find the radio parts.`

`Find the locket.`

`When the flame dies, prayer gives you one final chance.`

`Return to the radio centre and call for help.`

Do not create a long tutorial level.

The player should understand the game within the first 30 seconds.

---

# 33. DIFFICULTY CURVE

Start forgiving.

First minute:

* strong lantern
* few vampires
* nearby fuel
* easy radio part

Middle:

* weaker fuel
* larger exploration
* vampire pressure

Late:

* low fuel
* aggressive vampires
* more risk
* remaining radio parts harder to reach

Final:

* emergency prayer
* return to centre
* final tension sequence

The player should feel that the world is progressively closing in.

---

# 34. FAIRNESS RULES

Never:

* spawn a vampire directly next to the player
* place required fuel in an impossible location
* hide required mission items completely
* force the player into unavoidable damage
* make the prayer system unclear
* kill the player without understandable cause
* make the final objective impossible within the prayer window

Always give the player enough information to make a meaningful decision.

---

# 35. MCP IMPLEMENTATION PROCESS

Use this exact sequence.

## PHASE A — AUDIT

Inspect:

* project structure
* scene
* scripts
* prefabs
* player
* camera
* lighting
* input
* existing UI
* existing enemy
* existing pickups
* packages
* build settings
* console

Then produce a concise internal implementation plan based on what actually exists.

Do not immediately delete anything.

## PHASE B — CORE GAMEPLAY

Implement/fix:

1. player movement
2. gravity/collisions
3. camera
4. lantern fuel
5. dynamic light
6. fuel pickups

Test.

## PHASE C — VAMPIRES

Implement:

1. vampire prefab
2. movement
3. state machine
4. light response
5. attack
6. spawning

Test.

## PHASE D — MISSION

Implement:

1. radio parts
2. counter
3. radio centre
4. objective progression
5. victory state

Test.

## PHASE E — LOCKET + PRAYER

Implement:

1. locket pickup
2. unlock
3. fuel-zero state
4. prayer input
5. Jesus/cross visual
6. supernatural light
7. vampire repulsion
8. 60-second countdown
9. expiry logic

Test.

## PHASE F — POLISH

Then implement:

* animations
* particles
* audio
* UI transitions
* fog
* lighting
* camera feel
* environmental details
* screen effects

## PHASE G — OPTIMIZATION

Profile and reduce unnecessary:

* draw calls
* lights
* particles
* physics interactions
* enemy count
* expensive scripts

## PHASE H — BUILD VALIDATION

Validate:

* Unity Editor
* Android
* Web

Fix all critical errors before considering the task complete.

---

# 36. ACCEPTANCE TESTS

The implementation is not complete until all of these work.

TEST 1:
Player can move using keyboard.

TEST 2:
Player can move using Android joystick.

TEST 3:
Camera can rotate smoothly.

TEST 4:
Player correctly interacts with ground, slopes, gravity and collisions.

TEST 5:
Lantern is physically attached to the character's hand.

TEST 6:
Fuel drains continuously.

TEST 7:
Lantern intensity changes with fuel.

TEST 8:
Lantern radius changes with fuel.

TEST 9:
Fuel pickup increases fuel.

TEST 10:
Radio part pickup increments the mission counter.

TEST 11:
All radio parts are required before extraction.

TEST 12:
Locket pickup unlocks prayer.

TEST 13:
Prayer cannot be activated before fuel reaches zero.

TEST 14:
Fuel reaching zero extinguishes the lantern.

TEST 15:
Vampires flee when the lantern is strong.

TEST 16:
Vampires approach when the lantern is weak.

TEST 17:
Vampires become aggressive when the lantern is extinguished.

TEST 18:
Vampires can attack/kill the player.

TEST 19:
Prayer causes vampires to retreat.

TEST 20:
Prayer lasts approximately 60 seconds.

TEST 21:
Prayer can only be used once.

TEST 22:
Collecting all radio parts changes the mission objective.

TEST 23:
Player must physically return to the radio centre.

TEST 24:
Radio interaction triggers the final call.

TEST 25:
Successful call triggers victory.

TEST 26:
Death triggers a proper defeat/restart flow.

TEST 27:
Pause works.

TEST 28:
No compile errors exist.

TEST 29:
No obvious missing references.

TEST 30:
Game remains playable on a mid-range Android device.

TEST 31:
Web controls work.

TEST 32:
UI remains usable on different aspect ratios.

---

# 37. QUALITY BAR

Before calling the project finished, ask:

Does the game feel like a coherent game?

Does the lantern actually feel important?

Can I immediately understand why I need fuel?

Can I visually understand vampire behaviour?

Does low fuel create genuine tension?

Does finding the locket feel meaningful?

Does prayer feel like an emergency ability rather than a random power-up?

Do radio parts give the player a reason to explore?

Does returning to the radio centre feel like a real objective?

Does the final minute feel more intense than the opening minute?

Does the environment look good even though the team is small?

Does the game feel polished rather than feature-heavy?

Does the first 30 seconds communicate the core mechanic without requiring a tutorial wall?

---

# 38. CRITICAL SCOPE RULE

Do not add features merely because they sound interesting.

Do NOT add:

* crafting trees
* inventory grids
* hunger
* thirst
* temperature
* multiple enemy classes
* multiplayer
* weapons
* procedural generation
* large open world
* multiple biomes
* complex dialogue systems
* branching story
* complicated skill trees
* complicated combat
* unnecessary shops
* unnecessary quests

The core game must remain:

**LIGHT + FUEL + EXPLORATION + RADIO PARTS + VAMPIRES + LOCKET + PRAYER + ESCAPE**

That is the game.

Polish those mechanics heavily instead of expanding the feature list.

---

# 39. IMPORTANT BEGINNER-FRIENDLINESS RULE

When implementing anything, prefer:

* readable C#
* Inspector-configurable values
* small components
* clear naming
* minimal dependencies
* simple state machines
* reusable prefabs
* predictable behaviour

After creating each major system:

1. compile
2. inspect Console
3. wire references
4. test it
5. only then continue

Do not continue accumulating features on top of a broken system.

---

# 40. FINAL DELIVERY

At the end, leave me with:

* fully playable game
* polished main scene
* functional player
* working lantern/fuel system
* working fuel pickups
* working vampires
* light-based vampire AI
* radio collection mission
* radio centre
* locket
* prayer system
* proper win/lose flow
* Android touch controls
* Web/Desktop controls
* polished UI
* audio
* lighting
* atmosphere
* animations
* optimized performance
* clean scripts
* no compile errors
* no obvious missing references

Also provide a concise implementation summary showing:

1. what was reused from the existing prototype
2. what systems were added
3. what scripts were created/modified
4. what GameObjects/prefabs were created
5. what remains intentionally simple
6. exact Play Mode tests performed
7. any remaining issues that genuinely require manual Unity interaction

Do not claim something works unless it was actually validated through the available Unity/MCP tools.

Most importantly:

**Do not optimize for amount of code. Optimize for player experience.**

The final result should feel like a small, atmospheric, polished survival game that a judge can understand within seconds and remember after playing.