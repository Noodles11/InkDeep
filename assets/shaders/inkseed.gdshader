// An inkseed (docs/CORRUPTION.md; BlightView.Inkseeds): a small black hole, drawn only while her light is on it (shown).
// Its body lightless black; a thin wavering ring of her light hugging its silhouette (its photon ring); its core a dull
// ember asleep, and awake a deepening red-violet as it swells (swell, 0 → 1; below naught: asleep), flickering faster
// toward its end, its ring brightening with it.
shader_type spatial;
render_mode unshaded, shadows_disabled, depth_draw_always;

instance uniform float shown = 0.0;
instance uniform float swell = -1.0;
instance uniform vec3 ring_color = vec3(1.0, 0.77, 0.42);
instance uniform float seed = 0.0;

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float armed = swell >= 0.0 ? 1.0 : 0.0;
	float s = max(swell, 0.0);
	// Its photon ring: thin, wavering round the silhouette, brighter as it swells.
	float around = atan(NORMAL.y, NORMAL.x);
	float waver = 0.75 + 0.25 * sin(around * 5.0 + TIME * 3.0 + seed);
	float ring = pow(1.0 - facing, 7.0) * waver * (0.9 + 1.4 * s * armed);
	// Its core: a small heart in the middle of it.
	float heart = smoothstep(0.88, 0.99, facing);
	vec3 ember = vec3(0.22, 0.05, 0.12) * 0.5;
	vec3 deep = mix(vec3(0.4, 0.05, 0.3), vec3(1.0, 0.12, 0.55), s);
	float flicker = 0.7 + 0.3 * sin(TIME * (5.0 + 45.0 * s * s) + seed * 3.1);
	vec3 core = mix(ember, deep * (0.5 + 2.2 * s) * flicker, armed) * heart;
	ALBEDO = ring_color * ring * 1.3 + core;
	ALPHA = clamp(shown, 0.0, 1.0);
}
