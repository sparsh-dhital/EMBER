# PROJECT EMBER

## CLAUDE OPUS — FULL PROFESSIONAL REFINEMENT, POLISH, PHYSICS, ANIMATION, COMBAT, CAMERA & ENVIRONMENT PASS

You are now responsible for taking my **existing playable Unity game prototype** and turning it into a polished, atmospheric, believable, exciting small-scale 3D survival game.

I am using **Unity 6**, Unity MCP, version control, Android + Web builds, and an existing project that already contains a working version of the game.

I do NOT want a greenfield rewrite.

I want you to inspect the current project deeply and improve what already exists.

The current game concept is:

> A survivor explores a dark environment carrying a lantern. Fuel continuously decreases. Strong light keeps the hostile creatures away; weak light causes them to approach. The survivor must collect radio parts, find a sacred locket that unlocks a one-time prayer emergency, survive the darkness, and return to a communication centre to call for help.

The existing game is already functional enough to play, but it currently feels like a prototype.

Your job is to make it feel like a **real game**.

The target is NOT photorealism.

The target is:

**Believable movement + convincing animation + readable darkness + atmospheric environment + responsive combat + intelligent enemy behaviour + beautiful lighting + polished UI + satisfying feedback + stable performance.**

The final experience should feel deliberately authored by a competent indie game team rather than like a collection of generated Unity components.

---

# 0. ABSOLUTE RULES

Follow these rules throughout the entire task.

### RULE 1 — DO NOT REBUILD BLINDLY

First inspect the existing project.

Inspect:

- project hierarchy
- active scene(s)
- player GameObject and hierarchy
- character model
- Animator
- existing animation controller
- movement scripts
- camera
- lantern
- fuel system
- enemy/zombie/vampire system
- combat if present
- radio system
- locket
- prayer
- UI
- lighting
- materials
- terrain
- environment
- colliders
- NavMesh / AI navigation
- input system
- packages
- audio
- post-processing
- quality settings
- build settings
- Android settings
- Web settings
- Console errors and warnings

Understand what is already good.

Reuse good systems.

Refactor weak systems.

Replace broken systems only when necessary.

Do NOT delete working functionality simply because you prefer a different implementation.

---

# 1. CREATE A RESTORE POINT BEFORE MAJOR CHANGES

Because the project is already under version control:

Before substantial modifications:

- verify the project is in a known working state
- inspect the current branch/status
- create a sensible checkpoint/commit if appropriate
- make changes incrementally

Never turn a working game into a massive untestable change-set.

Implement one major system at a time.

After each major system:

1. compile
2. inspect Unity Console
3. fix errors
4. test
5. continue

Do not accumulate compile errors.

---

# 2. FIRST TASK — AUDIT THE EXISTING GAME

Before changing gameplay, perform a professional technical audit.

Identify:

### PLAYER

- current controller
- movement method
- gravity
- jumping
- grounded detection
- slope handling
- collision
- step handling
- animation synchronization
- acceleration/deceleration
- rotation
- sprint/run
- crouch/crawl
- interaction

### CAMERA

- current perspective
- camera follow
- clipping
- collision
- FPP support
- TPP support
- transitions

### ANIMATION

- Animator Controller
- avatar
- locomotion blend trees
- idle
- walking
- running
- jump
- landing
- crouch/crawl
- combat
- hit reaction
- death
- prayer
- lantern holding

### ENEMIES

- movement
- navigation
- state machine
- detection
- pathfinding
- attack
- damage
- death
- response to lantern
- response to prayer

### ENVIRONMENT

- terrain
- hills
- obstacles
- trees
- rocks
- buildings
- colliders
- navigation
- lighting

### RENDERING

- URP
- shadows
- lights
- fog
- bloom
- ambient lighting
- exposure
- anti-aliasing
- materials
- texture quality
- post-processing

### UI

- HUD
- fuel
- mission counter
- objective
- prayer state
- interaction prompts
- menus
- game-over
- victory

### AUDIO

- footsteps
- movement
- enemy audio
- attacks
- death
- fuel
- prayer
- environment
- music
- mixer

At the end of the audit, identify the **highest-impact problems**, then fix them in the order that most improves player perception.

Do not merely report the problems and stop.

Proceed directly into implementation.

---

# 3. PLAYER LOCOMOTION — MAKE IT FEEL HUMAN

The player currently feels too game-like.

Rework locomotion to feel like an actual human moving through uneven terrain.

The player should have these states:

- Idle
- Walk
- Jog
- Run
- Jump Start
- In Air
- Falling
- Landing
- Crouch
- Crawl
- Attack
- Hit
- Death
- Prayer

Do not make these states feel like separate disconnected animations.

They must blend naturally.

---

# 4. MOVEMENT PHYSICS

Do not move the player simply by directly changing the Transform position every frame.

Use a proper collision-aware locomotion system.

The player should have:

- capsule collision
- grounded detection
- gravity
- acceleration
- deceleration
- slope handling
- step handling
- collision response
- air control
- jump impulse
- controlled falling
- proper ground snapping
- sensible stopping

Gravity should feel believable.

Do not allow the character to float.

Do not allow the character to remain airborne after moving over tiny terrain changes.

Do not allow the player to sink into terrain.

Do not allow the player to hover above the ground.

---

# 5. HUMAN-LIKE ACCELERATION

The player should not instantly go from:

0 → maximum speed

Instead:

Idle
→ accelerate
→ walk/jog
→ run

Likewise:

Run
→ decelerate
→ walk
→ idle

Use tuned acceleration/deceleration so the character has physical presence.

However:

DO NOT make movement sluggish.

This is a game.

The controls should remain responsive.

Target:

- walking: believable
- running: energetic
- stopping: slight momentum
- turning: controlled
- direction changes: smooth
- reverse movement: visibly slower than simply snapping 180 degrees

---

# 6. WALKING STYLE

The current walking style does not look natural.

Fix this comprehensively.

The character's animation must respond to actual movement speed.

Use an Animator Blend Tree or equivalent system.

Drive locomotion from actual player velocity.

Do not use the same animation regardless of how quickly the character moves.

Walking should have:

- natural leg cadence
- natural arm swing
- subtle torso movement
- subtle shoulder movement
- believable foot placement
- controlled head movement
- natural turn transitions

Running should have:

- longer stride
- stronger arm swing
- increased body movement
- slight forward lean
- faster foot cadence

Do not allow visible foot sliding.

