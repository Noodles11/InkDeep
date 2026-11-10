// A leak's disc (AdventureView): the brittle star's middle, lying flat at her plane, where the ink comes through. Seen
// only in light (leak_unseen). A
// dark disc of ink with five lobes toward its arms, a violet rim that brightens on the leak's beat, and a slow swirl
// at its heart (the post pass bends the water over it like hot air, topdown_post.gdshader). As her light staples its
// arms it pales toward her gold from the rim in; sealed, it dissolves.
//   UV: the flat quad, the disc in its middle.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, shadows_disabled;

#include "res://assets/shaders/her_burn.gdshaderinc"

/// Its shimmer now (0..1), how full of her light it is (0..1), how far dissolved once sealed (0..1), drunk by the boss (1).
instance uniform vec4 state = vec4(0.0);
instance uniform float seed = 0.0;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}


// Seen only in light, as a murkling is (murkling.gdshaderinc): within her aura, where a beam of hers falls on it, or in a
// lit crystal's light (crystal_seen, AdventureView); elsewhere it is not drawn at all, its edge breaking up in the water.
instance uniform float crystal_seen = 0.0;
bool leak_unseen(vec3 world, float salt) {
	float seen = max(max(her_reveal_aura(world), her_reveal_beams(world)), crystal_seen);
	return seen < 0.15 + 0.7 * noise(world.xz * 4.0 + vec2(TIME * 0.4, salt));
}

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (leak_unseen(v_world, seed)) discard;
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x);
	float shimmer = state.x, glow = state.y, shut = state.z;
	// Five soft lobes toward its arms.
	float edge = 0.62 + 0.08 * cos(a * 5.0 + seed);
	if (r > edge) discard;
	float n = noise(p * 6.0 + seed);
	if (shut > 0.0 && n < shut * 1.15 - 0.05) discard;
	float swirl = noise(vec2(a * 2.0 + r * 6.0 - TIME * 0.8, r * 3.0 + seed));
	vec3 rim = mix(vec3(0.42, 0.14, 0.75), vec3(0.78, 0.42, 1.0), shimmer);
	float lip = smoothstep(edge - 0.18, edge, r);
	vec3 c = state.w > 0.5 ? vec3(0.3, 0.12, 0.45) : her_light;
	// Her light taking it, from the rim in.
	float taken = smoothstep(edge * (1.0 - glow) - 0.05, edge * (1.0 - glow) + 0.05, r) * step(0.001, glow);
	ALBEDO = mix(vec3(0.03, 0.012, 0.06) * (0.8 + 0.4 * swirl), c * 0.25, taken);
	ROUGHNESS = 0.3;
	EMISSION = rim * (lip * (0.3 + 0.9 * shimmer) + swirl * 0.12 * (1.0 - r)) * (1.0 - taken) + c * taken * 0.6;
}
