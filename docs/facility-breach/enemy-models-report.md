# Facility Breach — Security Robot Visual Models Report

Branch: `it24101848-enemy-model`

## Security Robot Visual Family
1. **Patrol Guard (PG-01)**: Teal accent. Readable optical sensor and dome head silhouette.
2. **Sound Hunter (SH-01)**: Orange accent. Visible auditory perception discs and directional sensor arrays.
3. **Cover Defender (CD-03)**: Violet accent. Heavier defensive armor silhouette with reinforced plating.
4. **Flanker (FL-04)**: Lime accent. Lightweight, agile frame silhouette built for flanking movement.

## Deliverables
- Editable Blender `.blend` source files: `ArtSource/FacilityBreach/Enemies/{RobotName}/{RobotName}.blend`
- Exported FBX models: `Assets/Models/FacilityBreach/Enemies/{RobotName}.fbx`
- Materials: `Assets/Materials/FacilityBreach/Enemies/`
- Visual Prefabs: `Assets/Prefabs/FacilityBreach/Enemies/`

## Modeling & Scale Standards
- Metric scale: 1 unit = 1 meter (~1.7m standing height).
- Clean pivot origin at floor center. Applied transforms.
- Forward axis: Unity forward (+Z).
- Rigged skeleton with state light indicator bone on chest (Blue: patrol, Yellow: investigate/search, Red: combat, White: pre-shot warning, Off: inactive).

## Verification
All 4 models compile without errors in Unity 6000.6.2f1 with zero model mesh or transform warnings.
