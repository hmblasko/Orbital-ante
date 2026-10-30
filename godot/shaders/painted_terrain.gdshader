// Painted terrain: TF2-style illustrative shading (Mitchell, Francke & Eng,
// 2007): half-Lambert diffuse through a soft warm/cool ramp so shadows are
// never pure black, plus a view-dependent rim light. Albedo is painted
// from per-vertex hints (crater floors, rims, height, slope) and broad,
// low-contrast noise, like a hand-painted texture with soft gradients.
// COLOR.r = crater floor, COLOR.g = crater rim, COLOR.b = normalised height.
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

varying vec3 local_pos;
varying vec3 local_normal;

void vertex() {
	local_pos = VERTEX;
	local_normal = NORMAL;
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
	// Soft painterly variation and fine regolith grain.
	albedo *= 0.88 + broad * 0.22;
	albedo *= 0.90 + grain * 0.2;
	// Scattered darker pebbles.
	albedo *= 1.0 - 0.18 * smoothstep(0.68, 0.74, grain);
	ALBEDO = albedo;
	ROUGHNESS = 1.0;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float half_lambert = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	half_lambert *= half_lambert;
	// Warp ramp: cool, dim shadow side rising to full warm light.
	float lit = smoothstep(0.12, 0.9, half_lambert);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	float shadow = mix(0.25, 1.0, ATTENUATION);
	DIFFUSE_LIGHT += ramp * shadow * LIGHT_COLOR / PI;
	// Rim light on edges facing the light's side.
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.2, 0.5, ndl) * ATTENUATION * LIGHT_COLOR / PI;
}
