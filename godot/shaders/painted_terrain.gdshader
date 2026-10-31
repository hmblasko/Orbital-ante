// Painted terrain: TF2-style illustrative shading (Mitchell, Francke & Eng,
// 2007): half-Lambert diffuse through a soft warm/cool ramp so shadows are
// never pure black, plus a view-dependent rim light. Albedo is painted
// from per-vertex hints (crater floors, rims, height, slope) and broad,
// low-contrast noise, like a hand-painted texture with soft gradients.
// COLOR.r = crater floor, COLOR.g = crater rim, COLOR.b = normalised height,
// COLOR.a = mineral terraces; UV = (strata, lava); UV2 = (snow, wet);
// CUSTOM0 = (mare, rays, frost, melt).
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform vec3 color_low : source_color = vec3(0.52, 0.50, 0.48);
uniform vec3 color_high : source_color = vec3(0.86, 0.83, 0.78);
uniform vec3 color_crater : source_color = vec3(0.40, 0.38, 0.37);
uniform vec3 color_rim : source_color = vec3(0.93, 0.91, 0.86);
uniform vec3 color_slope : source_color = vec3(0.45, 0.42, 0.40);
uniform vec3 shadow_tint : source_color = vec3(0.30, 0.36, 0.55);
uniform vec3 rim_color : source_color = vec3(0.55, 0.65, 0.95);
uniform float rim_strength = 0.35;
uniform float shadow_floor = 0.10;
uniform sampler2D detail_noise : filter_linear_mipmap, repeat_enable;
uniform sampler2D grain_noise : filter_linear_mipmap, repeat_enable;
uniform float detail_scale = 0.045;
uniform float grain_scale = 0.35;
uniform float body_radius = 60.0;
// Fine bumps from the noise, so close ground isn't flat (no extra textures).
// Strength is the bump height in metres for a full 0..1 noise swing.
uniform float bump_strength = 0.03;
uniform float bump_scale = 0.12;
// Landform paint (procgen/landforms.rs).
uniform vec3 strata_a : source_color = vec3(0.62, 0.55, 0.50);
uniform vec3 strata_b : source_color = vec3(0.48, 0.43, 0.40);
uniform vec3 basalt_color : source_color = vec3(0.18, 0.17, 0.19);
uniform vec3 lava_color : source_color = vec3(1.0, 0.42, 0.08);
uniform float lava_energy = 3.0;
uniform vec3 snow_color : source_color = vec3(0.92, 0.94, 0.98);
uniform vec3 wet_color : source_color = vec3(0.30, 0.28, 0.27);
uniform vec3 mineral_color : source_color = vec3(0.93, 0.92, 0.86);
uniform vec3 mare_color : source_color = vec3(0.24, 0.24, 0.26);
uniform vec3 ray_color : source_color = vec3(0.92, 0.91, 0.88);
uniform vec3 frost_color : source_color = vec3(0.80, 0.86, 0.95);
uniform vec3 melt_color : source_color = vec3(0.10, 0.11, 0.14);

// Where this tile's mesh origin sits in the body (tiles are drawn around
// their own centres so detail levels switch by distance to the tile).
instance uniform vec3 tile_offset = vec3(0.0);

varying vec3 local_pos;
varying vec3 local_normal;
varying vec4 lunar;

void vertex() {
	local_pos = VERTEX + tile_offset;
	local_normal = NORMAL;
	lunar = CUSTOM0;
}

float triplanar(sampler2D tex, vec3 p, vec3 n, float scale) {
	vec3 w = pow(abs(n), vec3(4.0));
	w /= (w.x + w.y + w.z);
	return texture(tex, p.zy * scale).r * w.x + texture(tex, p.xz * scale).r * w.y + texture(tex, p.xy * scale).r * w.z;
}

