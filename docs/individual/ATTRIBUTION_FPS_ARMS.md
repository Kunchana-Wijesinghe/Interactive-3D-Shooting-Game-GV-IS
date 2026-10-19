# 3D Asset Attribution & License Documentation

## Asset Information
- **Asset Name:** First Person arms
- **Creator / Author:** DJMaesen
- **Source Link:** [Sketchfab - First Person arms](https://sketchfab.com/3d-models/first-person-arms-e3c42c05b22944e5839deb8e003f0987)
- **License:** Creative Commons Attribution (CC BY 4.0)
- **License Deed:** [https://creativecommons.org/licenses/by/4.0/](https://creativecommons.org/licenses/by/4.0/)
- **Legal Code:** [https://creativecommons.org/licenses/by/4.0/legalcode](https://creativecommons.org/licenses/by/4.0/legalcode)

---

## License Notice & Permissions
Under the terms of the Creative Commons Attribution 4.0 International License:
- **Share:** You are free to copy and redistribute the material in any medium or format.
- **Adapt:** You are free to remix, transform, and build upon the material for any purpose, even commercially.
- **Attribution:** Appropriate credit must be given, a link to the license provided, and an indication of changes made.

---

## Modifications Applied for FacilityBreach Integration
1. **Asset Structure & Packaging:**
   - Copied `fpsarms.fbx` into `Assets/Models/FacilityBreach/Arms/fpsarms.fbx`.
   - Copied source textures (`armAO.png`, `armColor.png`, `armnormal.png`, `armRoughness.png`) into `Assets/Textures/FacilityBreach/Arms/`.
   - Preserved original source files intact in downloads directory.
2. **Tactical Glove Albedo Derivation:**
   - Authored `armColor_TacticalGloves.png` from `armColor.png` by converting skin tones to dark tactical charcoal tones (`#25282A` - `#303438`) while preserving original fabric sleeve textures, shading, and micro-details.
3. **Unity 6 PBR Material:**
   - Created `FPSArms_Mat.mat` utilizing Unity Standard Shader with tactical albedo, normal map, ambient occlusion, and matched roughness/smoothness properties.
4. **First-Person Viewmodel Integration:**
   - Bound tactical-gloved arms to the FPS weapon viewmodels (`Ammo1gun_ViewModel`, `Ammo2gun_ViewModel`, `Ammo3gun_ViewModel`) aligned with weapon `GripAttachment` and `SupportHand` reference sockets.
   - Preserved procedural obstruction modulation (`FpsViewModelObstruction`), zero collider interference, and all existing weapon mechanics.
