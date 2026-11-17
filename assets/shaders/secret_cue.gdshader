// An arrow carved into the floor at a wall's foot, pointing to a secret's mouth (SecretCueView; docs/DIVE.md §6.1):
// wholly unseen until her beam falls on it, then its groove glows a pale moonlit blue, its edges worn, a slow shimmer
// running along it toward where it points.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

#include "res://assets/shaders/reef_show.gdshaderinc"

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float cue_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float cue_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(cue_hash(i), cue_hash(i + vec2(1.0, 0.0)), f.x), mix(cue_hash(i + vec2(0.0, 1.0)), cue_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	// (x across, y along the way it points.)
	vec2 q = UV * 2.0 - 1.0;
	q.y = -q.y;
	q += (vec2(cue_noise(q * 6.0), cue_noise(q * 6.0 + 7.0)) - 0.5) * 0.06;
	float aa = fwidth(q.y) * 1.5;
	// Its shaft and its head (a broad chevron).
	float shaft = (1.0 - smoothstep(0.07, 0.07 + aa, abs(q.x))) * smoothstep(-0.75, -0.75 + aa, q.y) * (1.0 - smoothstep(0.35, 0.35 + aa, q.y));
	float outer = 0.85 - 1.1 * abs(q.x), inner = 0.5 - 1.1 * abs(q.x);
	float head = (1.0 - smoothstep(outer, outer + aa, q.y)) * smoothstep(inner, inner + aa, q.y) * (1.0 - smoothstep(0.62, 0.62 + aa, abs(q.x)));
	float carved = max(shaft, head);
	// Worn: broken here and there.
	carved *= smoothstep(0.18, 0.4, cue_noise(q * 9.0 + 3.0));
	// Shown only in her beam (not her glow): a share of its light that falls here.
	float beam = rs_beam(rs_seen(world_pos, CAMERA_POSITION_WORLD));
	float shown = smoothstep(0.04, 0.35, beam);
	float run = 0.75 + 0.25 * sin(q.y * 9.0 - TIME * 4.0);
	ALBEDO = vec3(0.62, 0.78, 1.0) * (0.9 + 0.6 * run);
	ALPHA = carved * shown * 0.92;
}
