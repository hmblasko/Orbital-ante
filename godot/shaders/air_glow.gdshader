// Air seen as light (section 4: subtle atmosphere tints, "orange haze on
// Mars, light blue on Earth"). From space: a soft rim that glows on the
// day side and fades into the night side. From inside: a gentle tint over
// the sky that is strongest at the horizon. Additive, so it never darkens.
shader_type spatial;
render_mode blend_add, cull_disabled, depth_draw_never, specular_disabled, shadows_disabled;

uniform vec3 air_color : source_color = vec3(0.45, 0.7, 1.0);
uniform float strength = 1.0;
uniform float inner_radius = 600.0; // kept for future height-based scattering
uniform float outer_radius = 660.0;

varying vec3 outward_view;
varying float inside;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 center = NODE_POSITION_WORLD;
	outward_view = normalize((VIEW_MATRIX * vec4(normalize(world - center), 0.0)).xyz);
	inside = step(length(CAMERA_POSITION_WORLD - center), outer_radius);
}

void fragment() {
	vec3 n = outward_view;
	float facing = abs(dot(n, VIEW));
	float glow;
	if (inside > 0.5) {
		// Looking up through the air: thicker toward the horizon.
		glow = (0.25 + 0.75 * pow(1.0 - facing, 2.0)) * 0.45;
	} else {
		// Limb glow: brightest where the line of sight grazes the shell.
		// Only the thin band where the line of sight grazes the air glows;
		// the face of the planet stays clear.
		float rim = 1.0 - facing;
		glow = pow(smoothstep(0.45, 0.97, rim), 2.0) * (1.0 - smoothstep(0.97, 1.0, rim));
	}
	ALBEDO = vec3(1.0);
	ALPHA = clamp(glow * strength, 0.0, 1.0);
	// Store the outward normal for light(): back faces would flip NORMAL.
	NORMAL = n;
}

void light() {
	// Day side bright, a little scattered light past the terminator.
	float day = smoothstep(-0.35, 0.45, dot(NORMAL, LIGHT));
	DIFFUSE_LIGHT += air_color * day * LIGHT_COLOR * ATTENUATION;
}