Tune animation playback speed to actual movement speed.

If current animations have poor quality, replace them with better compatible animations where practical.

Do not blindly enable root motion for all locomotion.

Use animation-driven movement only where it actually improves fidelity and does not fight the gameplay controller.

Root motion can be evaluated for authored actions such as attacks, finishers and certain interactions.

---

# 7. TERRAIN-AWARE LOCOMOTION

The character must react naturally to hills.

When walking uphill:

- movement should remain grounded
- feet should align naturally with the terrain
- animation should remain believable
- movement may slow slightly on steep slopes

When moving downhill:

- maintain contact with terrain
- avoid floating
- avoid sudden teleportation
- avoid unnatural bouncing

The player must NOT be able to walk up extremely steep surfaces.

Use a sensible slope limit.

If terrain becomes too steep:

- movement should be blocked, redirected, or transition into an appropriate state
- do not allow walking through terrain

The character's orientation should follow terrain where appropriate without making the entire body tilt unnaturally.

Use ground-normal projection for movement.

---

# 8. JUMPING

Implement proper jumping.

Jump should:

- only initiate when grounded
- have a readable takeoff
- use gravity
- have a real arc
- have a falling phase
- have a landing phase
- transition correctly into locomotion

Do NOT simply move the player upward for a fixed time.

Use physics/gravity to create an actual jump trajectory.

Add:

- jump anticipation
- airborne animation
- fall animation if appropriate
- landing animation
- subtle camera impact
- landing sound

Prevent:

- infinite air jumps
- mid-air repeated jumps
- jumping through ceilings
- hovering

Do not turn the game into a parkour game.

Jumping should primarily be traversal and physical realism.

---

# 9. CROUCH / CRAWL

Add an actual crawl state.

The character should lower its body.

Crawl should change:

- collider height
- player height
- movement speed
- camera height
- animation
- shadow shape
- interaction height

Crawling should be useful for moving under low obstacles.

Before returning to standing:

perform an overhead clearance check.

If there is an obstacle above the player:

DO NOT allow the player to stand and clip through it.

The character should remain crouched/crawling until enough space exists.

Use a capsule clearance check rather than blindly changing the collider height.

This is important.

---

# 10. OBSTACLE COLLISION — FIX THE TREE BUG

The player currently passes through environmental objects.

Perform a complete collision audit.

Trees:

- trunk must have appropriate collider
- branches may use simplified colliders where necessary
- player must not walk through trunks

Rocks:

- collision must match visual footprint

Buildings:

- walls must block player
- doors/openings must remain traversable

Fences:

- block where appropriate

Large props:

- properly collidable

Terrain:

- must have correct terrain collision

Do NOT put expensive MeshColliders on every small object.

Use:

- capsule colliders
- box colliders
- simple convex colliders
- terrain collider
- simplified collision meshes

where appropriate.

The rule is:

**visual mesh can be detailed; collision mesh should be simple and believable.**

---

# 11. REAL-WORLD PHYSICS PHILOSOPHY

When I say "real-world physics", interpret it as:

- believable gravity
- believable acceleration
- believable stopping
- believable collision
- believable slopes
- believable jumping
- believable object interaction
- believable weight
- believable animation
- believable traversal

NOT:

- uncontrollable ragdoll movement
- extremely slow controls
- excessive inertia
- simulation for the sake of simulation

The player should feel like a human operating in a physical environment while remaining enjoyable to control.

---

# 12. CAMERA SYSTEM — FPP + TPP

The game must support both:

## TPP — THIRD PERSON

Camera:

- behind player
- slightly above player
- smooth follow
- smooth rotation
- collision-aware
- avoids clipping into trees/walls
- maintains character visibility

Target feel:

cinematic survival game.

Do not let the camera snap violently.

Do not allow walls to completely hide the player.

Use camera obstruction handling.

When camera approaches an obstacle:

- move camera closer
- avoid clipping
- restore distance when clear

---

# 13. FPP — FIRST PERSON

Add a proper first-person camera.

Do not simply move the third-person camera into the face.

FPP should have:

- eye-level camera
- believable height
- subtle head movement
- subtle breathing
- subtle movement bob
- restrained camera sway
- proper lantern visibility
- no character head clipping
- no camera clipping through body

Hide/adjust the character mesh appropriately in FPP so:

- body does not block camera
- head does not intersect camera
- lantern remains believable

When looking down, the player should not see ugly geometry inside the character.

---

# 14. CAMERA TOGGLE

Desktop:

`V` = switch FPP/TPP

Mobile:

add a small camera toggle button.

Transition between FPP and TPP smoothly.

Do not instantly teleport the camera.

Use a short transition.

Preserve player orientation.

Do not reset gameplay state.

The camera mode must not affect the underlying gameplay systems.

---

# 15. LANTERN IN FPP + TPP

The lantern must remain believable in both camera modes.

TPP:

- clearly attached to hand
- correct arm pose
- light originates from lantern

FPP:

- visible hand/lantern representation
- no impossible floating light
- no camera clipping
- believable illumination

When the player moves:

the lantern should naturally sway with the hand.

When the player runs:

lantern movement becomes slightly more noticeable.

When the player stops:

lantern settles.

This will significantly increase perceived realism.

---

# 16. CHARACTER SHADOW

The character currently lacks a convincing shadow.

Fix this.

The player must cast visible shadows.

At minimum:

- character shadow on ground
- shadow during standing
- shadow while running
- shadow while near walls
- lantern/light interaction should create meaningful shadowing

The shadow should not look like a black blob.

Use the rendering pipeline appropriately.

Avoid excessive shadow distance or unnecessarily expensive settings.

The shadow should remain visible without destroying mobile performance.

Prioritize:

**near-camera shadow quality > distant shadow quality.**

---

# 17. LIGHTING — DO NOT MAKE DARKNESS PURE BLACK

This is one of the most important changes.

Current darkness is too intense.

The world should remain clearly dark but still readable.

I want:

**Dark atmosphere, not black screen.**

The player should be able to see:

- trees
- ground
- terrain
- buildings
- silhouettes
- environmental landmarks
- enemies
- shadows

without needing the lantern pointed directly at everything.

Use a layered lighting strategy.

Suggested visual hierarchy:

### Moonlight / ambient environment

Cool, very low-intensity directional illumination.

### Lantern

Warm, strongest local source near player.

### Environment fill

Extremely subtle ambient visibility so the world is readable.

