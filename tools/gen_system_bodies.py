#!/usr/bin/env python3
"""Writes the Phase 3 body files in godot/data/bodies/system/.

The RON files are the data (tune them directly); this script only makes the
first version consistent. Re-running it overwrites hand edits, so after
tuning, edit the RON files and leave this script alone.
"""
import os

OUT = os.path.join(os.path.dirname(__file__), "..", "godot", "data", "bodies", "system")

def c(t):
    return "(%.2f, %.2f, %.2f)" % t

def landforms(water, clear, groups):
    rows = []
    for g in groups:
        kind, count, rad, hgt = g[:4]
        length = g[4] if len(g) > 4 else None
        row = "(kind: %s, count: %d, radius_m: (%.1f, %.1f), height_m: (%.1f, %.1f)" % (kind, count, rad[0], rad[1], hgt[0], hgt[1])
        if length:
            row += ", length_m: (%.1f, %.1f)" % length
        rows.append("                " + row + "),")
    w = "Some(%.1f)" % water if water is not None else "None"
    return """        landforms: (
            water_level_m: %s,
            clear_zone_deg: %.1f,
            random: [
%s
            ],
        ),""" % (w, clear, "\n".join(rows))

def rocky(b):
    r = b["radius"]
    sub = b.get("sub", 384)
    lod = b.get("lod")
    lod_txt = ""
    if lod:
        lod_txt = """    lod: Some((
        near_subdivisions: %d,
        far_subdivisions: %d,
        tiles: %d,
        near_range_m: %.1f,
    )),
""" % lod
    air = b.get("air")
    air_txt = "    air_glow: Some((color: %s, height_m: %.1f, strength: %.2f)),\n" % (c(air[0]), air[1], air[2]) if air else ""
    atmo = b.get("atmo")
    atmo_txt = "    atmosphere: Some((breathable: %s, height_m: %.1f)),\n" % ("true" if atmo[0] else "false", atmo[1]) if atmo else ""
    clouds = b.get("clouds")
    clouds_txt = ""
    if clouds:
        clouds_txt = """    clouds: Some((
        radius_m: %.1f,
        band_a: %s,
        band_b: %s,
        band_c: %s,
        band_d: %s,
        bands: %.1f,
        turbulence: %.2f,
        storm_latitude_deg: %.1f,
        storm_size: %.2f,
        storm_color: %s,
        ring_m: (%.1f, %.1f),
        ring_color: %s,
    )),
""" % (clouds["radius"], c(clouds["a"]), c(clouds["b"]), c(clouds["c"]), c(clouds["d"]), clouds["bands"], clouds["turb"],
       clouds.get("storm_lat", 0.0), clouds.get("storm", 0.0), c(clouds.get("storm_color", (0, 0, 0))),
       clouds.get("ring", (0, 0))[0], clouds.get("ring", (0, 0))[1], c(clouds.get("ring_color", (0, 0, 0))))
    look = b["look"]
    land = b.get("land", {})
    t = b["terrain"]
    hoodoos = b.get("hoodoos")
    hoodoo_txt = ""
    if hoodoos:
        hoodoo_txt = """    hoodoos: Some((
        clusters_per_mesa: 2,
        clusters_per_canyon: 2,
        per_cluster: (3, 6),
        cluster_spread_m: 12.0,
        height_m: (5.0, 13.0),
        variants: 6,
        band_a: %s,
        band_b: %s,
        cap: %s,
        visible_m: 600.0,
    )),
""" % (c(hoodoos[0]), c(hoodoos[1]), c(hoodoos[2]))
    return """// %s (section 4 roster, Phase 3 first pass). %s
// Generated once by tools/gen_system_bodies.py; tune this file directly.
(
    name: "%s",
    radius: %.1f,
    gravity: (
        surface_gravity: %.1f,
        influence_radius: %.1f,
    ),
%s    terrain: (
        seed: %d,
        subdivisions: %d,
        hill_height_m: %.1f,
        hill_frequency: %.1f,
        hill_octaves: 6,
        ridge_height_m: %.1f,
        ridge_frequency: %.1f,
        crater_count: %d,
        crater_radius_m: (%.1f, %.1f),
        crater_depth_ratio: %.2f,
        crater_rim_ratio: 0.35,
%s
    ),
    look: (
        color_low: %s,
        color_high: %s,
        color_crater: %s,
        color_rim: %s,
        color_slope: %s,
        shadow_tint: %s,
        rim_color: %s,
        rim_strength: 0.3,
        detail_scale: 0.02,
        grain_scale: 0.15,
    ),
    land_look: (
        strata_a: %s,
        strata_b: %s,
        basalt: %s,
        lava: (1.00, 0.45, 0.10),
        lava_energy: %.1f,
        lava_light_range_m: %.1f,
        snow: %s,
        wet: %s,
        mineral: %s,
        water_shallow: %s,
        water_deep: %s,
        water_foam: (0.95, 0.98, 1.00),
        water_subdivisions: 64,
        frozen: %s,
    ),
    rocks: (
        count: %d,
        size_m: (0.4, 3.2),
        cluster_size: (2, 6),
        cluster_spread_m: 8.0,
        roughness: 0.32,
        color: %s,
        color_dark: %s,
        visible_m: 220.0,
    ),
%s    render_tiles: %d,
%s    preview_subdivisions: %d,
%s%s)
""" % (b["title"], b["note"], b["title"], r, b["g"], b["influence"], atmo_txt, b["seed"], sub,
       t["hills"], t["hill_f"], t["ridges"], t["ridge_f"], t["craters"], t["crater_r"][0], t["crater_r"][1], t.get("crater_depth", 0.18),
       landforms(t.get("water"), t.get("clear", 4.0), t["landforms"]),
       c(look["low"]), c(look["high"]), c(look["crater"]), c(look["rim"]), c(look["slope"]), c(look.get("shadow", (0.36, 0.36, 0.62))), c(look.get("rim_light", (0.60, 0.70, 1.0))),
       c(land.get("strata_a", look["slope"])), c(land.get("strata_b", look["crater"])), c(land.get("basalt", (0.22, 0.20, 0.22))),
       land.get("lava_energy", 0.0), land.get("lava_light", 0.0),
       c(land.get("snow", (0.92, 0.94, 0.98))), c(land.get("wet", look["crater"])), c(land.get("mineral", (0.90, 0.88, 0.82))),
       c(land.get("shallow", (0.25, 0.70, 0.75))), c(land.get("deep", (0.05, 0.22, 0.40))), "true" if land.get("frozen") else "false",
       b.get("rocks", 600), c(look["rock"]), c(look["rock_dark"]), hoodoo_txt,
       b.get("tiles", 8), lod_txt, b.get("preview", 64), clouds_txt, air_txt)

