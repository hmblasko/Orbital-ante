# Model clean-up scripts (glTF-Transform)

Used to bring NASA 3D Resources models to the Steam Deck budgets (CLAUDE.md section 11).

Setup, once, in an empty folder outside the repo:

    npm init -y && npm i @gltf-transform/cli@4.1.1

Steps used for each model (run from that folder; copy the two .mjs files there):

1. `npx gltf-transform optimize IN.glb step1.glb --compress false --texture-compress false --simplify-ratio 1 --instance false`
   (decodes Draco, which Godot can't import, and dedups)
2. `npx gltf-transform resize step1.glb step2.glb --width 1024 --height 1024`
3. `node clean_model.mjs step2.glb OUT.glb RATIO "" ERROR`
   strips animations, bakes transforms, merges parts into one mesh per material, simplifies.
   Ratios used: lander 0.5 (error 0.01), Viking 0.8, dish 0.8 (0.003), Voyager 0.7, others 1.0.
4. Lander only: `node replace_textures.mjs OUT.glb CLEAN.glb gold_foil.png TEXTUREA TEXTURE_`
   replaces the flag and lettering textures (no flags or national insignia in the game).

Always check every texture for logos before committing (section 15).
