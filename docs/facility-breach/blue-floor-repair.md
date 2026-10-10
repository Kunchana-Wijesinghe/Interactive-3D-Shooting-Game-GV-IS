# Blue-floor repair — Unity 6000.6.2f1

The cyan triangles are Unity's native NavMesh visualization, controlled by `UnityEditor.AI.NavMeshVisualizationSettings.showNavMesh`. They are not a damaged floor material or a runtime floor renderer. `FB_Gameplay` loads `Assets/Navigation/FacilityBreach/PrototypeGameplayNavMesh.asset` through `FacilityBreachRuntime.Start`; Unity's Editor draws its navigation polygons when visualization is enabled. The navigation data and movement components remain active.

The previous `EnemyNavigationDebug.cs` workaround changed `showNavigation`. Inspection of the installed Unity assemblies confirms that this obsolete public property is a managed backing-field counter. It does not switch the native visualization off. The real `showNavMesh` property is internal in this Unity version, explaining the earlier CS0117 errors when accessed directly.

## Diagnosis and evidence

1. The saved Editor layout had Game-view Gizmos enabled. The user initially reported neither blue surfaces nor light icons disappearing when toggling Gizmos. A controlled real Game-view render reproduced the same cyan triangles with Show NavMesh and Gizmos enabled. Disabling Gizmos removed both in that isolated instance, but left the native Show NavMesh setting enabled. More decisively, switching only Show NavMesh off with Gizmos still enabled removed the cyan triangles while retaining the light icons. The user subsequently confirmed that **Facility Breach → Restore normal view** removed the blue overlay in the main Game view.
2. Custom hearing-node gizmos draw small spheres/wire boxes; links and vision draw lines. The current hearing graph and decision/navigation scripts create no hearing or vision render meshes. Protected Perera code was read only.
3. Play Mode renderer inspection found the existing bounded gameplay props/models, no runtime LineRenderers, and no floor-sized cyan debug renderer. No renderer was disabled to achieve this fix.
4. `Facility_Floor` uses `Facility_RoughConcrete_Floor`, the built-in Standard shader, its original diffuse/normal/height/AO textures, opaque Mode 0, ZWrite 1 and tint (0.32, 0.34, 0.36, 1). Its object, transform, MeshFilter, MeshRenderer and BoxCollider blocks and material file match world-builder revision `b7c1425`.
5. Runtime material writes target newly created gameplay props or cloned enemy light materials. No floor renderer recoloring or MaterialPropertyBlock modification was found.
6. There is one `Facility_Floor`. All 1,193 scene MeshRenderers match the world-builder revision, with no additional or changed scene renderer blocks. No confirmed duplicate floor overlay exists, so no geometry was removed.

## Exact change

Only production file `Assets/Editor/FacilityBreach/EnemyNavigationDebug.cs` changed. It uses a cached reflection accessor for the verified internal `showNavMesh` property instead of unsupported direct properties or the ineffective deprecated counter. Normal view disables native NavMesh drawing on reload, in both Play/Edit transitions, and during Editor updates. Only the visualization preference changes; no scene serialization, floor material, renderer, collider, NavMesh data, sensor or movement component changes.

- **Facility Breach → Restore normal view** disables the cyan navigation display.
- **Facility Breach → Debug → Toggle navigation overlay** explicitly enables/disables it for debugging for the current Editor session.
- Unity's underlying setting is **Scene view → AI Navigation overlay → Surfaces → Show NavMesh**. While normal view is selected, the script keeps this off; use the debug menu to enable it deliberately.
- **Game view → Gizmos** controls Editor icons and other gizmos. Turn this off separately if light icons are unwanted. The repair does not remove scene lights.

This depends on the verified Unity 6000.6.2f1 internal property. If a future Unity version removes it, a single warning points to the manual toggle. No package internals are modified.

## Verification and remaining limits

Final isolated-copy Unity runs used a real graphics device and the current production script. Two Edit Mode checks and nineteen Play Mode checks passed. Render evidence demonstrates the original textured floor in Edit Mode, the cyan overlay with navigation visualization enabled, its removal with Gizmos either on or off, and normal rendering after an actual scene Restart. Start, pause and resume retained the native visualization-off state. The original floor collider accepted a collision ray; gameplay NavMesh triangulation remained loaded and a complete PG patrol path was found before/after the display fix. Restart reloaded the actual scene and retained navigation and textured floor.

No C# errors or unexpected gameplay Console errors occurred in the final run. Unity emitted an internal JobTempAlloc warning during Editor shutdown; this is recorded as a validation-environment limitation rather than claimed as a clean shutdown. Earlier audit startup attempts were interrupted by local licensing/low-disk dialogs and one corrected temporary audit type-resolution error; they are not counted as successful verification.

223 protected/authored files were checked byte-for-byte, including all 86 Perera files/meta, scene files, authored materials and world-builder Editor scripts. `Prototype.unity` was not edited. No environment-generation command ran. Unrelated changes in `FacilityBreachRuntime.cs`, `standalone/Checks.csproj` and the untracked `PackageManagerSettings.asset` were retained and excluded from this fix.

No standalone executable was found outside Library/Temp, so a standalone build was not inspected. The build excludes this Editor-only visualization script. A new full keyboard/mouse level playthrough, physical barricade pushing, every door transition, toxic-waste collision and an actual Scene-view framebuffer capture were not performed. Those authored systems are byte-preserved; this change only controls Editor drawing. Player collision verification here was a collider ray test, and enemy navigation verification was loaded data/path calculation rather than a timed movement walkthrough.

Evidence and temporary audit source are in [evidence/blue-floor](evidence/blue-floor). The temporary audit was executed only in the isolated project copy and never added to the playable scene.
