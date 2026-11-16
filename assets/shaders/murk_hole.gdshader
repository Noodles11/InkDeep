// The water round a murkling, a small black hole (docs/CORRUPTION.md; BlightView.Murklings): a flat disc in her plane
// at its middle (a PlaneMesh 2 across, scaled to Reach × its radius), drawn only while her light is on it (shown).
// Light bent round it: the picture behind pulled in toward its edge, strongest just outside it, gone at the disc's rim;
// the water shimmering there as air over a hot road; two or three broken rings of ink whirling round it in her plane,
// the nearer the faster. The body itself (drawn whole, nearer the camera) covers the disc's middle.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_mix;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
instance uniform float shown = 0.0;
instance uniform float seed = 0.0;
// Its body's edge, as a share of the disc's radius.
const float BODY = 1.0 / 2.6;

varying vec2 v_q;

void vertex() {
	v_q = VERTEX.xz;
}

float h2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float n2(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h2(i), h2(i + vec2(1.0, 0.0)), f.x), mix(h2(i + vec2(0.0, 1.0)), h2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 q = v_q;
	float r = length(q);
	if (shown < 0.01 || r > 1.0 || r < BODY * 0.97) discard;
	float t = clamp((r - BODY) / (1.0 - BODY), 0.0, 1.0);
	vec2 inward = -q / max(r, 1e-4);
	// The lensing, in the disc's own units: pulled in toward its edge, the more the nearer.
	float pull = 0.22 * BODY * pow(1.0 - t, 2.2);
	// The hot water: a fine shimmer, strongest in the whirl.
	float th = atan(q.y, q.x);
	vec2 heat = vec2(n2(q * 9.0 + vec2(TIME * 1.7, seed)) - 0.5, n2(q * 9.0 + vec2(seed, -TIME * 1.9)) - 0.5) * 0.05 * (1.0 - t);
	vec2 local_off = inward * pull + heat;
	// The disc's units to the screen's pixels (its own derivatives), so the bend is the same whatever the view.
	vec2 dx = dFdx(q), dy = dFdy(q);
	float det = dx.x * dy.y - dx.y * dy.x;
	vec2 px = abs(det) > 1e-9 ? vec2(dy.y * local_off.x - dy.x * local_off.y, -dx.y * local_off.x + dx.x * local_off.y) / det : vec2(0.0);
	vec3 col = textureLod(screen, SCREEN_UV + px / VIEWPORT_SIZE, 0.0).rgb;
	// The whirl: broken rings of ink, the nearer the faster.
	float ink = 0.0;
	for (int k = 0; k < 3; k++) {
		float fk = float(k);
		float ring_r = BODY * (1.18 + 0.32 * fk);
		float spin = TIME * (2.6 / (1.0 + fk)) + seed * (1.0 + fk);
		float arc = smoothstep(0.45, 0.75, n2(vec2((th + spin) * (1.6 + fk * 0.6), fk * 7.0 + seed)));
		float w = BODY * (0.05 + 0.02 * fk);
		float band = 1.0 - smoothstep(w * 0.4, w, abs(r - ring_r - 0.02 * sin(th * 3.0 + spin * 2.0)));
		ink = max(ink, band * arc * (1.0 - 0.25 * fk));
	}
	col = mix(col, vec3(0.008, 0.004, 0.014), ink * 0.85);
	ALBEDO = col;
	ALPHA = shown * (1.0 - smoothstep(0.8, 1.0, r));
}
