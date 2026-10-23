# Jump and Run

A small 2.5D platformer the user is building to **learn Unity**. When you change code, explain the Unity idea behind it (for example interpolation or why physics runs in FixedUpdate). Don't just make silent fixes.

## Setup
- Unity **6000.6.2f1** (Unity 6), URP 17.6, new **Input System** 1.20 (read directly with `Keyboard.current` / `Gamepad.current`, so the `InputSystem_Actions.inputactions` asset isn't wired up yet).
- Code comments, Inspector headers and tooltips are in **German**. Keep it that way.
- Scenes: `Assets/Scenes/levels/level01.unity`, `level02.unity` (`SampleScene` is the template leftover).
- Physics: 50 Hz fixed timestep (0.02), default gravity -9.81. The player turns off Rigidbody gravity and applies its own.
- Layer 3 = `Ground`, the player's `groundLayer` for ground checks. The player has the `Player` tag.

## Gameplay structure
- **2.5D**: the level runs along +X. The player Rigidbody has **position Z frozen** and all rotation frozen. `PrefabGridEnforcer` (editor) snaps prefabs to a 1x1 grid at Z = 0.
- `PlayerController`: Rigidbody movement (velocity set directly), Celeste/Hollow-Knight-style jump: gravity derived from `jumpHeight`/`timeToApex`, stronger gravity when falling, jump cut on release, apex hang, coyote time and jump buffer. Powerups call `ApplySpeedBoost` / `ApplyJumpBoost` (the latter multiplies jump velocity).
- `ThirdPersonCamera`: follows the player in `LateUpdate` with a world-space offset, separate SmoothDamp for horizontal and vertical, fixed rotation.
- `PointToPointMover`: the shared A↔B movement (used directly by the **Flying Enemy** prefab). `MovingPlatform` is an empty subclass of it, so the player recognizes platforms. Movement details:
- `MovingPlatform`: kinematic Rigidbody, moves A↔B with ease in/out (`easingStrength`, peak speed = `speed` × strength; `speed` = average speed), waits `waitTime` at each end, starts from its editor position (projected onto A→B, heading toward B first), and only waits at start if it sits on an endpoint. A/B are children of the platform (`moving platform start/end`) and their positions are read once in `Start`. It does **not** move passengers itself; it exposes `Velocity`, and `PlayerController` adds that velocity to its own (`carryVelocity` stays while airborne, so jumps keep platform momentum; rising platforms add their upward speed to the jump). Never move the dynamic player with `MovePosition`/teleporting, because that fights the physics and kills jumps.
- `PowerUpShine` (on the Player prefab) + `Assets/Shaders/PowerUpShine.shader` (hand-written URP HLSL, additive fresnel rim + sweeping stripe): while a boost is active, it attaches the shine material as an extra material on the renderer, colored per power-up type, and blinks shortly before the boost runs out. `PlayerController.ApplySpeedBoost/ApplyJumpBoost` trigger it.
- Prefabs are capitalized (`Platform Big`, `Moving Platform Small`, `Enemy`, `Checkpoint`, `Goal`, `PowerUp`, ...). Paths in editor scripts must match exactly.
- Enemy prefabs: `Enemy` (red, `EnemyPlatformPatrol`, needs `WaypointA/B` on its platform) and `Flying Enemy` (purple, `PointToPointMover` between its children `Point A`/`Point B`). Both use `EnemyDamage`.
- `EnemyDamage`: a contact from above (direction to player `y >= stompDirectionThreshold`, player not rising) is a **stomp**. The enemy is disabled immediately (collider plus all other scripts), squashes flat and is destroyed, and `PlayerController.StompBounce()` bounces the player (higher while jump is held). Any other contact calls `GameManager.RespawnPlayer()`.
- **Level reset on respawn**: `GameManager.RespawnPlayer()` raises the static event `GameManager.LevelReset`. Subscribers (subscribe in `Awake`, unsubscribe in `OnDestroy`): `EnemyDamage` revives squashed enemies, `EnemyPlatformPatrol` and `PointToPointMover` go back to their start positions, `PowerUp` reactivates (`respawnOnLevelReset`), and `PlayerController` clears boosts, shine and platform momentum. Enemies and power-ups are therefore only **deactivated** (`SetActive(false)`), never `Destroy`ed. Keep it that way for new resettable objects.
- Other scripts: `GameManager` (singleton, level timer, respawn), `Checkpoint`, `KillZone`, `LevelGoal`, `PowerUp`, `EnemyPlatformPatrol`, `TimerDisplay`.
- Editor tools under **Tools > Jump and Run**: `ObstacleCourseBuilder` generates the levels from `Level1Layout`/`Level2Layout` (gaps measured from edge to edge). `LevelEssentialsSetup` copies Player/GameManager into level 2. `LevelCleaner` (**Clear Level (active scene)**) deletes platforms, enemies, checkpoints and waypoints but keeps Player, StartPoint, GameManager, camera, Goal and KillZone. The levels are hand-built (as of 2026-10-02): level 1 is finished, and level 2 was reset from a copy of level 1 (course removed; Player/GameManager/Goal are prefab instances). The `ObstacleCourseBuilder` layouts are outdated. Don't run them, because they would overwrite the hand-built level.

## Pitfalls
- Jump reach limits level design. Right now that's about 3.2 m high and about 5.5 m far at `moveSpeed` 7. If you change the jump or moveSpeed, check the gaps in `ObstacleCourseBuilder` (level 2 has 5.5 m gaps with a 1 m step up).
- The player collider gets a **zero-friction PhysicsMaterial** in `Awake`. With the strong custom gravity, default friction (0.6) almost cancelled out walking acceleration. Don't remove it.
- "Grounded" for jumping = ground check hit + no jump in the last 0.1 s (`GroundLockAfterJump`). **Don't** add a vertical-velocity condition. On moving platforms the player's vy never exactly matches the platform's (easing, solver), so jumps got dropped.
- The player Rigidbody needs **Interpolate** (set on the prefab). Without it the camera jitters, because physics runs at 50 Hz and rendering doesn't.
- Serialized values in prefabs/scenes override code defaults. If you change a default in code, also check the prefab (`Assets/Prefabs/Player.prefab`) and scene overrides.
- Verify it compiles without the editor: `/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet build Assembly-CSharp.csproj` (the CS0649 warnings for `[SerializeField]` are normal). Game feel can only be tested in the editor, so ask the user.
- Not committed yet: the repo has no `.gitignore`. `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj` and `*.slnx` should not be committed.