def star(b):
    return """// %s (section 4 roster, Phase 3 first pass). %s
// Generated once by tools/gen_system_bodies.py; tune this file directly.
(
    name: "%s",
    radius: %.1f,
    gravity: (
        surface_gravity: %.1f,
        influence_radius: %.1f,
    ),
    star: Some((
        core: %s,
        edge: %s,
        energy: %.1f,
    )),
)
""" % (b["title"], b["note"], b["title"], b["radius"], b["g"], b["influence"], c(b["core"]), c(b["edge"]), b["energy"])

GREY_ROCK = dict(rock=(0.55, 0.53, 0.52), rock_dark=(0.30, 0.29, 0.29))

BODIES = {
"sun": dict(kind="star", title="Sun", note="Lethal heat close in (heat damage comes in a later phase).",
    radius=1200, g=14.0, influence=4200, core=(1.0, 0.93, 0.74), edge=(1.0, 0.52, 0.16), energy=3.2),
"mercury": dict(title="Mercury", note="Battered and cratered; slow spin so you can outrun the terminator line.",
    radius=420, g=4.5, influence=1000, seed=1101, sub=256, rocks=500,
    terrain=dict(hills=4, hill_f=9, ridges=4, ridge_f=6, craters=160, crater_r=(4, 40), crater_depth=0.22,
        landforms=[("Crater", 8, (40, 90), (8, 14)), ("Ridge", 6, (10, 16), (6, 10), (250, 500)), ("Sinkhole", 3, (8, 12), (12, 18))]),
    look=dict(low=(0.52, 0.49, 0.47), high=(0.70, 0.67, 0.63), crater=(0.40, 0.37, 0.36), rim=(0.82, 0.78, 0.72), slope=(0.46, 0.42, 0.40), **GREY_ROCK)),
"venus": dict(title="Venus", note="Volcanic plains under thick yellow air; Faction A home (bases come later).",
    radius=640, g=8.5, influence=1500, seed=1201, sub=384, rocks=800, lod=(512, 192, 8, 200),
    air=((1.0, 0.78, 0.42), 70, 1.4),
    terrain=dict(hills=5, hill_f=10, ridges=4, ridge_f=6, craters=25, crater_r=(8, 30),
        landforms=[("Volcano", 10, (70, 130), (40, 75)), ("Mountain", 6, (60, 100), (35, 60)), ("Ridge", 8, (12, 20), (6, 12), (300, 700)), ("Crater", 3, (50, 80), (8, 12)), ("Mesa", 4, (30, 50), (16, 28))]),
    look=dict(low=(0.62, 0.45, 0.28), high=(0.80, 0.64, 0.42), crater=(0.50, 0.35, 0.22), rim=(0.86, 0.72, 0.50), slope=(0.50, 0.33, 0.22),
        rock=(0.48, 0.36, 0.26), rock_dark=(0.26, 0.19, 0.15)),
    land=dict(basalt=(0.28, 0.22, 0.20), lava_energy=4.0, lava_light=60)),
"earth": dict(title="Earth", note="Your home base and launch pad (they arrive with the home base work).",
    radius=700, g=9.8, influence=1700, seed=1301, sub=384, rocks=900, lod=(512, 192, 8, 200),
    air=((0.45, 0.70, 1.0), 80, 1.2), atmo=(True, 80),
    terrain=dict(hills=5, hill_f=11, ridges=5, ridge_f=7, craters=4, crater_r=(10, 25), water=-3.0, clear=5,
        landforms=[("Mountain", 18, (60, 110), (35, 70)), ("Lake", 9, (90, 200), (6, 12)), ("River", 10, (4, 6), (0, 0), (300, 600)),
                   ("Canyon", 3, (14, 20), (12, 20), (400, 800)), ("Mesa", 5, (25, 45), (18, 30)), ("Pool", 6, (5, 8), (1.2, 1.8))]),
    look=dict(low=(0.38, 0.54, 0.30), high=(0.56, 0.64, 0.40), crater=(0.42, 0.46, 0.30), rim=(0.70, 0.70, 0.52), slope=(0.50, 0.44, 0.34),
        rim_light=(0.60, 0.78, 1.0), rock=(0.56, 0.54, 0.50), rock_dark=(0.30, 0.30, 0.28)),
    land=dict(strata_a=(0.66, 0.52, 0.40), strata_b=(0.52, 0.40, 0.32), snow=(0.94, 0.96, 1.0), wet=(0.30, 0.36, 0.24), shallow=(0.25, 0.70, 0.75), deep=(0.05, 0.22, 0.42))),
"moon": dict(title="Moon", note="Crater driving and far-side outposts; the vertical-slice location.",
    radius=260, g=3.0, influence=620, seed=1401, sub=256, rocks=450,
    terrain=dict(hills=3, hill_f=8, ridges=3, ridge_f=5, craters=140, crater_r=(3, 28), crater_depth=0.22,
        landforms=[("Crater", 6, (30, 60), (6, 10)), ("Ridge", 4, (8, 12), (4, 7), (150, 300)), ("Sinkhole", 3, (5, 9), (8, 14))]),
    look=dict(low=(0.44, 0.44, 0.46), high=(0.72, 0.71, 0.70), crater=(0.36, 0.36, 0.38), rim=(0.82, 0.81, 0.79), slope=(0.52, 0.51, 0.51), **GREY_ROCK)),
"mars": dict(title="Mars", note="Dust, a giant canyon and rover country; Faction B home (bases come later).",
    radius=520, g=6.0, influence=1250, seed=1501, sub=384, rocks=900, lod=(512, 192, 8, 200),
    air=((0.95, 0.60, 0.40), 50, 0.8),
    terrain=dict(hills=4, hill_f=10, ridges=4, ridge_f=6, craters=70, crater_r=(4, 30), clear=5,
        landforms=[("Canyon", 3, (18, 26), (14, 24), (500, 900)), ("Volcano", 3, (90, 140), (50, 80)), ("Dunes", 5, (80, 140), (5, 9)),
                   ("Crater", 6, (40, 90), (6, 14)), ("Mesa", 8, (25, 50), (16, 30)), ("Butte", 10, (12, 22), (25, 42))]),
    look=dict(low=(0.68, 0.38, 0.24), high=(0.82, 0.56, 0.38), crater=(0.52, 0.28, 0.19), rim=(0.88, 0.66, 0.48), slope=(0.56, 0.29, 0.19),
        rock=(0.58, 0.34, 0.24), rock_dark=(0.32, 0.18, 0.13)),
    land=dict(strata_a=(0.80, 0.46, 0.30), strata_b=(0.60, 0.30, 0.20), snow=(0.86, 0.72, 0.60), lava_energy=2.0, lava_light=40),
    hoodoos=((0.86, 0.52, 0.34), (0.74, 0.46, 0.32), (0.48, 0.30, 0.24))),
"phobos": dict(title="Phobos", note="A small lumpy moon of Mars with one huge crater.",
    radius=150, g=1.5, influence=380, seed=1601, sub=192, rocks=200,
    terrain=dict(hills=4, hill_f=4, ridges=3, ridge_f=4, craters=60, crater_r=(3, 16), crater_depth=0.24,
        landforms=[("Crater", 1, (55, 60), (8, 9))]),
    look=dict(low=(0.40, 0.36, 0.33), high=(0.56, 0.51, 0.46), crater=(0.32, 0.29, 0.27), rim=(0.64, 0.58, 0.52), slope=(0.38, 0.33, 0.30), **GREY_ROCK)),
"jupiter": dict(title="Jupiter", note="Fly through the cloud deck to a deep inner surface (filled out in Phase 5).",
    radius=1100, g=12.0, influence=5200, seed=1701, sub=384, rocks=600, tiles=8,
    clouds=dict(radius=2200, a=(0.86, 0.76, 0.62), b=(0.72, 0.50, 0.36), c=(0.95, 0.91, 0.82), d=(0.60, 0.44, 0.34), bands=16, turb=0.55,
        storm_lat=-22, storm=0.22, storm_color=(0.82, 0.42, 0.28)),
    terrain=dict(hills=6, hill_f=8, ridges=5, ridge_f=5, craters=0, crater_r=(5, 10),
        landforms=[("Mountain", 10, (80, 140), (40, 80)), ("Mesa", 6, (40, 70), (20, 35))]),
    look=dict(low=(0.48, 0.36, 0.28), high=(0.64, 0.50, 0.38), crater=(0.40, 0.30, 0.24), rim=(0.70, 0.58, 0.46), slope=(0.42, 0.30, 0.24),
        rock=(0.44, 0.34, 0.28), rock_dark=(0.24, 0.18, 0.15))),
"io": dict(title="Io", note="Sulphur-yellow and covered in volcanoes.",
    radius=320, g=3.5, influence=700, seed=1801, sub=256, rocks=350,
    terrain=dict(hills=3, hill_f=8, ridges=3, ridge_f=5, craters=0, crater_r=(3, 8),
        landforms=[("Volcano", 9, (35, 60), (18, 32)), ("Mountain", 4, (40, 60), (25, 40)), ("Mesa", 4, (18, 30), (10, 18))]),
    look=dict(low=(0.84, 0.76, 0.36), high=(0.94, 0.90, 0.62), crater=(0.70, 0.55, 0.26), rim=(0.96, 0.92, 0.70), slope=(0.72, 0.52, 0.26),
        rock=(0.62, 0.52, 0.30), rock_dark=(0.34, 0.26, 0.16)),
    land=dict(basalt=(0.24, 0.18, 0.14), lava_energy=4.5, lava_light=35)),
"europa": dict(title="Europa", note="Cracked ice over an ocean; Faction C home (bases come later).",
    radius=360, g=3.0, influence=760, seed=1901, sub=256, rocks=250,
    terrain=dict(hills=2, hill_f=8, ridges=4, ridge_f=9, craters=8, crater_r=(4, 14), water=-0.8,
        landforms=[("Ridge", 16, (5, 9), (3, 6), (200, 450)), ("Canyon", 2, (6, 9), (5, 8), (200, 350)), ("Lake", 4, (25, 45), (2, 3)), ("Pool", 5, (4, 7), (1, 1.5))]),
    look=dict(low=(0.80, 0.78, 0.72), high=(0.93, 0.93, 0.91), crater=(0.70, 0.62, 0.54), rim=(0.96, 0.96, 0.95), slope=(0.66, 0.50, 0.40),
        shadow=(0.30, 0.38, 0.68), rim_light=(0.66, 0.82, 1.0), rock=(0.82, 0.84, 0.86), rock_dark=(0.50, 0.54, 0.60)),
    land=dict(strata_a=(0.70, 0.52, 0.42), strata_b=(0.56, 0.40, 0.34), frozen=True, shallow=(0.74, 0.90, 0.96), deep=(0.18, 0.42, 0.66), mineral=(0.92, 0.94, 0.96))),
"saturn": dict(title="Saturn", note="Pale gold bands and wide rings for the ring-ice slalom (Phase 5+).",
    radius=1000, g=10.0, influence=4800, seed=2001, sub=384, rocks=600, tiles=8,
    clouds=dict(radius=1900, a=(0.90, 0.82, 0.62), b=(0.82, 0.70, 0.50), c=(0.95, 0.90, 0.74), d=(0.76, 0.64, 0.46), bands=12, turb=0.3,
        ring=(2450, 3500), ring_color=(0.86, 0.80, 0.68)),
    terrain=dict(hills=6, hill_f=8, ridges=5, ridge_f=5, craters=0, crater_r=(5, 10),
        landforms=[("Mountain", 8, (80, 140), (40, 80)), ("Butte", 8, (20, 35), (30, 50))]),
    look=dict(low=(0.54, 0.46, 0.34), high=(0.70, 0.62, 0.46), crater=(0.44, 0.38, 0.28), rim=(0.76, 0.68, 0.52), slope=(0.46, 0.38, 0.28),
        rock=(0.50, 0.44, 0.34), rock_dark=(0.28, 0.24, 0.18))),
"titan": dict(title="Titan", note="Methane lakes and dunes under orange haze.",
    radius=450, g=3.5, influence=950, seed=2101, sub=256, rocks=400, lod=(384, 160, 8, 180),
    air=((1.0, 0.62, 0.22), 70, 1.6),
    terrain=dict(hills=3, hill_f=9, ridges=3, ridge_f=6, craters=6, crater_r=(8, 20), water=-1.5,
        landforms=[("Lake", 8, (50, 110), (4, 8)), ("River", 8, (3, 5), (0, 0), (200, 400)), ("Dunes", 6, (70, 130), (5, 8)), ("Mountain", 4, (40, 70), (20, 35))]),
    look=dict(low=(0.56, 0.40, 0.22), high=(0.68, 0.53, 0.32), crater=(0.46, 0.33, 0.20), rim=(0.74, 0.60, 0.40), slope=(0.48, 0.34, 0.20),
        shadow=(0.42, 0.32, 0.48), rim_light=(1.0, 0.70, 0.40), rock=(0.50, 0.40, 0.28), rock_dark=(0.28, 0.20, 0.14)),
    land=dict(shallow=(0.36, 0.28, 0.18), deep=(0.10, 0.08, 0.06), wet=(0.32, 0.24, 0.16))),
"uranus": dict(title="Uranus", note="A pale cyan ice giant spinning on its side.",
    radius=800, g=9.0, influence=3600, seed=2201, sub=256, rocks=400, tiles=8,
    clouds=dict(radius=1500, a=(0.64, 0.86, 0.88), b=(0.58, 0.80, 0.84), c=(0.72, 0.90, 0.90), d=(0.60, 0.82, 0.86), bands=8, turb=0.15),
    terrain=dict(hills=5, hill_f=8, ridges=4, ridge_f=5, craters=0, crater_r=(5, 10),
        landforms=[("Mountain", 6, (60, 100), (30, 60))]),
    look=dict(low=(0.46, 0.56, 0.60), high=(0.62, 0.72, 0.76), crater=(0.38, 0.46, 0.50), rim=(0.70, 0.80, 0.84), slope=(0.40, 0.48, 0.52),
        rock=(0.48, 0.56, 0.60), rock_dark=(0.26, 0.32, 0.36))),
"neptune": dict(title="Neptune", note="Deep blue with a dark storm; its ocean world arrives in Phase 5.",
    radius=800, g=9.5, influence=3600, seed=2301, sub=256, rocks=400, tiles=8,
    clouds=dict(radius=1500, a=(0.26, 0.44, 0.82), b=(0.20, 0.36, 0.72), c=(0.38, 0.58, 0.90), d=(0.22, 0.40, 0.76), bands=10, turb=0.35,
        storm_lat=-20, storm=0.16, storm_color=(0.12, 0.20, 0.46)),
    terrain=dict(hills=5, hill_f=8, ridges=4, ridge_f=5, craters=0, crater_r=(5, 10),
        landforms=[("Mountain", 6, (60, 100), (30, 60))]),
    look=dict(low=(0.30, 0.38, 0.52), high=(0.44, 0.52, 0.66), crater=(0.24, 0.30, 0.42), rim=(0.52, 0.60, 0.74), slope=(0.28, 0.34, 0.46),
        rock=(0.34, 0.40, 0.52), rock_dark=(0.18, 0.22, 0.30))),
}

for key, b in BODIES.items():
    text = star(b) if b.get("kind") == "star" else rocky(b)
    with open(os.path.join(OUT, key + ".ron"), "w") as f:
        f.write(text)
print("wrote", len(BODIES), "bodies")
