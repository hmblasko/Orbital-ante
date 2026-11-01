// Painted props (tanks, supply point, rocks): the same TF2-style ramp and
// rim as the terrain, with optional soft specular for metal and an
// emission mask for lights and gauges.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform vec3 albedo : source_color = vec3(0.8);
uniform vec3 albedo_dark : source_color = vec3(0.5);
uniform sampler2D grain_noise : filter_linear_mipmap, repeat_enable;
uniform float grain_scale = 1.5;
uniform float grain_strength = 0.25;
uniform vec3 shadow_tint : source_color = vec3(0.30, 0.36, 0.55);
uniform float shadow_floor = 0.14;
uniform vec3 rim_color : source_color = vec3(0.6, 0.7, 1.0);
uniform float rim_strength = 0.45;
uniform float specular = 0.0;
uniform float shininess = 24.0;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
// Take the base colour from the mesh's vertex colours (hoodoo bands) instead of `albedo`.
uniform bool use_vertex_color = false;

varying vec3 obj_pos;
varying vec3 obj_normal;

void vertex() {
	obj_pos = VERTEX;
	obj_normal = NORMAL;
}

void fragment() {
	vec3 w = pow(abs(obj_normal), vec3(4.0));
	w /= (w.x + w.y + w.z);
	float g = texture(grain_noise, obj_pos.zy * grain_scale).r * w.x + texture(grain_noise, obj_pos.xz * grain_scale).r * w.y + texture(grain_noise, obj_pos.xy * grain_scale).r * w.z;
	// Vertex colours arrive as sRGB; the lighting works in linear.
	vec3 base = use_vertex_color ? pow(COLOR.rgb, vec3(2.2)) : albedo;
	vec3 dark = use_vertex_color ? base * 0.55 : albedo_dark;
	ALBEDO = mix(dark, base, clamp(1.0 - grain_strength + g * grain_strength * 2.0, 0.0, 1.0));
	EMISSION = emission * emission_energy;
	ROUGHNESS = 1.0;
}

void light() {
	// The shadow floor only applies to the sun; lamps (supply ring, lava glow)
	// must fade to nothing at their range, or their light cluster shows as a box.
	float ndl = dot(NORMAL, LIGHT);
	float hl = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	hl *= hl;
	float lit = smoothstep(0.2, 0.65, hl);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	DIFFUSE_LIGHT += ramp * (LIGHT_IS_DIRECTIONAL ? mix(0.3, 1.0, ATTENUATION) : ATTENUATION) * LIGHT_COLOR / PI;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.3, 0.5, ndl) * (LIGHT_IS_DIRECTIONAL ? 1.0 : ATTENUATION) * LIGHT_COLOR / PI;
	if (specular > 0.0) {
		vec3 h = normalize(LIGHT + VIEW);
		SPECULAR_LIGHT += pow(max(dot(NORMAL, h), 0.0), shininess) * specular * ATTENUATION * LIGHT_COLOR / PI;
	}
}
