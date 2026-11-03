// A flake of a cave's wall ash (AshView): a scrap of washed-purple ash clinging to the rock, torn like old wallpaper —
// a ragged outline with one straighter torn cut, pinholes, a faint grain of fibres, its edges a little darker. Lit as the
// rock is, so it is seen only where light falls on it (a faint violet sheen keeps its hue under her warm light). Its
// colour in COLOR; INSTANCE_CUSTOM.x a seed.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

varying float v_seed;
varying vec3 v_colour;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	v_seed = INSTANCE_CUSTOM.x * 61.0;
	v_colour = COLOR.rgb;
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float a = atan(uv.y, uv.x), r = length(uv);
	// The ragged outline, and one straighter torn cut across it.
	float edge = 0.6 + 0.26 * noise(vec2(a * 1.6 + v_seed, v_seed)) + 0.12 * noise(vec2(a * 6.0 + v_seed * 3.0, v_seed * 0.5));
	vec2 cut_way = vec2(cos(v_seed), sin(v_seed));
	float cut = dot(uv, cut_way) - (0.25 + 0.35 * hash(vec2(v_seed, 1.7))) - 0.08 * (noise(vec2(dot(uv, vec2(-cut_way.y, cut_way.x)) * 9.0, v_seed)) - 0.5);
	if (r > edge || cut > 0.0) discard;
	// Pinholes.
	if (noise(uv * 7.0 + v_seed) > 0.8) discard;
	float fibre = noise(uv * vec2(16.0, 3.0) + v_seed);
	float rim = smoothstep(edge - 0.2, edge, r) + smoothstep(-0.12, 0.0, cut);
	ALBEDO = v_colour * (0.78 + 0.32 * fibre) * mix(1.0, 0.62, clamp(rim, 0.0, 1.0));
	ROUGHNESS = 1.0;
	SPECULAR = 0.08;
	BACKLIGHT = v_colour * 0.25;
	EMISSION = v_colour * 0.05;
}
