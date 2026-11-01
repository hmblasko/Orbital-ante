// Space sky: two layers of stars, a dusty galactic band with faint nebula
// tints, and a sun disc with a soft corona that follows the scene's sun.
// Stylised, not photoreal (CLAUDE.md section 11). Original work.
shader_type sky;
render_mode use_half_res_pass;

uniform vec3 deep_color : source_color = vec3(0.010, 0.012, 0.030);
uniform vec3 band_color : source_color = vec3(0.34, 0.30, 0.46);
uniform vec3 nebula_warm : source_color = vec3(0.50, 0.30, 0.55);
uniform vec3 nebula_cool : source_color = vec3(0.12, 0.32, 0.55);
uniform vec3 band_normal = vec3(0.35, 0.80, -0.48);
uniform float band_width = 0.22;
uniform float band_strength = 0.55;
uniform float star_density = 220.0;
uniform float star_brightness = 2.2;
uniform float sun_disc_size = 0.022;
uniform float corona_strength = 0.6;

float hash13(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

vec3 hash33(vec3 p) {
	p = vec3(dot(p, vec3(127.1, 311.7, 74.7)), dot(p, vec3(269.5, 183.3, 246.1)), dot(p, vec3(113.5, 271.9, 124.6)));
	return fract(sin(p) * 43758.5453123);
}

float value_noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash13(i), hash13(i + vec3(1, 0, 0)), f.x), mix(hash13(i + vec3(0, 1, 0)), hash13(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash13(i + vec3(0, 0, 1)), hash13(i + vec3(1, 0, 1)), f.x), mix(hash13(i + vec3(0, 1, 1)), hash13(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float fbm(vec3 p) {
	float s = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		s += a * value_noise(p);
		p *= 2.03;
		a *= 0.5;
	}
	return s;
}

// One layer of point stars: one candidate per cell, most cells empty.
vec3 stars(vec3 dir, float scale, float chance) {
	vec3 p = dir * scale;
	vec3 cell = floor(p);
	vec3 offset = hash33(cell) * 0.8 + 0.1;
	float present = step(1.0 - chance, hash13(cell + 17.0));
	float d = length(fract(p) - offset);
	float size = mix(0.035, 0.11, hash13(cell + 3.0));
	float glow = smoothstep(size, 0.0, d);
	float t = hash13(cell + 9.0);
	vec3 tint = mix(vec3(0.75, 0.85, 1.0), vec3(1.0, 0.86, 0.68), t);
	return tint * glow * present * mix(0.4, 1.0, hash13(cell + 5.0));
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	if (AT_HALF_RES_PASS) {
		// Soft, expensive parts at half resolution.
		vec3 col = deep_color;
		float across = dot(dir, normalize(band_normal));
		float band = exp(-pow(across / band_width, 2.0));
		float dust = fbm(dir * 3.5);
		float wisps = fbm(dir * 7.0 + 4.0);
		col += band_color * band * band_strength * smoothstep(0.35, 0.8, dust);
		// Dark dust lane through the middle of the band.
		col *= 1.0 - 0.55 * band * smoothstep(0.55, 0.75, wisps) * exp(-pow(across / (band_width * 0.35), 2.0));
		// Faint nebula colour patches.
		float n1 = smoothstep(0.55, 0.85, fbm(dir * 1.7 + 11.0));
		float n2 = smoothstep(0.58, 0.88, fbm(dir * 2.1 - 7.0));
		col += nebula_warm * n1 * 0.12 + nebula_cool * n2 * 0.14;
		COLOR = col;
		ALPHA = band;
	} else {
		vec3 col = HALF_RES_COLOR.rgb;
		float band = HALF_RES_COLOR.a;
		// Stars: a dense faint layer and a sparse bright one, denser in the band.
		vec3 s = stars(dir, star_density, 0.18 + 0.25 * band) * 0.6 + stars(dir, star_density * 0.35, 0.10) * 1.4;
		col += s * star_brightness;
		// Sun disc and corona.
		if (LIGHT0_ENABLED) {
			vec3 to_sun = normalize(LIGHT0_DIRECTION);
			float a = acos(clamp(dot(dir, to_sun), -1.0, 1.0));
			float disc = smoothstep(sun_disc_size, sun_disc_size * 0.75, a);
			float corona = exp(-a * 9.0) * corona_strength + exp(-a * 2.5) * corona_strength * 0.15;
			col += LIGHT0_COLOR * (disc * 18.0 + corona);
		}
		COLOR = col;
	}
}
