// Gas giant cloud deck: soft latitude bands from four painted colours,
// warped by swirling noise, with an optional storm oval. Lit with the same
// half-Lambert ramp and rim as everything else (TF2 rules, section 11) so
// it reads as painted, not photographic. Two-sided: you can fly in.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec3 band_a : source_color = vec3(0.86, 0.74, 0.58);
uniform vec3 band_b : source_color = vec3(0.70, 0.50, 0.36);
uniform vec3 band_c : source_color = vec3(0.94, 0.88, 0.76);
uniform vec3 band_d : source_color = vec3(0.56, 0.40, 0.32);
uniform vec3 storm_color : source_color = vec3(0.78, 0.36, 0.24);
uniform float bands = 14.0;
uniform float turbulence = 0.5;
uniform float storm_latitude = -0.35;
uniform float storm_size = 0.0;
uniform sampler2D swirl_noise : filter_linear_mipmap, repeat_enable;
uniform vec3 shadow_tint : source_color = vec3(0.30, 0.30, 0.55);
uniform float shadow_floor = 0.18;
uniform vec3 rim_color : source_color = vec3(0.7, 0.8, 1.0);
uniform float rim_strength = 0.35;

varying vec3 local_dir;

vec3 palette(float t) {
	t = fract(t);
	if (t < 0.25) return mix(band_a, band_b, smoothstep(0.0, 0.25, t));
	if (t < 0.5) return mix(band_b, band_c, smoothstep(0.25, 0.5, t));
	if (t < 0.75) return mix(band_c, band_d, smoothstep(0.5, 0.75, t));
	return mix(band_d, band_a, smoothstep(0.75, 1.0, t));
}

void vertex() {
	local_dir = normalize(VERTEX);
}

void fragment() {
	vec3 d = local_dir;
	float lon = atan(d.z, d.x) / 6.2832;
	float lat = d.y;
	// Bands drift at different speeds by latitude (zonal winds).
	float wind = sin(lat * 9.0) * 0.004 * TIME;
	vec2 uv = vec2(lon + wind, lat * 0.5 + 0.5);
	float n1 = texture(swirl_noise, uv * vec2(2.0, 1.5)).r - 0.5;
	float n2 = texture(swirl_noise, uv * vec2(6.0, 4.0) + vec2(0.3, 0.1)).r - 0.5;
	float warp = (n1 * 0.7 + n2 * 0.3) * turbulence;
	float t = (lat * 0.5 + 0.5) * bands * 0.25 + warp;
	vec3 c = palette(t);
	// Fine streaks along the bands.
	c *= 0.92 + 0.16 * texture(swirl_noise, vec2(lon * 8.0 + wind * 3.0, lat * 40.0)).r;
	if (storm_size > 0.0) {
		vec2 s = vec2((fract(lon + wind + 0.5) - 0.5) * 2.2, (lat - storm_latitude) * 2.6);
		float r = length(s) / storm_size;
		float swirl = texture(swirl_noise, vec2(atan(s.y, s.x) / 6.2832 + r * 0.6, r * 0.5)).r;
		float oval = 1.0 - smoothstep(0.65, 1.0, r + swirl * 0.25);
		c = mix(c, storm_color * (0.85 + 0.3 * swirl), oval);
	}
	ALBEDO = c;
	ROUGHNESS = 1.0;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float hl = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	hl *= hl;
	float lit = smoothstep(0.15, 0.75, hl);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	DIFFUSE_LIGHT += ramp * mix(0.35, 1.0, ATTENUATION) * LIGHT_COLOR / PI;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.3, 0.5, ndl) * LIGHT_COLOR / PI;
}
