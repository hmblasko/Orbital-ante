// The sun: a hot, slowly churning glowing ball. Bright core, warmer limb
// (limb darkening), drifting granules. Unshaded so it is its own light;
// strong emission feeds the bloom.
shader_type spatial;
render_mode unshaded, specular_disabled, shadows_disabled;

uniform vec3 core_color : source_color = vec3(1.0, 0.92, 0.7);
uniform vec3 edge_color : source_color = vec3(1.0, 0.5, 0.15);
uniform float energy = 4.0;
uniform sampler2D granules : filter_linear_mipmap, repeat_enable;

varying vec3 local_dir;

void vertex() {
	local_dir = normalize(VERTEX);
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Two layers of noise drifting opposite ways look like boiling plasma.
	vec2 uv = vec2(atan(local_dir.z, local_dir.x) / 6.2832, acos(clamp(local_dir.y, -1.0, 1.0)) / 3.1416);
	float g1 = texture(granules, uv * vec2(3.0, 2.0) + vec2(TIME * 0.004, 0.0)).r;
	float g2 = texture(granules, uv * vec2(5.0, 3.0) - vec2(TIME * 0.006, TIME * 0.002)).r;
	float churn = mix(g1, g2, 0.5);
	vec3 c = mix(edge_color, core_color, smoothstep(0.0, 0.85, facing));
	c *= 0.82 + 0.36 * churn;
	ALBEDO = c * energy;
}
