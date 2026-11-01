// Ship interiors (CLAUDE.md section 5: rounded, bowl-like, good lighting;
// section 11: TF2-style painted look, panel lines, rivets, wear).
// One shader for every interior part; `kind` picks the surface pattern:
// 0 wall panels, 1 rubber floor, 2 metal trim, 3 shaft, 4 glowing ring,
// 5 seat fabric, 6 console. UV for walls/shaft: x = around (0-1),
// y = metres up from the floor.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform int kind = 0;
uniform vec4 base_color : source_color = vec4(0.86, 0.83, 0.76, 1.0);
uniform vec3 shadow_tint : source_color = vec3(0.30, 0.34, 0.55);
uniform float shadow_floor = 0.22;
uniform vec3 rim_color : source_color = vec3(0.62, 0.72, 1.0);
uniform float rim_strength = 0.25;
// Panels around the wall, and their height in metres.
uniform float panels_around = 24.0;
uniform float panel_height = 0.9;

varying vec3 local_pos;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float line(float x, float width) {
	float f = abs(fract(x) - 0.5);
	return smoothstep(0.5 - width, 0.5 - width * 0.4, f);
}

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec3 c = base_color.rgb;
	float emit = 0.0;
	if (kind == 0 || kind == 3) {
		vec2 g = vec2(UV.x * (kind == 0 ? panels_around : 12.0), UV.y / panel_height);
		vec2 cell = floor(g);
		// Each panel a slightly different paint, like a hand-painted set.
		c *= 0.93 + 0.1 * hash(cell);
		float seams = max(line(g.x, 0.03), line(g.y, 0.04));
		c *= 1.0 - 0.32 * seams;
		// Rivets near panel corners.
		vec2 f = fract(g) - 0.5;
		vec2 r = abs(f) - vec2(0.43, 0.40);
		float rivet = smoothstep(0.035, 0.02, length(max(r, 0.0)) + max(min(r.x, 0.0), min(r.y, 0.0)) * 0.0 + length(vec2(abs(f.x) - 0.43, abs(f.y) - 0.40)));
		c = mix(c, c * 1.18, rivet);
		// A cool stripe at waist height and grime near the floor (warm/cool split).
		float stripe = smoothstep(0.02, 0.0, abs(UV.y - 1.05) - 0.07);
		if (kind == 0) c = mix(c, vec3(0.30, 0.52, 0.62), stripe * 0.85);
		c *= mix(0.78, 1.0, smoothstep(0.0, 0.7, UV.y));
	} else if (kind == 1) {
		// Rubber floor with a diamond tread.
		vec2 t = UV * 7.0;
		float d = abs(fract(t.x + t.y) - 0.5) + abs(fract(t.x - t.y) - 0.5);
		c *= 0.86 + 0.18 * smoothstep(0.55, 0.45, d);
	} else if (kind == 2) {
		c *= 0.92 + 0.12 * hash(floor(local_pos.xz * 30.0));
	} else if (kind == 4) {
		emit = 2.2;
	} else if (kind == 5) {
		// Quilted seat fabric.
		vec2 q = fract(local_pos.xy * 7.0) - 0.5;
		c *= 0.88 + 0.14 * (1.0 - dot(q, q) * 2.0);
	} else if (kind == 6) {
		c *= 0.95 + 0.08 * hash(floor(local_pos.xz * 12.0));
	}
	ALBEDO = c;
	ROUGHNESS = 1.0;
	EMISSION = c * emit;
}

void light() {
	// Half-Lambert ramp: shadows never go pure black (Mitchell et al. 2007).
	float ndl = dot(NORMAL, LIGHT);
	float hl = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	hl *= hl;
	float lit = smoothstep(0.15, 0.75, hl);
	vec3 ramp = mix(shadow_tint * shadow_floor, vec3(1.0), lit);
	DIFFUSE_LIGHT += ramp * ATTENUATION * LIGHT_COLOR / PI;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += rim_color * fres * rim_strength * ATTENUATION * LIGHT_COLOR / PI;
}
