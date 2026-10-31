// Planetary rings: many thin ringlets of dusty ice with gaps, see-through
// at the edges and in the gaps, lit from both sides.
shader_type spatial;
render_mode cull_disabled, specular_disabled, depth_draw_opaque;

uniform vec3 ring_color : source_color = vec3(0.86, 0.80, 0.68);
uniform float inner_m = 2000.0;
uniform float outer_m = 3200.0;
uniform sampler2D grain : filter_linear_mipmap, repeat_enable;
uniform vec3 shadow_tint : source_color = vec3(0.30, 0.30, 0.55);

varying float radial;

void vertex() {
	radial = (length(VERTEX.xz) - inner_m) / (outer_m - inner_m);
}

void fragment() {
	float r = clamp(radial, 0.0, 1.0);
	float rings = texture(grain, vec2(r * 3.0, 0.5)).r;
	float fine = texture(grain, vec2(r * 17.0, 0.25)).r;
	// A big gap a bit past the middle (like the Cassini division).
	float gap = smoothstep(0.58, 0.6, r) * (1.0 - smoothstep(0.64, 0.66, r));
	float density = (0.6 + 0.5 * rings) * (0.7 + 0.3 * fine) * (1.0 - 0.9 * gap);
	density *= smoothstep(0.0, 0.06, r) * (1.0 - smoothstep(0.94, 1.0, r));
	ALBEDO = ring_color * (0.8 + 0.4 * fine);
	ALPHA = clamp(density, 0.0, 1.0);
}

void light() {
	float lit = 0.35 + 0.65 * abs(dot(NORMAL, LIGHT));
	DIFFUSE_LIGHT += mix(shadow_tint * 0.3, vec3(1.0), lit) * ATTENUATION * LIGHT_COLOR / PI;
}