### Fog

Adds depth without completely hiding geometry.

### Colour grading

Cooler shadows.

Warmer local lantern light.

Avoid crushing blacks.

Avoid making distant objects disappear into solid black.

---

# 18. LIGHTING TARGET

The player should look at the scene and think:

"Something bad could be hiding there."

not:

"I literally cannot see anything."

The environment should have:

- visible silhouettes
- readable terrain
- controlled dark values
- localized light
- atmospheric depth
- contrast between safe and dangerous areas

The lantern remains important because it provides:

- strongest visibility
- warm comfort
- enemy deterrence
- navigation assistance

But the environment should still be visually readable without it.

---

# 19. VISUAL REALISM / GRAPHICS QUALITY

Improve overall graphical fidelity.

Interpret "improve the pixels" as:

- higher-quality textures where available
- better material response
- better normals
- better roughness
- better lighting
- cleaner anti-aliasing
- better shadow filtering
- better environment composition
- better texture import settings
- reduced visible artifacts
- better LOD transitions

Do NOT simply increase every texture resolution blindly.

That will hurt Android performance.

Use appropriate texture compression and mipmaps.

Improve the assets that the player notices most:

1. character
2. lantern
3. enemy
4. sword
5. radio centre
6. key props
7. nearby terrain
8. foreground foliage

Distant objects can remain optimized.

---

# 20. MATERIAL QUALITY

Improve major materials.

Character:

- skin/material response where appropriate
- clothing roughness
- subtle normal detail
- believable highlights

Lantern:

- glass
- metal
- emissive flame
- warm reflection

Enemy:

- convincing dark/red material
- subtle roughness variation
- readable silhouette

Environment:

- soil
- grass
- rock
- wood
- metal
- concrete

Avoid everything looking like the same plastic material.

---

# 21. ENVIRONMENT REFINEMENT

The environment needs to feel authored.

Do not simply spread random assets around the map.

Create visual storytelling.

Use:

- paths
- clearings
- clusters of trees
- rocks
- ruins
- broken equipment
- old structures
- signs of abandonment
- small environmental details
- varied ground height
- natural composition

Create several recognizable micro-areas.

For example:

### AREA A — RADIO CENTRE

Relatively safe.

Contains:

- radio equipment
- antenna
- shelter
- subtle emergency lighting

### AREA B — WOODED PATH

Dense trees.

Limited visibility.

Fuel pickup nearby.

### AREA C — ABANDONED STRUCTURE

Higher-risk location.

Radio part.

Dark interior.

### AREA D — OPEN HILL

Long sightline.

Enemies visible at distance.

Strong silhouette composition.

### AREA E — LOST SHRINE / SACRED AREA

Locket location.

Subtle environmental storytelling.

Do not create a massive world.

Make the existing world denser, more believable and more memorable.

---

# 22. ENVIRONMENTAL STORYTELLING

Use objects to tell the story without excessive text.

Examples:

- abandoned emergency equipment
- broken radio
- torn cloth
- old camp
- overturned equipment
- damaged vehicle
- discarded lantern/fuel container
- strange markings
- abandoned shelter
- distant antenna

The player should feel:

"Someone was here."

This adds atmosphere without requiring a complicated narrative system.

---

# 23. ZOMBIE / VAMPIRE VISUAL REFINEMENT

Keep the existing enemy type unless changing the model clearly improves the game.

They must remain visually readable.

The enemy should have:

- dark/red body
- readable silhouette
- visible face
- red eyes or subtle emissive accents
- believable animation
- smooth locomotion
- attack animation
- hit reactions
- death animation

Do not make the enemy look like a generic floating red asset.

Use controlled red accents rather than painting the entire screen red.

---

# 24. ENEMY MOVEMENT

Enemy movement must also respect the environment.

They should not:

- pass through trees
- pass through walls
- walk through rocks
- slide over terrain
- float above hills
- clip through the player

Use appropriate navigation.

Where appropriate, use Unity AI Navigation/NavMesh.

Bake or generate navigation for walkable terrain.

Respect:

- slopes
- obstacles
- restricted areas
- structures

The enemy should navigate around obstacles instead of moving directly through them.

---

# 25. ENEMY LIGHT RESPONSE

Keep the existing signature mechanic.

Strong lantern:

Enemy retreats.

Medium lantern:

Enemy stalks/approaches.

Low lantern:

Enemy becomes aggressive.

Lantern OFF:

Enemy becomes highly dangerous.

Make state transitions stable.

Do not allow:

FLEE → APPROACH → FLEE → APPROACH

every frame.

Use sensible threshold hysteresis.

The player should visually understand the relationship.

---

# 26. ENEMY ANIMATION QUALITY

Improve:

- idle
- walk
- run
- chase
- attack
- stagger
- death

Movement should use animation blending based on velocity.

Do not allow visible foot sliding.

Attack should contain:

1. anticipation
2. movement
3. strike window
4. follow-through
5. recovery

Do not allow instant damage with no telegraph.

---

# 27. PLAYER DEATH / KILL ANIMATION

This needs major improvement.

When a zombie/vampire successfully kills the player:

do NOT instantly switch to Game Over.

Create a short controlled death sequence.

Example:

Enemy approaches.

Enemy begins attack.

Player reacts.

Enemy performs lethal strike/grab.

Player staggers/falls.

Camera responds.

Player death animation plays.

Audio reaches a climax.

Screen transitions naturally to defeat state.

Target approximately:

1–3 seconds depending on animation quality.

Do not make it excessively long.

The player should clearly understand:

"I died because the enemy caught me."

---

# 28. DEATH CAMERA

In TPP:

- camera stays readable
- small cinematic push/tilt
- player remains visible

In FPP:

- impact
- camera shake
- brief focus distortion or vignette
- player collapse/death
- controlled fade

Do not spin the camera wildly.

Do not make the effect nauseating.

---

# 29. DEATH AUDIO

Death sound must be smooth.

Use layered audio:

- impact sound
- enemy vocalization
- player reaction
- environmental sound ducking
- low-frequency tension
- final audio release

When death occurs:

temporarily lower ambient/gameplay audio.

Allow the death moment to become the focus.

Avoid five sounds playing at the exact same volume simultaneously.

---

# 30. AUDIO MIXING

Create or improve an AudioMixer structure.

Suggested groups:

- Master
- Music
- Ambient
- Player
- Enemy
- UI
- Effects