void fragment() {
	vec3 up = normalize(local_pos);
	float slope = 1.0 - clamp(dot(normalize(local_normal), up), 0.0, 1.0);
	float broad = triplanar(detail_noise, local_pos, local_normal, detail_scale);
	float grain = triplanar(grain_noise, local_pos, local_normal, grain_scale);

	// Bump: perturb the normal from a fine noise height (Mikkelsen 2010,
	// "Bump Mapping Unparametrized Surfaces on the GPU"). Needs no tangents.
	float h = triplanar(grain_noise, local_pos, local_normal, bump_scale) + 0.5 * triplanar(detail_noise, local_pos, local_normal, detail_scale * 6.0);
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 surf_grad = sign(det) * (dFdx(h) * r1 + dFdy(h) * r2);
	NORMAL = normalize(abs(det) * NORMAL - surf_grad * bump_strength);

	vec3 albedo = mix(color_low, color_high, smoothstep(0.25, 0.85, COLOR.b + (broad - 0.5) * 0.5));
	albedo = mix(albedo, color_slope, smoothstep(0.12, 0.4, slope));
	albedo = mix(albedo, color_crater, COLOR.r * 0.85);
	albedo = mix(albedo, color_rim, COLOR.g * 0.6);
	// Layered rock bands by altitude, wobbled so they never look ruled.
	float strata = UV.x;
	float alt = length(local_pos) + (broad - 0.5) * 3.0 + (grain - 0.5) * 0.4;
	float band = sin(alt * 1.5) * 0.5 + 0.5;
	float coarse = sin(alt * 0.45 + 1.3) * 0.5 + 0.5;
	vec3 rock = mix(strata_a, strata_b, smoothstep(0.15, 0.85, band));
	rock = mix(rock, rock * vec3(1.06, 0.98, 0.92), smoothstep(0.6, 0.95, coarse));
	rock *= 1.0 - 0.1 * smoothstep(0.75, 0.95, coarse);
	albedo = mix(albedo, rock, strata * mix(0.6, 1.0, smoothstep(0.1, 0.4, slope)));
	// Volcanic basalt, damp banks, mineral terraces, snow caps.
	float lava = UV.y;
	albedo = mix(albedo, basalt_color * (0.85 + broad * 0.3), smoothstep(0.05, 0.28, lava));
	albedo = mix(albedo, wet_color, UV2.y * 0.7);
	float mineral = COLOR.a;
	// Travertine: cream terrace lips with darker risers between them and
	// warm iron streaks running downhill, never a plain white mound.
	float lip = smoothstep(0.55, 0.9, sin(length(local_pos) * 9.0) * 0.5 + 0.5);
	vec3 trav = mineral_color * mix(0.82, 1.0, lip) * (0.94 + grain * 0.1);
	trav = mix(trav, trav * vec3(1.0, 0.78, 0.58), smoothstep(0.5, 0.75, broad) * 0.7);
	albedo = mix(albedo, trav, smoothstep(0.15, 0.55, mineral));
	float snow = smoothstep(0.25, 0.6, UV2.x + (broad - 0.5) * 0.5) * (1.0 - 0.75 * smoothstep(0.3, 0.6, slope));
	// Break the cap into drifts and bare rock streaks, so it reads as snow
	// lying in hollows rather than a painted-on white cap.
	float drift = smoothstep(0.45, 0.6, broad * 0.75 + grain * 0.45);
	snow *= mix(0.15, 1.0, drift);
	albedo = mix(albedo, mix(snow_color, albedo, 0.12), snow * 0.9);
	// Glowing lava with a cooled crust breaking it up.
	float glow = smoothstep(0.6, 0.95, lava) * smoothstep(0.25, 0.55, grain + broad * 0.4);
	albedo = mix(albedo, vec3(0.08, 0.06, 0.06), smoothstep(0.6, 0.95, lava));
	EMISSION = lava_color * lava_energy * glow;
	// Mare basalt: darker, cooler plains with soft mottling, never flat.
	albedo = mix(albedo, mare_color * (0.86 + broad * 0.28), smoothstep(0.05, 0.6, lunar.r));
	// Fresh ejecta rays: bright streaks broken up by the grain.
	float rays = lunar.g * mix(0.55, 1.0, smoothstep(0.3, 0.6, grain * 0.6 + broad * 0.6));
	albedo = mix(albedo, ray_color, rays * 0.9);
	// Frost lying in hollows of the shadowed floor.
	float frost = smoothstep(0.1, 0.5, lunar.b) * mix(0.55, 1.0, smoothstep(0.35, 0.6, grain));
	albedo = mix(albedo, frost_color, frost * 0.7);
	// Impact melt: near-black glass with a cool sheen at grazing angles.
	float melt = smoothstep(0.2, 0.6, lunar.a);
	albedo = mix(albedo, melt_color, melt * 0.9);
	float fres_m = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	EMISSION += vec3(0.35, 0.42, 0.55) * fres_m * melt * 0.25;
	// Soft painterly variation and fine regolith grain.
	albedo *= 0.88 + broad * 0.22;
	albedo *= 0.90 + grain * 0.2;
	// Scattered darker pebbles.
	albedo *= 1.0 - 0.18 * smoothstep(0.68, 0.74, grain);
	ALBEDO = albedo;
	ROUGHNESS = 1.0;
}

void light() {
	// The shadow floor only applies to the sun; lamps (supply ring, lava glow)
	// must fade to nothing at their range, or their light cluster shows as a box.
	float ndl = dot(NORMAL, LIGHT);
	float half_lambert = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	half_lambert *= half_lambert;
	// Warp ramp: cool, dim shadow side rising to full warm light.
	float lit = smoothstep(0.12, 0.9, half_lambert);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	float shadow = (LIGHT_IS_DIRECTIONAL ? mix(0.25, 1.0, ATTENUATION) : ATTENUATION);
	DIFFUSE_LIGHT += ramp * shadow * LIGHT_COLOR / PI;
	// Rim light on edges facing the light's side.
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.2, 0.5, ndl) * ATTENUATION * LIGHT_COLOR / PI;
}
