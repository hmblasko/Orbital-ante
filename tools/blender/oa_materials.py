"""Surface detail for offline pictures: what textures will add once the game can read them.

The game today draws one colour per corner and nothing finer (no textures). `surface_detail` works cloth weave,
scuffs, stains and worn edges into a preview material from 3D patterns, so a picture can show the same model as it
would look with colour, normal and roughness textures. Nothing here reaches the game: it is for Cycles pictures
only, and every picture made with it must say so.

    oa.render_preview(path, objects, eye, target, finish=oa_materials.surface_detail)
"""
import bpy


def _n(tree, kind, **values):
    node = tree.nodes.new(kind)
    for key, value in values.items():
        if key in node.inputs:
            node.inputs[key].default_value = value
        else:
            setattr(node, key, value)
    return node


def surface_detail(material, weave=0.55, scuff=0.5, stain=0.35):
    """Adds to a material already wired by render_preview (paint from "Col", finish from "Mat" when there is one)."""
    tree = material.node_tree
    link = tree.links.new
    bsdf = tree.nodes["Principled BSDF"]
    place = _n(tree, "ShaderNodeTexCoord")

    def source(socket_name):
        links = bsdf.inputs[socket_name].links
        return links[0].from_socket if links else None

    paint, rough_in, metal_in = source("Base Color"), source("Roughness"), source("Metallic")

    # How much of a surface is cloth: rough and not metal. Cloth gets the weave, hard things get scratches.
    cloth = _n(tree, "ShaderNodeMath", operation='SUBTRACT')
    cloth.use_clamp = True
    if rough_in:
        ramp = _n(tree, "ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.70, 0.88
        link(rough_in, ramp.inputs["Value"])
        link(ramp.outputs[0], cloth.inputs[0])
    else:
        cloth.inputs[0].default_value = 1.0
    if metal_in:
        link(metal_in, cloth.inputs[1])
    else:
        cloth.inputs[1].default_value = 0.0

    # The weave: two fine sets of threads crossing, as plain-woven suit cloth has.
    warp = _n(tree, "ShaderNodeTexWave", wave_type='BANDS', bands_direction='X', Scale=420.0, Distortion=0.6)
    weft = _n(tree, "ShaderNodeTexWave", wave_type='BANDS', bands_direction='Z', Scale=420.0, Distortion=0.6)
    link(place.outputs["Object"], warp.inputs["Vector"])
    link(place.outputs["Object"], weft.inputs["Vector"])
    threads = _n(tree, "ShaderNodeMath", operation='MAXIMUM')
    link(warp.outputs["Fac"], threads.inputs[0])
    link(weft.outputs["Fac"], threads.inputs[1])
    woven = _n(tree, "ShaderNodeMath", operation='MULTIPLY')
    link(threads.outputs[0], woven.inputs[0])
    link(cloth.outputs[0], woven.inputs[1])

    # Scuffs and dents on everything, finer and sharper on hard things.
    fine = _n(tree, "ShaderNodeTexNoise", Scale=95.0, Detail=8.0, Roughness=0.72)
    broad = _n(tree, "ShaderNodeTexNoise", Scale=11.0, Detail=6.0, Roughness=0.62)
    scratch = _n(tree, "ShaderNodeTexVoronoi", feature='DISTANCE_TO_EDGE', Scale=38.0)
    for node in (fine, broad, scratch):
        link(place.outputs["Object"], node.inputs["Vector"])
    cut = _n(tree, "ShaderNodeMapRange")
    cut.inputs["From Min"].default_value, cut.inputs["From Max"].default_value = 0.0, 0.035
    link(scratch.outputs["Distance"], cut.inputs["Value"])
    hard = _n(tree, "ShaderNodeMath", operation='SUBTRACT')
    hard.inputs[0].default_value = 1.0
    link(cloth.outputs[0], hard.inputs[1])
    scored = _n(tree, "ShaderNodeMixRGB", blend_type='MIX')
    scored.inputs["Color1"].default_value = (1, 1, 1, 1)
    link(cut.outputs[0], scored.inputs["Color2"])
    worn = _n(tree, "ShaderNodeMath", operation='MULTIPLY')
    worn.inputs[1].default_value = 0.45 * scuff
    link(hard.outputs[0], worn.inputs[0])
    link(worn.outputs[0], scored.inputs["Fac"])

    height = _n(tree, "ShaderNodeMath", operation='ADD')
    link(woven.outputs[0], height.inputs[0])
    link(fine.outputs["Fac"], height.inputs[1])
    height2 = _n(tree, "ShaderNodeMath", operation='ADD')
    link(height.outputs[0], height2.inputs[0])
    link(scored.outputs[0], height2.inputs[1])
    bump = _n(tree, "ShaderNodeBump", Strength=0.30 + 0.5 * weave, Distance=0.0016)
    link(height2.outputs[0], bump.inputs["Height"])
    link(bump.outputs["Normal"], bsdf.inputs["Normal"])

    # Stains and fading: the paint goes a little darker and lighter in broad soft patches, darker in the scratches.
    if paint:
        patch = _n(tree, "ShaderNodeMapRange")
        patch.inputs["From Min"].default_value, patch.inputs["From Max"].default_value = 0.30, 0.70
        patch.inputs["To Min"].default_value, patch.inputs["To Max"].default_value = 1.0 - 0.40 * stain, 1.0 + 0.22 * stain
        link(broad.outputs["Fac"], patch.inputs["Value"])
        thread_shade = _n(tree, "ShaderNodeMapRange")
        thread_shade.inputs["To Min"].default_value, thread_shade.inputs["To Max"].default_value = 0.86, 1.06
        link(woven.outputs[0], thread_shade.inputs["Value"])
        shade = _n(tree, "ShaderNodeMath", operation='MULTIPLY')
        link(patch.outputs[0], shade.inputs[0])
        link(thread_shade.outputs[0], shade.inputs[1])
        stained = _n(tree, "ShaderNodeMixRGB", blend_type='MULTIPLY')
        stained.inputs["Fac"].default_value = 1.0
        link(paint, stained.inputs["Color1"])
        link(shade.outputs[0], stained.inputs["Color2"])
        link(stained.outputs[0], bsdf.inputs["Base Color"])

    # Roughness breaks up too: polished where handled, dull where dusty.
    if rough_in:
        vary = _n(tree, "ShaderNodeMapRange")
        vary.inputs["To Min"].default_value, vary.inputs["To Max"].default_value = -0.10, 0.12
        link(fine.outputs["Fac"], vary.inputs["Value"])
        rougher = _n(tree, "ShaderNodeMath", operation='ADD')
        rougher.use_clamp = True
        link(rough_in, rougher.inputs[0])
        link(vary.outputs[0], rougher.inputs[1])
        link(rougher.outputs[0], bsdf.inputs["Roughness"])
    return material
