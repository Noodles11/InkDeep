// A leak (AdventureView): an X-shaped tear hanging in the water, facing the camera. Nearly invisible: the post pass bends
// the picture through it like hot air (topdown_post.gdshader), and this draws what lies over that: a purple shimmer that
// runs through it on a beat, the faint outline left once she has found it, and the gold staples that close it: two to an
// arm, one arm at a time, each snapping in across the tear. As it seals, they bite tight, the arms close, and all dissolve.
//   UV: the camera-facing quad.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

/// How far each arm is tied (0..1).
instance uniform vec4 ties = vec4(0.0);
/// Its shimmer now (0..1), how plainly it shows once found (0..1), how far sealed (0..1), a tie about to snap (0..1).
instance uniform vec4 state = vec4(0.0);
/// The staples' colour (gold; ink if the boss shut it).
instance uniform vec3 thread = vec3(1.0, 0.6, 0.1);
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

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float shimmer = state.x, seen = state.y, shut = state.z, fray = state.w;
	float closed = smoothstep(0.1, 0.6, shut);
	float gone = smoothstep(noise(p * 7.0 + seed) * 0.5 + 0.45, noise(p * 7.0 + seed) * 0.5 + 0.55, shut);
	float tear = 0.0, gold = 0.0;
	for (int k = 0; k < 4; k++) {
		float a = 0.785398 + 1.570796 * float(k);
		vec2 d = vec2(cos(a), sin(a)), n = vec2(-d.y, d.x);
		float along = dot(p, d) / 0.88, side = dot(p, n);
		if (along < 0.0 || along > 1.0) continue;
		float tie = k == 0 ? ties.x : k == 1 ? ties.y : k == 2 ? ties.z : ties.w;
		// Pulled together as its staples go in.
		side += (noise(vec2(along * 9.0 + float(k) * 5.0, seed)) - 0.5) * 0.07 * (1.0 - closed);
		float width = (0.15 * (1.0 - along * 0.9) + 0.012) * (1.0 - 0.55 * tie) * (1.0 - closed);
		tear = max(tear, 1.0 - smoothstep(width * 0.55, width + 0.01, abs(side)));
		// Two staples across it: the first in over the first half of the tie, the second over the rest. A crown and two
		// short legs biting into the tear's lips.
		for (int n = 0; n < 2; n++) {
			float in_ = clamp(tie * 2.0 - float(n), 0.0, 1.0);
			if (in_ <= 0.0) continue;
			float at = 0.3 + 0.36 * float(n);
			float tremble = fray * (noise(vec2(TIME * 30.0, float(k + n))) - 0.5) * 0.03;
			float s = side + tremble, u = (along - at) * 0.88;
			float t = 0.014 + 0.01 * closed;
			float half_ = (0.13 - 0.05 * at) * (1.0 - 0.3 * closed);
			float crown = step(abs(u), t) * step(abs(s), half_ + t);
			float legs = step(abs(abs(s) - half_), t) * step(-0.05, u) * step(u, t);
			gold = max(gold, max(crown, legs) * smoothstep(0.0, 0.5, in_));
		}
	}
	vec3 purple = mix(vec3(0.42, 0.14, 0.75), vec3(0.78, 0.42, 1.0), shimmer);
	float tearShown = tear * (seen * 0.14 + shimmer * 0.42) * (1.0 - closed);
	ALBEDO = mix(purple, thread, gold);
	ALPHA = clamp(max(tearShown, gold) * (1.0 - gone), 0.0, 1.0);
}