Use ducking where appropriate.

Examples:

During death:

Music ↓

Ambient ↓

Enemy remains audible

Death sound ↑

During prayer:

Ambient changes

Heartbeat changes

Prayer sound layer increases

During low fuel:

Heartbeat becomes increasingly prominent.

Audio should feel like a designed composition rather than separate sound files being played randomly.

---

# 31. FOOTSTEP SYSTEM

Improve footsteps.

Footstep timing should come from animation events or a reliable locomotion timing system.

Do NOT simply play footsteps every X seconds regardless of movement speed.

Footsteps should respond to:

- walk speed
- run speed
- crawl
- terrain

Where practical, use different sounds for:

- grass
- dirt
- wood
- stone
- indoor surfaces

Keep the implementation lightweight.

---

# 32. AMBIENT AUDIO

Make the environment sound alive.

Use:

- wind
- leaves
- distant creatures
- insects
- branches
- distant impacts
- radio interference
- environmental creaks
- occasional distant enemy sounds

Do not make the environment noisy constantly.

Silence should also be used as tension.

---

# 33. LOW-FUEL AUDIO

Create an escalating sound design curve.

High fuel:

minimal warning.

Medium:

subtle heartbeat.

Low:

clear heartbeat.

Critical:

fast heartbeat + subtle tension layer.

Empty:

lantern extinguish sound + audio transition.

The audio should teach the player:

"Something is getting dangerous."

without needing a giant warning message.

---

# 34. SWORD SYSTEM

Add exactly ONE sword as a gameplay weapon.

Do not create a full inventory system.

The sword should be a meaningful emergency tool.

Possible implementation:

- sword is found/collected in the environment
- when collected, attaches correctly to character
- player can equip/use it
- simple attack system
- hit detection
- enemy damage
- hit feedback

Do not create complicated combos unless the existing game already supports them well.

---

# 35. SWORD COMBAT

The sword attack should feel physical.

Attack:

- anticipation
- swing
- impact window
- follow-through
- recovery

Use an actual hit detection volume during the attack window.

Do NOT leave a permanent sword collider active.

The sword should damage an enemy only during the intended strike window.

One swing should not deal repeated damage every frame.

---

# 36. SWORD DURABILITY

The sword breaks after approximately 3–4 confirmed enemy kills.

Default:

`4 kills maximum`

Make this configurable in the Inspector.

Track:

`SWORD 4/4`

Then:

`SWORD 3/4`

`SWORD 2/4`

`SWORD 1/4`

After the final kill:

Sword breaks.

Play:

- break sound
- visual feedback
- UI update
- subtle animation

Do not count hits.

Count confirmed kills.

The sword should not break just because the player swung four times.

---

# 37. SWORD BREAK MOMENT

When the durability reaches zero:

- play break sound
- brief vibration/feedback where supported
- weapon becomes unavailable
- character sheath/empty-hand animation
- UI updates
- do not spawn another sword

This creates tension:

"The weapon cannot save me forever."

---

# 38. COMBAT BALANCE

The sword should NOT trivialize the horror.

Enemies should remain threatening.

The player should use the sword strategically.

Combat should buy time, not turn the game into an action RPG.

The lantern must remain the primary survival mechanic.

The sword is secondary.

---

# 39. PRAYER / JESUS SIGN REFINEMENT

The prayer mechanic should feel like a major supernatural moment.

When prayer activates:

- character enters prayer animation
- character pauses/reduces movement briefly if necessary
- luminous symbol appears
- symbol should be clearly Christian
- warm supernatural light radiates from player
- enemy behaviour changes
- enemies retreat
- audio shifts
- scene briefly feels calmer

Do not make the effect look like a generic mobile-game emoji.

---

# 40. JESUS / CROSS SYMBOL VISUAL

Improve the glowing effect significantly.

The symbol should have:

- controlled emissive glow
- soft bloom
- slight pulse
- subtle particle halo
- smooth appearance
- smooth disappearance

Use layered animation:

Core symbol:
stable.

Outer glow:
slow pulse.

Particles:
very subtle movement.

Light:
slight breathing/pulsing.

The effect should become brightest at activation.

Then settle into a softer sustained glow.

Do not make it flash aggressively.

---

# 41. PRAYER LIGHT

The prayer light should be noticeably stronger than the lantern.

But:

DO NOT turn the entire screen white.

It should create a local sacred glow.

Nearby environment should receive the light.

The enemy should visibly react to it.

The lighting should communicate:

"Something supernatural is protecting you."

---

# 42. PRAYER VFX QUALITY

Add restrained effects such as:

- subtle floating particles
- soft radial light
- gentle spark-like particles
- controlled bloom
- slight atmospheric glow

Avoid:

- excessive particles
- giant explosions
- arcade-style magic effects
- rainbow colours

Keep the effect elegant and atmospheric.

---

# 43. PRAYER GAMEPLAY RULE

Prayer can only activate:

- after fuel reaches zero
- if locket has been collected
- once per run

Prayer lasts approximately:

`60 seconds`

Prayer does not refill fuel.

It provides:

temporary protection.

Enemy response:

strong retreat.

The player must use that time wisely.

---

# 44. PRAYER UI

When locket is collected:

`PRAYER READY`

When fuel reaches zero:

`THE FLAME IS OUT`

After activation:

`PRAYER 60`

Countdown smoothly to:

`59 ... 58 ...`

Do not update UI in a visually jarring way.

Use smooth transitions.

---

# 45. RADIO MISSION

Keep the radio mission as the main objective.

Collect all required radio parts.

The player should see:

`RADIO PARTS 0/5`

Update dynamically.

After all parts:

`RADIO PARTS COMPLETE`

then:

`RETURN TO THE RADIO CENTRE`

Do not make the objective completion instant.

The player must physically return.

---

# 46. RADIO CENTRE POLISH

Make the radio centre visually memorable.

It should have:

- antenna
- radio equipment
- cables
- generator/equipment
- subtle lights
- atmospheric props

When inactive:

- old
- dark
- quiet

When all radio components are collected:

- radio wakes up
- light activates
- equipment begins humming
- indicator lights turn on
- audio signal begins

This creates a strong sense of progression.

---

# 47. RADIO CALL SEQUENCE

When player reaches centre:

show:

`INTERACT`

When activated:

- character approaches radio
- animation plays
- radio lights activate
- static plays
- signal sound builds
- short dialogue/transmission if existing
- rescue call is sent

