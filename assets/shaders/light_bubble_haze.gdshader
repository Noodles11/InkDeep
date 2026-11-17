// The water burning round her light bubble (CombatView): a disc of shimmering water round it, the picture behind bent
// as air over a hot road (the scene drawn so far, read and offset by a churning noise), strongest near the bubble and
// gone at the disc's rim. Faces the camera.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
instance uniform float seed = 0.0;
instance uniform float strength = 1.0;

float h_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float h_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h_hash(i), h_hash(i + vec2(1.0, 0.0)), f.x), mix(h_hash(i + vec2(0.0, 1.0)), h_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	// Billboard, keeping the node's scale.
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
	vec2 q = UV * 2.0 - 1.0;
	float d = length(q);
	if (d > 1.0) discard;
	// Strongest in a ring just round the bubble, fading to nothing at the rim.
	float k = smoothstep(1.0, 0.45, d) * smoothstep(0.12, 0.35, d) * strength;
	// The churn: two layers of noise rising and swirling.
	vec2 n = vec2(h_noise(q * 3.5 + vec2(seed, TIME * 2.2)), h_noise(q * 3.5 + vec2(TIME * 1.7, seed + 5.0))) - 0.5;
	n += (vec2(h_noise(q * 8.0 - vec2(TIME * 3.1, seed)), h_noise(q * 8.0 + vec2(seed, -TIME * 2.6))) - 0.5) * 0.5;
	vec2 bent = SCREEN_UV + n * k * 0.012;
	vec3 behind = textureLod(screen_tex, bent, 0.0).rgb;
	// (A breath of warmth in the bent water, as heat shows.)
	ALBEDO = behind * (1.0 + 0.08 * k);
	ALPHA = k;
}
