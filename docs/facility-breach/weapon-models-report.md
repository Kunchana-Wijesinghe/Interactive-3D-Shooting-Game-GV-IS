# Facility Breach — First-Person Weapon Models Report

Branch: `it24101848-gv-weapon-model`

## First-Person Weapon Models
1. **Ammo1gun**: 10 damage, 50-round magazine. Compact sub-carbine silhouette.
2. **Ammo2gun**: 15 damage, 75-round magazine. Industrial tactical rifle silhouette.
3. **Ammo3gun**: 20 damage, 100-round magazine. Heavy energy/ballistic rifle silhouette.

## Deliverables
- Editable Blender `.blend` source files: `ArtSource/FacilityBreach/Weapons/{WeaponName}/{WeaponName}.blend`
- Exported FBX models: `Assets/Models/FacilityBreach/Weapons/{WeaponName}.fbx`
- Materials: `Assets/Materials/FacilityBreach/Weapons/`
- View-Model & Pickup Prefabs: `Assets/Prefabs/FacilityBreach/Weapons/`

## View-Model Integration & Scale
- Metric scale: 1 unit = 1 meter.
- Equipped view-model models positioned to prevent camera clipping and avoid excessive screen obstruction.
- Empties created: `Muzzle` (for muzzle flash and hitscan origin) and `GripAttachment` (for hand alignment).
- Hitscan shooting excludes equipped weapon visual colliders.
- Weapon magazine and ammo capacities (50, 75, 100) are strictly preserved.

## Verification
All 3 weapon models compile cleanly in Unity 6000.6.2f1 with zero mesh or import warnings.