Do not make the final interaction instant.

Make it feel like the player actually completed something.

---

# 48. FINAL ESCAPE SEQUENCE

The final objective should become tense.

After the radio call:

- enemy pressure increases slightly
- audio intensifies
- radio signal continues
- player must survive the final moments

Do not create a huge cinematic that consumes most of the game.

Keep it lightweight but memorable.

Then trigger victory.

---

# 49. GAME OVER QUALITY

Game Over should feel polished.

Show:

`THE DARKNESS TOOK YOU`

or an equivalent concise message.

Show:

- survival time
- radio parts
- sword kills if tracked
- optional best time

Buttons:

`RETRY`

`MAIN MENU`

Do not make the screen feel like a default Unity sample.

---

# 50. VICTORY QUALITY

Victory should clearly communicate:

`SIGNAL SENT`

`HELP IS COMING`

Then:

`YOU SURVIVED`

Use:

- atmospheric audio release
- subtle brightness increase
- clean animation
- elegant UI

Avoid a generic "YOU WIN!!!" arcade presentation.

---

# 51. UI REDESIGN

The UI currently needs visual refinement.

Create a consistent visual language.

Use:

- clean typography
- subtle glass/translucent panels
- soft shadows
- restrained glow
- smooth transitions
- consistent spacing
- consistent iconography

Do NOT cover the entire screen with HUD.

Primary information:

Fuel

Radio parts

Objective

Sword durability

Prayer state

Interaction prompt

Everything else should remain secondary.

---

# 52. FUEL UI

Fuel should feel integrated into the world.

Possible design:

small lantern icon + fuel percentage + subtle meter.

At high fuel:

normal state.

At medium:

slight pulse.

At low:

warning state.

At critical:

stronger visual warning.

At zero:

meter transitions smoothly to empty.

Avoid giant red bars.

---

# 53. MOBILE CONTROLS UI

Create polished Android controls.

Left:

movement joystick.

Right:

camera region.

Buttons:

- interact
- attack
- prayer when available
- camera toggle
- pause

Keep buttons:

- translucent
- responsive
- large enough for touch
- visually subtle
- separated enough to avoid accidental presses

Do not make the UI look like a cheap mobile game.

---

# 54. BUTTON FEEDBACK

Every button should have:

- press animation
- hover/active state where applicable
- subtle scale
- subtle sound

Use smooth easing.

No instantaneous ugly jumps.

---

# 55. RESPONSIVE UI

Verify:

- 16:9
- 18:9
- modern Android tall aspect ratios
- desktop/browser

Use safe-area handling.

No UI should overlap:

- notch areas
- camera controls
- important gameplay information

---

# 56. GRAPHICAL POST-PROCESSING

Use URP appropriately.

Improve:

- tone mapping
- color grading
- bloom
- vignette
- ambient occlusion where performance allows
- anti-aliasing
- fog

The visual target:

dark horror atmosphere + readable environment + warm lantern contrast.

Do not over-process the image.

The final image should look cinematic, not filtered.

---

# 57. SHADOW QUALITY

Tune:

- shadow distance
- shadow resolution
- shadow filtering
- bias
- normal bias
- cascades where appropriate

Fix:

- acne
- floating shadows
- overly hard shadows
- missing shadows
- flickering shadows

Keep nearby shadows high quality.

Distant shadows can be cheaper.

---

# 58. MOBILE GRAPHICS TIERS

Create sensible quality tiers if the current project architecture allows.

Example:

### MOBILE PERFORMANCE

- lower shadows
- lower post-processing
- reduced particles
- optimized texture quality

### MOBILE HIGH

- better shadows
- better effects
- better lighting

### WEB / HIGH QUALITY

- stronger visual quality
- higher shadows
- higher effects

Do not allow mobile settings to destroy the core visual identity.

---

# 59. ENVIRONMENT LIGHTING COMPOSITION

Make each important location have its own visual mood.

Radio centre:

warm/cool mixed emergency mood.

Forest:

cooler and mysterious.

Abandoned building:

deeper shadows but still readable.

Hill:

open moonlight and silhouettes.

Sacred locket area:

subtle supernatural warmth.

Use lighting to guide the player without obvious arrows.

---

# 60. PLAYER GUIDANCE

Do not add a giant minimap.

Guide the player using:

- landmarks
- subtle objective indicator
- environmental composition
- sound
- light
- antenna visibility
- distinctive buildings

The player should feel like they are exploring rather than following a GPS arrow.

---

# 61. GAME FEEL / JUICE

Add polish to every important interaction.

Fuel pickup:

- sound
- small particle burst
- UI animation
- lantern strengthening

Radio pickup:

- sound
- subtle feedback
- mission counter animation

Locket pickup:

- supernatural sound
- small visual burst
- prayer UI unlock

Sword attack:

- swing sound
- impact sound
- animation
- subtle camera feedback

Enemy attack:

- sound
- hit reaction
- camera impact

Prayer:

- visual transition
- audio swell
- supernatural lighting

Death:

- impact
- animation
- audio transition

Victory:

- audio release
- UI transition

---

# 62. INPUT ARCHITECTURE

Use the existing input system if good.

Otherwise use Unity's New Input System.

Do not create separate copies of gameplay logic for Android and Web.

The same gameplay actions should exist abstractly:

- Move
- Look
- Jump
- Crawl
- Interact
- Attack
- Prayer
- ToggleCamera
- Pause

Platform-specific input should only map to those actions.

---

# 63. SAVE / GAME STATE

Do not add a complicated save system.

The run should reset cleanly.

Persist only simple information such as:

- best survival time
- optional settings

Do not introduce unnecessary persistent state.

---

# 64. PERFORMANCE

This game must remain playable on Android.

Watch for:

- excessive real-time lights
- excessive shadow casters
- too many particles
- too many enemies
- expensive MeshColliders
- unnecessary Update loops
- excessive allocations
- repeated physics queries
- unnecessarily large textures
- expensive post-processing

Use pooling for recurring objects if appropriate.

Keep enemy counts controlled.

Keep effects local to the player.

---

# 65. COLLISION LAYERS

Create sensible collision layers.

For example:

- Player
- PlayerHitbox
- Enemy
- EnemyHitbox
- Environment
- Interactable
- Pickup
- DynamicProp

Do not allow every system to collide with every other system.

This will reduce bugs and physics cost.

---

# 66. DEBUG TOOLS

