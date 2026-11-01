// Helmet visor overlay: a soft dark rim at the screen edge with a slight
// curve, faint glass reflections, and a red-orange edge pulse when air is
// low. Kept subtle so it never gets in the way (section 6, "never in the way").
shader_type canvas_item;

uniform float edge_darkness = 0.55;
uniform float low_oxygen = 0.0;
uniform float damage = 0.0;
uniform float time_s = 0.0;
uniform vec3 warn_color : source_color = vec3(1.0, 0.38, 0.2);
uniform vec2 screen_size = vec2(1280.0, 800.0);

void fragment() {
	vec2 uv = UV;
	vec2 c = (uv - 0.5) * vec2(screen_size.x / screen_size.y, 1.0);
	// Rounded-rectangle distance: the visor frame hugs the corners.
	vec2 box = vec2(0.5 * screen_size.x / screen_size.y, 0.5) - 0.06;
	vec2 q = abs(c) - box + 0.22;
	float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 0.22;
	float frame = smoothstep(-0.10, 0.06, d);
	vec3 col = vec3(0.01, 0.015, 0.03);
	float alpha = frame * edge_darkness;

	// Glass reflections: two soft diagonal streaks, top left.
	float streak = smoothstep(0.035, 0.0, abs(uv.x * 0.8 + uv.y - 0.32)) * smoothstep(0.55, 0.0, length(uv - vec2(0.12, 0.12)));
	streak += 0.5 * smoothstep(0.015, 0.0, abs(uv.x * 0.8 + uv.y - 0.40)) * smoothstep(0.5, 0.0, length(uv - vec2(0.14, 0.16)));
	col = mix(col, vec3(0.75, 0.85, 1.0), streak * 0.35);
	alpha = max(alpha, streak * 0.04);

	// Low-oxygen pulse and damage flash on the frame edge.
	float pulse = (0.5 + 0.5 * sin(time_s * 6.2832 * 1.2)) * low_oxygen;
	float glow = smoothstep(-0.035, 0.07, d);
	col = mix(col, warn_color, glow * max(pulse * 0.8, damage));
	alpha = max(alpha, glow * max(pulse * 0.24, damage * 0.35));
	COLOR = vec4(col, alpha);
}
