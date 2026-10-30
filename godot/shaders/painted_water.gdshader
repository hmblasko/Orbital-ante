// Painted water: a clean stylised surface (section 11: painterly, readable,
// not photoreal). Colour deepens with the water's thickness, a soft foam
// line traces every shore, small animated ripples catch the sun, and the
// edges pick up a sky-coloured fresnel sheen.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, specular_schlick_ggx;

uniform vec3 shallow_color : source_color = vec3(0.25, 0.75, 0.78);
uniform vec3 deep_color : source_color = vec3(0.05, 0.22, 0.38);
uniform vec3 foam_color : source_color = vec3(0.92, 0.97, 1.0);
uniform vec3 sheen_color : source_color = vec3(0.55, 0.70, 0.95);
uniform sampler2D wave_noise : filter_linear_mipmap, repeat_enable;
uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
// Metres of water before it reaches the deep colour.
uniform float depth_fade = 3.5;
uniform float foam_width = 0.45;
uniform float ripple_scale = 0.18;
uniform float ripple_speed = 0.35;
uniform float ripple_strength = 0.06;

varying vec3 local_pos;
varying vec3 local_normal;

void vertex() {
	local_pos = VERTEX;
	local_normal = NORMAL;
}

float tri(vec3 p, vec3 n, float scale, vec2 drift) {
	vec3 w = pow(abs(n), vec3(4.0));
	w /= (w.x + w.y + w.z);
	return texture(wave_noise, p.zy * scale + drift).r * w.x + texture(wave_noise, p.xz * scale + drift).r * w.y + texture(wave_noise, p.xy * scale + drift).r * w.z;
}

void fragment() {
	// How much water lies between this surface and the ground behind it.
	float raw = texture(depth_texture, SCREEN_UV).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
	behind.xyz /= behind.w;
	float thickness = max(VERTEX.z - behind.z, 0.0);

	// Two drifting ripple layers, applied as a derivative bump (no tangents needed).
	float t = TIME * ripple_speed;
	float h = tri(local_pos, local_normal, ripple_scale, vec2(t * 0.11, t * 0.07)) + tri(local_pos, local_normal, ripple_scale * 2.3, vec2(-t * 0.09, t * 0.13)) * 0.5;
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(h) * r1 + dFdy(h) * r2);
	NORMAL = normalize(abs(det) * NORMAL - grad * ripple_strength);

	float deep = smoothstep(0.0, depth_fade, thickness);
	vec3 col = mix(shallow_color, deep_color, deep);
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	col = mix(col, sheen_color, fres * 0.45);
	// Foam hugs the shore, broken up by the ripple noise.
	float foam = 1.0 - smoothstep(foam_width * 0.3, foam_width * (0.7 + h * 0.6), thickness);
	col = mix(col, foam_color, foam * 0.85);
	ALBEDO = col;
	ALPHA = clamp(mix(0.55, 0.92, deep) + foam * 0.3 + fres * 0.2, 0.0, 1.0);
	ROUGHNESS = mix(0.08, 0.6, foam);
	SPECULAR = 0.6;
}