Create lightweight development/debug support.

Useful debug information:

- current player speed
- grounded state
- slope angle
- fuel
- lantern intensity
- current enemy state
- sword durability
- prayer state
- radio parts

This can be behind a development/debug toggle.

Do not show debug UI in the final release.

---

# 67. ANIMATION QUALITY AUDIT

Perform an explicit animation audit.

Look for:

- foot sliding
- snapping
- animation pops
- incorrect transitions
- unnatural turns
- poor death pose
- poor attack timing
- lantern not following hand
- sword not following hand
- incorrect root orientation
- excessive looping

Fix these issues rather than simply adding more animations.

Quality > quantity.

---

# 68. IK / FOOT PLACEMENT

Where practical and compatible with the current character setup:

use foot IK or another lightweight terrain-aware foot placement solution.

The character should appear planted on slopes.

Feet should not visibly float above uneven ground.

Do not force an expensive rigging solution if it would destabilize the project.

---

# 69. CHARACTER BODY LANGUAGE

Improve the player's body language.

Idle:

subtle breathing.

Walking:

relaxed.

Running:

forward lean.

Low fuel:

slightly tense animation/audio only if it looks natural.

Prayer:

stable deliberate pose.

Damage:

brief body reaction.

Death:

clear loss of control.

These details dramatically affect perceived quality.

---

# 70. INTERACTION SYSTEM

Do not create separate interaction code for every object.

Use a reusable interaction system.

Interactable objects:

- fuel
- radio part
- locket
- radio centre
- sword

The player should receive a contextual prompt only when close enough.

Example:

`E  PICK UP`

`E  ACTIVATE RADIO`

`E  COLLECT`

On mobile:

show the interact button only when interaction is available.

---

# 71. AUDIO SPATIALIZATION

Where appropriate:

use 3D spatial sound for:

- enemies
- radio
- environmental sounds
- pickups
- major points of interest

The player should be able to hear that danger is coming from somewhere.

Do not make every UI sound spatial.

---

# 72. DYNAMIC TENSION SYSTEM

Create one lightweight gameplay tension value.

It can respond to:

- fuel level
- number of nearby enemies
- distance to enemy
- prayer state
- radio completion

Use it to subtly influence:

- heartbeat
- ambient intensity
- music layer
- enemy audio

Do not create a huge adaptive music framework.

Keep it simple.

---

# 73. ENEMY SPAWN FAIRNESS

Never spawn an enemy:

- directly beside the player
- inside a wall
- inside a tree
- directly on the player
- immediately beside a required pickup

Spawn enemies in valid navigable locations.

Keep early game forgiving.

Increase pressure later.

---

# 74. DIFFICULTY CURVE

Opening:

player feels safe.

Middle:

player begins worrying about fuel.

Late:

enemy pressure increases.

Emergency:

lantern dies.

Prayer:

temporary relief.

Final:

race back / complete rescue.

The emotional curve should resemble:

**Calm → Curiosity → Unease → Tension → Panic → Hope → Escape**

That is more important than adding more mechanics.

---

# 75. ENVIRONMENTAL VISIBILITY TEST

Perform an explicit lighting test.

Start with lantern OFF.

Stand at several points.

Verify that the player can still visually understand:

- terrain
- tree silhouettes
- buildings
- paths
- enemies at appropriate distances

Then activate lantern.

The scene should become significantly clearer and warmer.

The lantern should improve visibility without turning the scene from black to daylight.

---

# 76. FPP/TPP QUALITY TEST

Test:

TPP walking

TPP running

TPP jumping

TPP crawling

TPP slopes

TPP lantern

TPP sword

TPP combat

TPP death

FPP walking

FPP running

FPP jumping

FPP crawling

FPP lantern

FPP sword

FPP combat

FPP death

Switch between modes repeatedly.

There must be no:

- clipping
- broken hands
- invisible sword
- broken lantern
- camera inside objects
- incorrect animator state
- loss of input

---

# 77. PHYSICS QA

Explicitly test:

- tree collision
- rock collision
- wall collision
- hill movement
- steep slope
- jumping
- falling
- landing
- crawling beneath obstacle
- standing beneath low ceiling
- enemy navigation around obstacle
- enemy collision
- weapon collision

Fix every obvious physical exploit.

---

# 78. MOBILE QA

Test actual Android behaviour.

Check:

- joystick response
- camera response
- touch buttons
- screen aspect ratio
- UI safe area
- FPS
- thermal/performance issues
- shadows
- particle count
- enemy count
- memory usage
- loading
- scene transition

Do not assume the Editor represents the phone accurately.

The roadmap explicitly prioritizes a working phone build and real-device testing rather than relying only on Unity Editor testing.

---

# 79. WEB QA

Test:

- keyboard controls
- mouse camera
- FPP/TPP switching
- attack
- interaction
- prayer
- pause
- UI scaling
- audio
- loading

Do not let Android-specific controls appear awkwardly on Web.

---

# 80. PROJECT STRUCTURE

Keep code modular.

Prefer systems such as:

`GameManager`

`PlayerController`

`PlayerLocomotion`

`PlayerGravity`

`PlayerCamera`

`PlayerAnimator`

`PlayerInteractor`

`LanternFuelSystem`

`LanternVisualController`

`FuelPickup`

`RadioMissionSystem`

`RadioPart`

`RadioCentre`

`LocketPickup`

`PrayerSystem`

`PrayerVisualController`

`EnemyAI`

`EnemyStateMachine`

`EnemySpawner`

`EnemyCombat`

`SwordController`

`SwordDurability`

`AudioManager`

`AudioTensionController`

`HUDController`

`CameraModeController`

`GameOverController`

Do not split code into dozens of meaningless scripts.

Use components where they provide clear responsibility.

---

# 81. SCRIPTING QUALITY

Code must be:

- readable
- maintainable
- modular
- serialized where useful
- easy for a beginner to tune
- free of hidden magic numbers
- defensive against null references
- safe against duplicate event subscriptions

Expose tuning values in Inspector.

Examples:

- walk speed
- run speed
- crawl speed
- acceleration
- deceleration
- gravity
- jump height
- slope limit
- fuel drain rate
- lantern radius
- lantern intensity
- prayer duration
- sword durability
- enemy speed
- enemy attack range
- enemy flee radius

Do not bury all values inside code.

---

# 82. ERROR HANDLING

Every major system should gracefully handle:

