// Painted ice: frozen pools and lakes (section 11: painterly, readable).
// Clear blue where the ice is thick, frosted white at the shore, with pale
// crack lines and frost patches so it reads as solid from a distance and
// never as water. Static: ice doesn't ripple.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, specular_schlick_ggx;

uniform vec3 shallow_color : source_color = vec3(0.70, 0.88, 0.95);
uniform vec3 deep_color : source_color = vec3(0.15, 0.40, 0.62);
uniform vec3 frost_color : source_color = vec3(0.94, 0.97, 1.0);
uniform vec3 sheen_color : source_color = vec3(0.70, 0.82, 1.0);
uniform sampler2D wave_noise : filter_linear_mipmap, repeat_enable;
uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
// Metres of ice before it reaches the deep colour.
uniform float depth_fade = 2.0;
uniform float frost_width = 0.6;
uniform float crack_scale = 0.032;
uniform float crack_width = 0.012;

varying vec3 local_pos;
varying vec3 local_normal;

void vertex() {
	local_pos = VERTEX;
	local_normal = NORMAL;
}

float tri(vec3 p, vec3 n, float scale) {
	vec3 w = pow(abs(n), vec3(4.0));
	w /= (w.x + w.y + w.z);
	return texture(wave_noise, p.zy * scale).r * w.x + texture(wave_noise, p.xz * scale).r * w.y + texture(wave_noise, p.xy * scale).r * w.z;
}

void fragment() {
	float raw = texture(depth_texture, SCREEN_UV).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
	behind.xyz /= behind.w;
	float thickness = max(VERTEX.z - behind.z, 0.0);

	// Crack lines: thin bands where two noise fields cross their midpoint.
	float n1 = tri(local_pos, local_normal, crack_scale);
	float n2 = tri(local_pos + 17.0, local_normal, crack_scale * 2.7);
	// Keep lines at least a pixel wide so they don't shimmer at a distance.
	float w = max(crack_width, fwidth(n1) * 1.5);
	float crack = 1.0 - smoothstep(w * 0.4, w, abs(n1 - 0.5));
	crack = max(crack, (1.0 - smoothstep(w * 0.3, w * 0.8, abs(n2 - 0.5))) * 0.35);
	// Patchy frost on top, thicker near the shore.
	float patch = smoothstep(0.5, 0.8, tri(local_pos + 41.0, local_normal, crack_scale * 0.5));
	float shore = 1.0 - smoothstep(frost_width * 0.3, frost_width * (0.8 + n2), thickness);

	float deep = smoothstep(0.0, depth_fade, thickness);
	vec3 col = mix(shallow_color, deep_color, deep);
	float frost = clamp(max(shore, patch * 0.4), 0.0, 1.0);
	col = mix(col, frost_color, frost * 0.9);
	col = mix(col, frost_color, crack * 0.75);
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	col = mix(col, sheen_color, fres * 0.35);
	ALBEDO = col;
	ALPHA = clamp(mix(0.75, 0.96, deep) + frost * 0.3 + crack * 0.3, 0.0, 1.0);
	// Polished clear ice, matte frost.
	ROUGHNESS = mix(0.05, 0.7, max(frost, crack));
	SPECULAR = 0.7;
}
