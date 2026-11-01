// Faction suits (CLAUDE.md sections 7, 11 and 17.1): an imported suit model
// repainted in a faction's colours, TF2-style. The suit texture's light
// cloth becomes the faction's main colour, its coloured stripes the trim,
// its dark parts the dark colour; shading and wear from the texture stay as
// value changes, so the suit keeps its seams, folds and grime. Lit with the
// same half-Lambert ramp and rim light as every painted model (Mitchell,
// Francke & Eng, 2007). Faction colours are used for factions only (section 10).
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 main_color : source_color = vec3(0.43, 0.46, 0.27);
uniform vec3 trim_color : source_color = vec3(0.62, 0.30, 0.15);
uniform vec3 dark_color : source_color = vec3(0.16, 0.15, 0.12);
// Visor glass glints with this colour at grazing angles.
uniform vec3 visor_color : source_color = vec3(0.95, 0.55, 0.2);
uniform vec3 shadow_tint : source_color = vec3(0.32, 0.36, 0.58);
uniform float shadow_floor = 0.18;
uniform vec3 rim_color : source_color = vec3(0.62, 0.72, 1.0);
uniform float rim_strength = 0.45;
// Hit flash: 0 to 1, a brief warm flash when this part is hit.
uniform float hit_flash = 0.0;
// Fade-out for gibs and ragdolls (1 = solid).
uniform float fade = 1.0;

varying float visor_mask;

void fragment() {
	vec3 t = textureLod(albedo_texture, UV, 1.0).rgb;
	float lum = dot(t, vec3(0.299, 0.587, 0.114));
	float sat = max(max(t.r, t.g), t.b) - min(min(t.r, t.g), t.b);
	// Coloured stripes (red and blue bands on the original suit) -> trim.
	float stripe = smoothstep(0.25, 0.45, sat);
	// Light cloth -> main colour, keeping the cloth's light and shade.
	vec3 cloth = main_color * mix(0.55, 1.15, smoothstep(0.25, 0.9, lum));
	vec3 dark = dark_color * mix(0.7, 1.3, lum * 3.0);
	vec3 c = mix(dark, cloth, smoothstep(0.12, 0.38, lum));
	c = mix(c, trim_color * mix(0.8, 1.2, lum), stripe);
	// Very dark, flat areas are visor glass.
	visor_mask = 1.0 - smoothstep(0.06, 0.14, lum);
	c = mix(c, vec3(0.04, 0.045, 0.05), visor_mask);
	c = mix(c, vec3(1.0, 0.85, 0.6), hit_flash * 0.6);
	ALBEDO = c;
	ROUGHNESS = mix(1.0, 0.2, visor_mask);
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	EMISSION = visor_color * fres * visor_mask * 0.35 + vec3(1.0, 0.6, 0.3) * hit_flash * 0.4;
	if (fade < 0.999) {
		// Dither out (no transparency sorting needed).
		float n = fract(sin(dot(floor(FRAGCOORD.xy), vec2(12.9898, 78.233))) * 43758.5453);
		if (n > fade) {
			discard;
		}
	}
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float hl = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	hl *= hl;
	float lit = smoothstep(0.2, 0.7, hl);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	DIFFUSE_LIGHT += ramp * (LIGHT_IS_DIRECTIONAL ? mix(0.3, 1.0, ATTENUATION) : ATTENUATION) * LIGHT_COLOR / PI;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.3, 0.5, ndl) * (LIGHT_IS_DIRECTIONAL ? 1.0 : ATTENUATION) * LIGHT_COLOR / PI;
	if (visor_mask > 0.5) {
		vec3 h = normalize(LIGHT + VIEW);
		SPECULAR_LIGHT += pow(max(dot(NORMAL, h), 0.0), 40.0) * 0.8 * ATTENUATION * LIGHT_COLOR / PI;
	}
}