- missing reference
- missing Animator
- missing AudioSource
- missing prefab
- unavailable target
- destroyed enemy
- destroyed pickup
- scene transition

Do not allow one missing optional component to crash the entire game.

---

# 83. DO NOT CREATE TECHNICAL DEBT FOR VISUAL POLISH

Never implement visual effects by creating:

- hundreds of duplicated objects
- unnecessary per-frame allocations
- uncontrolled particle spawns
- dozens of realtime lights
- giant canvases rebuilding constantly

Every visual improvement must respect performance.

---

# 84. IMPORTANT VISUAL PRIORITY ORDER

If time is limited, polish in this order:

1. character
2. movement
3. lantern
4. lighting
5. environment near player
6. enemy animation
7. combat
8. death sequence
9. prayer effect
10. UI
11. distant environment

Do not spend an hour beautifying a faraway tree while the player is still sliding through the ground.

---

# 85. IMPORTANT GAMEPLAY PRIORITY ORDER

If time is limited:

1. player movement
2. camera
3. collision
4. lantern/fuel
5. enemy behaviour
6. radio mission
7. sword combat
8. prayer
9. animation
10. environment
11. audio
12. UI
13. polish

Core gameplay stability comes first.

---

# 86. WHAT NOT TO ADD

Do NOT introduce:

- multiplayer
- crafting
- inventory grid
- hunger
- thirst
- temperature
- skill tree
- character stats
- full RPG progression
- multiple weapons
- dozens of enemy types
- procedural map generation
- huge open world
- dialogue trees
- complex quest system
- vehicles
- unnecessary cinematic cutscenes

The game should remain focused.

---

# 87. THE CORE DESIGN MUST REMAIN

**LANTERN + FUEL**

is the central mechanic.

**RADIO PARTS**

create exploration.

**ENEMIES**

create pressure.

**SWORD**

provides limited emergency defence.

**LOCKET + PRAYER**

creates a one-time emergency survival window.

**RADIO CENTRE**

creates the final objective.

Everything must support those systems.

---

# 88. PROFESSIONAL POLISH PASS

After functionality works, perform a separate polish pass.

Look at the game like a professional player, not like a programmer.

Ask:

Does the first movement feel good?

Does the player feel heavy enough?

Does the character look alive?

Does the lantern feel physically attached?

Does the environment look attractive in darkness?

Can I understand what is happening?

Do shadows look convincing?

Does the enemy feel dangerous?

Does combat feel responsive?

Does death feel impactful?

Does prayer feel special?

Does the world feel coherent?

Does the UI look expensive?

Does the audio have breathing room?

Does the game feel memorable?

---

# 89. DO NOT OVER-POLISH THE WRONG THING

Never hide poor gameplay under post-processing.

First make:

movement good.

Then:

camera good.

Then:

lighting good.

Then:

animations good.

Then:

audio and UI.

Then:

extra visual polish.

---

# 90. FINAL GAME FEEL TARGET

The intended feeling should be:

You are alone.

It is dark.

You have a small source of safety.

The world is physical.

The environment is beautiful.

Something is watching.

The lantern begins weakening.

You need fuel.

You still need radio parts.

You see a creature in the distance.

You raise the lantern.

It retreats.

Your fuel gets lower.

It begins following.

You find the sword.

You know it will not last forever.

You find the locket.

Eventually the flame dies.

Everything becomes dangerous.

You pray.

A supernatural light surrounds you.

The creatures flee.

You have roughly one minute.

You run toward the radio centre.

You activate the radio.

The signal connects.

You survived.

This emotional progression is the quality target.

---

# 91. FINAL ACCEPTANCE TEST — PLAYER MOVEMENT

PASS only if:

- walking feels natural
- running feels natural
- turning feels natural
- stopping feels natural
- jumping feels physical
- falling feels physical
- landing looks correct
- crawling works
- standing under ceilings works
- slopes work
- steep slopes are restricted
- obstacles block movement
- trees block movement
- rocks block movement
- character remains grounded

---

# 92. FINAL ACCEPTANCE TEST — CAMERA

PASS only if:

- TPP works
- FPP works
- transition works
- no camera clipping
- no character clipping
- no wall clipping
- camera collision works
- mobile camera works
- desktop camera works

---

# 93. FINAL ACCEPTANCE TEST — ANIMATION

PASS only if:

- idle works
- walk works
- run works
- jump works
- fall works
- land works
- crawl works
- attack works
- hit works
- death works
- prayer works
- lantern hand placement looks believable
- sword hand placement looks believable
- no obvious foot sliding

---

# 94. FINAL ACCEPTANCE TEST — LIGHTING

PASS only if:

- darkness remains atmospheric
- environment remains visible
- lantern remains important
- character shadow is visible
- lantern shadows work where practical
- terrain is readable
- important locations are readable
- no obvious lighting bugs
- mobile remains performant

---

# 95. FINAL ACCEPTANCE TEST — ENEMIES

PASS only if:

- enemy walks naturally
- enemy navigates terrain
- enemy respects obstacles
- strong lantern causes retreat
- weak lantern causes approach
- no rapid state flickering
- attack has anticipation
- hit reaction works
- death works
- enemy cannot pass through trees/walls

---

# 96. FINAL ACCEPTANCE TEST — SWORD

PASS only if:

- sword pickup/equip works
- sword follows hand
- attack animation works
- hitbox is timed correctly
- damage happens once per swing
- enemy reacts
- kills are counted
- sword durability decreases only after confirmed kills
- sword breaks at 3–4 configured kills
- sword disappears/becomes unavailable after break

---

# 97. FINAL ACCEPTANCE TEST — PRAYER

PASS only if:

- locket must be collected
- prayer is locked before locket
- prayer requires zero fuel
- prayer works once
- symbol appears smoothly
- glow is polished
- supernatural light is visible
- enemy flees
- countdown works
- approximately 60-second duration
- prayer does not restore fuel
- expiration works

---

# 98. FINAL ACCEPTANCE TEST — MISSION

PASS only if:

- radio parts can be collected
- counter updates
- all parts are required
- return objective activates
- centre interaction works
- radio sequence works
- victory works
- defeat works
- restart works

---

# 99. FINAL ACCEPTANCE TEST — UI

PASS only if:

- fuel display works
- radio counter works
- sword durability works
- prayer status works
- objectives work
- interaction prompts work
- mobile UI works
- FPP/TPP control works
- menus look polished
- transitions are smooth
- UI scales on different screens

