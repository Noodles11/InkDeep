// A leak (AdventureView): an X-shaped tear hanging in the water, facing the camera. Nearly invisible: the post pass bends
// the picture through it like hot air (topdown_post.gdshader), and this draws what lies over that: a purple shimmer that
// runs through it on a beat, the faint outline left once she has found it, and the golden threads that tie it, one arm at
// a time, stitching each from the middle out. As it seals, the threads pull tight, the arms close, and both dissolve.
//   UV: the camera-facing quad.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

/// How far each arm is tied (0..1).
instance uniform vec4 ties = vec4(0.0);
/// Its shimmer now (0..1), how plainly it shows once found (0..1), how far sealed (0..1), a tie about to snap (0..1).
instance uniform vec4 state = vec4(0.0);
/// The thread's colour (gold; ink if the boss shut it).
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
		// The tied length, from the middle out.
		float held = 1.0 - smoothstep(tie * 1.12 - 0.12, tie * 1.12, along);
		side += (noise(vec2(along * 9.0 + float(k) * 5.0, seed)) - 0.5) * 0.07 * (1.0 - closed);
		float width = (0.15 * (1.0 - along * 0.9) + 0.012) * (1.0 - 0.55 * held) * (1.0 - closed);
		tear = max(tear, 1.0 - smoothstep(width * 0.55, width + 0.01, abs(side)));
		// The thread: a zigzag across the tear, stitched from the middle out, trembling where it is about to snap.
		float stitch = (abs(fract(along * 9.0) - 0.5) * 4.0 - 1.0) * (0.065 * (1.0 - along * 0.8)) * (1.0 - closed * 0.8);
		stitch += fray * (noise(vec2(along * 20.0, TIME * 30.0)) - 0.5) * 0.05;
		float thick = 0.008 + 0.02 * closed;
		gold = max(gold, held * (1.0 - smoothstep(thick, thick + 0.016, abs(side - stitch))));
	}
	vec3 purple = mix(vec3(0.42, 0.14, 0.75), vec3(0.78, 0.42, 1.0), shimmer);
	float tearShown = tear * (seen * 0.14 + shimmer * 0.42) * (1.0 - closed);
	ALBEDO = mix(purple, thread, gold);
	ALPHA = clamp(max(tearShown, gold) * (1.0 - gone), 0.0, 1.0);
}
