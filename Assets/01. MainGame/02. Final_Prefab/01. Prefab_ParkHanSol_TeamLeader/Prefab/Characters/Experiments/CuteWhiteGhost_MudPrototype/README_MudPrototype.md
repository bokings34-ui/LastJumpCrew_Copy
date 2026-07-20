# CuteWhiteGhost Mud Prototype

## Scope

- Original `CuteWhiteGhost` folder must stay untouched.
- `.meta` files were not copied, so Unity should generate new GUIDs for this prototype.
- Prototype target: test a Fall Guys-like soft, squishy material/body feel around the copied ghost asset.
- Current player prefab material target: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/ParkHanSol_NetworkPlayer.prefab`.

## Source Assets

- Model copy: `ParkHanSol_CuteWhiteGhost_MudPrototype_FromGLB.fbx`
- Texture copy: `Textures/cute_white_ghost_mudprototype_basecolor.jpg`
- Controller copy: `AnimationsFromGLB/ParkHanSol_CuteWhiteGhost_MudPrototype.controller`

## Next Check

1. Let Unity import this folder.
2. Confirm generated `.meta` files have no GUID conflict warning.
3. Check the copied Animator Controller clip references. They may still point to the original FBX clips by GUID.
4. Use `MudMetaballVolume` only as an isolated shape experiment.
5. Tune actual player feel through the `ParkHanSol_CuteWhiteGhost_SquishyPlayer` material on `Ghost_Body`.