---

# 100. FINAL ACCEPTANCE TEST — AUDIO

PASS only if:

- footsteps are synchronized
- run footsteps differ appropriately
- pickup sounds work
- sword sounds work
- enemy audio works
- attack sounds work
- death audio is smooth
- prayer audio is smooth
- radio audio works
- ambient sound works
- low-fuel heartbeat works
- audio is mixed rather than competing

---

# 101. FINAL ACCEPTANCE TEST — PERFORMANCE

PASS only if:

- no obvious memory leaks
- no runaway enemy spawning
- no excessive realtime lights
- no excessive particles
- no expensive collision setup everywhere
- no major frame drops during combat
- no major frame drops during prayer
- no UI stutter
- Android remains playable
- Web remains playable

---

# 102. UNITY CONSOLE RULE

Before finishing:

Unity Console must contain:

**NO UNRESOLVED COMPILE ERRORS.**

Fix the FIRST meaningful error before moving to later errors.

Do not hide errors.

Do not disable systems merely to make the Console appear clean.

---

# 103. MCP WORKFLOW

Use the available Unity MCP tools actively.

Do not behave like a text-only coding assistant.

Use MCP to inspect:

- scene hierarchy
- components
- objects
- materials
- lights
- scripts
- prefabs
- animator setup
- runtime state where supported
- console output
- project settings where supported

Make actual changes in the Unity project where the tools allow it.

When an action cannot safely be automated through MCP and requires manual Unity Editor interaction, tell me exactly what I need to click/change.

Do not pretend an action was performed if it was not.

---

# 104. IMPLEMENTATION STRATEGY

Work in these phases.

## PHASE 1

Audit the project.

## PHASE 2

Fix player locomotion + gravity + slopes + collision.

## PHASE 3

Fix animation synchronization.

## PHASE 4

Implement FPP + TPP camera.

## PHASE 5

Fix environment collisions and navigation.

## PHASE 6

Improve darkness/lighting/shadows.

## PHASE 7

Improve enemy animation + behaviour.

## PHASE 8

Implement/refine sword combat.

## PHASE 9

Improve player death sequence.

## PHASE 10

Improve prayer visual/audio.

## PHASE 11

Improve environment composition.

## PHASE 12

Improve UI.

## PHASE 13

Improve audio.

## PHASE 14

Optimize Android/Web.

## PHASE 15

Perform final QA.

Do not jump randomly between phases.

---

# 105. CRITICAL CHANGE MANAGEMENT RULE

Do not modify ten unrelated systems at once.

Complete a coherent vertical slice.

Example:

Movement:

implement → compile → test → fix.

Then:

camera:

implement → compile → test → fix.

Then:

animation:

implement → compile → test → fix.

Then continue.

This makes debugging manageable.

The existing team workflow guidance specifically recommends one system per request, testing after implementation, and keeping Unity scene ownership controlled rather than generating many interdependent changes simultaneously.

---

# 106. WHEN YOU FIND A BAD EXISTING SYSTEM

Do not automatically rewrite it.

Evaluate:

### Keep

if:

- stable
- understandable
- mostly correct

### Refactor

if:

- correct idea
- poor implementation

### Replace

if:

- fundamentally broken
- impossible to extend safely
- responsible for major bugs

When replacing:

maintain existing public behaviour where possible.

Do not break other systems unnecessarily.

---

# 107. FINAL VISUAL TARGET

The game should visually communicate:

### SAFE

Warm lantern.

Readable environment.

Enemies retreating.

Soft ambience.

### DANGER

Lower light.

Cooler darkness.

Enemies visible.

Heartbeat.

### CRITICAL

Very dim lantern.

Strong shadows.

Enemy silhouettes.

Fast heartbeat.

Strong tension.

### PRAYER

Supernatural glow.

Enemy retreat.

Warm/cool contrast.

Beautiful sacred symbol.

### VICTORY

Relief.

Cleaner ambience.

Radio signal.

Subtle brightness increase.

This visual language should remain consistent.

---

# 108. DO NOT MAKE IT LOOK AI-GENERATED

Avoid:

- excessive glowing outlines
- giant UI
- random neon colors
- excessive particle effects
- arbitrary sci-fi interface elements
- too much bloom
- unrealistic character poses
- floating objects
- perfectly symmetrical environments
- repeated asset patterns
- generic procedural-looking layouts

Use restraint.

Real games are often visually convincing because they make fewer things look intentional.

---

# 109. FINAL PROFESSIONAL REVIEW

After everything is implemented, stop thinking like the developer.

Play the game as a stranger.

Ask:

What do I notice in the first 5 seconds?

What do I understand in the first 15 seconds?

What do I do in the first minute?

What makes me nervous?

What motivates me to move?

What happens when I run low on fuel?

Do I understand enemy behaviour?

Do I understand the sword?

Do I understand prayer?

Do I know where I need to go?

Does the ending feel earned?

Where does the experience feel cheap?

Where does animation break immersion?

Where does physics break immersion?

Where does the UI break immersion?

Fix the highest-impact remaining issues.

---

# 110. COMPLETION REPORT

When the refinement is complete, provide a concise final report with:

### PROJECT AUDIT

What already existed.

### SYSTEMS IMPROVED

Movement.

Physics.

Animation.

Camera.

Lighting.

Enemies.

Combat.

Prayer.

Environment.

UI.

Audio.

Optimization.

### FILES CREATED / MODIFIED

List actual scripts/assets/prefabs modified.

### MANUAL UNITY STEPS

Only list steps that genuinely require manual Editor interaction.

### TEST RESULTS

What was actually tested.

### REMAINING ISSUES

Only real remaining issues.

Do not claim something works unless it was actually tested.

---

# 111. FINAL COMMAND

Do not stop at writing code.

Do not stop after making the game compile.

Do not stop after implementing the requested features.

The objective is:

**TAKE THE EXISTING PROTOTYPE AND MAKE THE ENTIRE PLAY EXPERIENCE FEEL MORE PHYSICAL, MORE BELIEVABLE, MORE ATMOSPHERIC, MORE RESPONSIVE AND MORE POLISHED.**

Prioritize player-perceived quality.

When forced to choose between:

another feature

OR

making an existing mechanic feel significantly better,

choose the second.

The game should remain small.

The quality should become large.

Start by auditing the existing Unity project through MCP, identify the current architecture and biggest problems, then begin the refinement process immediately.
