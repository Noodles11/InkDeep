// A leak's arms (AdventureView.DrawArms): five tapering ribbons of ink at her plane, a brittle star's, seen only in
// light (leak_unseen). Black ink in
// little plates along the arm, a fringe of short spines at its edges, a violet rim that brightens on the leak's beat.
// Where her light-staples hold it, its edges sear as her light burns the corrupted (her_burn.gdshaderinc's look): the
// ink bleached to a pale crust lit in her colour from within, crazed with bright seams, a hard bright seam where the
// searing ends and a scorched band past it — brighter while she is sealing. Sealed, it dissolves.
//   UV.x  along the arm (0 at the disc, 1 at its tip); UV.y across it (0..1)
//   COLOR.r  how far along the arm it is stapled (0..1)
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, shadows_disabled;

#include "res://assets/shaders/her_burn.gdshaderinc"

/// Its shimmer now (0..1), how hard she is sealing it (0..1), how far dissolved once sealed (0..1), drunk by the boss (1).
instance uniform vec4 state = vec4(0.0);
instance uniform float seed = 0.0;

varying float v_stapled;
varying vec3 v_world;

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

void vertex() {
	v_stapled = COLOR.r;
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float along = UV.x;
	float across = abs(UV.y * 2.0 - 1.0);
	float shimmer = state.x, sealing = state.y, shut = state.z;
	if (leak_unseen(v_world, seed)) discard;
	// Dissolving once sealed: torn away in patches.
	float n = noise(v_world.xz * 3.0 + seed);
	if (shut > 0.0 && n < shut * 1.15 - 0.05) discard;

	// The plates: a brittle star's arm is ringed in little plates, a darker line between each.
	float plates = smoothstep(0.0, 0.12, abs(fract(along * 18.0 + seed) - 0.5) * 2.0);
	// The spines: short ticks along its edges.
	float spines = step(0.82, across) * step(0.6, fract(along * 36.0 + seed * 0.3));
	vec3 ink = vec3(0.045, 0.02, 0.08) * (0.7 + 0.3 * plates);
	vec3 rim = mix(vec3(0.42, 0.14, 0.75), vec3(0.78, 0.42, 1.0), shimmer);
	float edge = smoothstep(0.55, 1.0, across);
	vec3 albedo = ink;
	vec3 emission = rim * (edge * (0.35 + 0.75 * shimmer) + spines * 0.35);
	float rough = 0.35;

	// Its stapled stretch: the edges sear (most at the edge, reaching in as she seals), the front a hard bright seam.
	float torn = (noise(vec2(along * 40.0, seed + TIME * 0.6)) - 0.5) * 0.04;
	float past = along - v_stapled + torn;
	float held = 1.0 - smoothstep(-0.006, 0.006, past);
	float reach = mix(0.45, 0.2, sealing);
	float rimmed = smoothstep(reach, reach + 0.25, across + (noise(v_world.xz * 9.0 + seed) - 0.5) * 0.2);
	float burn = held * rimmed;
	float seam = (1.0 - smoothstep(0.0, 0.012, abs(past))) * step(0.001, v_stapled) * rimmed;
	float scorch = smoothstep(0.0, 0.01, past) * (1.0 - smoothstep(0.01, 0.06, past)) * rimmed * step(0.001, v_stapled);
	vec3 c = state.w > 0.5 ? vec3(0.3, 0.12, 0.45) : her_light;
	vec3 hot = mix(c, vec3(1.0), 0.35) * 1.2;
	float boil = noise(v_world.xz * 14.0 + vec2(TIME * 2.2, -TIME * 1.7));
	float craze = 1.0 - smoothstep(0.0, 0.08, abs(noise(v_world.xz * 6.0 + vec2(0.0, TIME * 0.35)) - 0.5));
	float flicker = 0.85 + 0.15 * sin(TIME * 19.0 + seed * 7.0 + v_world.x * 3.0);
	albedo = mix(albedo, c * 0.25 + vec3(0.05), burn);
	rough = mix(rough, 0.25, burn);
	emission = mix(emission, emission * 0.2, burn);
	emission += burn * (c * (0.5 + 0.6 * boil) * (0.6 + 0.6 * sealing) + hot * craze * 1.6) * flicker;
	emission = mix(emission, emission * 0.3, scorch);
	emission += hot * seam * 3.5 * flicker;

	ALBEDO = albedo;
	ROUGHNESS = rough;
	SPECULAR = 0.15;
	EMISSION = emission;
}
