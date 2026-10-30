// Painted imported models (NASA hardware): keeps each model's own colour
// texture but lights it with the same TF2-style ramp and rim as the terrain
// and props (Mitchell, Francke & Eng, 2007), so real spacecraft sit in the
// painted world instead of looking pasted in. Metal parts get a soft,
// broad highlight rather than a hard photoreal one.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform bool has_texture = false;
uniform float metallic = 0.0;
uniform vec3 shadow_tint : source_color = vec3(0.32, 0.36, 0.58);
uniform float shadow_floor = 0.16;
uniform vec3 rim_color : source_color = vec3(0.62, 0.72, 1.0);
uniform float rim_strength = 0.4;
// Painterly lift: flatten tiny texture noise and nudge saturation.
uniform float saturation = 1.12;
// Dust settled on upward faces (wrecks), in the planet's own sand colour.
uniform float grime = 0.0;
uniform vec3 dust_color : source_color = vec3(0.78, 0.56, 0.38);
uniform vec3 planet_center = vec3(0.0);

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 c = albedo_color.rgb;
	if (has_texture) {
		// Sample a blurrier mip so photo detail reads as soft paint.
		c *= textureLod(albedo_texture, UV, 1.2).rgb;
	}
	float grey = dot(c, vec3(0.299, 0.587, 0.114));
	c = clamp(mix(vec3(grey), c, saturation), 0.0, 1.0);
	if (grime > 0.0) {
		float facing_up = dot(world_normal, normalize(world_pos - planet_center));
		float speckle = fract(sin(dot(floor(world_pos * 9.0), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
		c = mix(c, dust_color, grime * smoothstep(0.1, 0.8, facing_up) * (0.75 + 0.25 * speckle));
		c *= 1.0 - grime * 0.25;
	}
	ALBEDO = c;
	ROUGHNESS = 1.0;
}

void light() {
	// The shadow floor only applies to the sun; lamps (supply ring, lava glow)
	// must fade to nothing at their range, or their light cluster shows as a box.
	float ndl = dot(NORMAL, LIGHT);
	float hl = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	hl *= hl;
	float lit = smoothstep(0.2, 0.7, hl);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	DIFFUSE_LIGHT += ramp * (LIGHT_IS_DIRECTIONAL ? mix(0.3, 1.0, ATTENUATION) : ATTENUATION) * LIGHT_COLOR / PI;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * smoothstep(-0.3, 0.5, ndl) * (LIGHT_IS_DIRECTIONAL ? 1.0 : ATTENUATION) * LIGHT_COLOR / PI;
	if (metallic > 0.05) {
		vec3 h = normalize(LIGHT + VIEW);
		SPECULAR_LIGHT += pow(max(dot(NORMAL, h), 0.0), 18.0) * metallic * 0.6 * ATTENUATION * LIGHT_COLOR / PI;
	}
}
