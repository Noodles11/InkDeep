// An inkseed (docs/CORRUPTION.md; BlightView.Inkseeds): an ink bomb, a plain black sphere, drawn only while her light
// is on it (shown). Glossy black: a soft highlight and a thin pale rim round its silhouette, so it reads as a solid
// ball, never as a creature; neither takes any hue (the ink look would make a colour of it). On its crown its
// detonator: a dull spark asleep; set off (state ≥ 0), a red spark blinking, and as her light defuses it (state 0 → 1) slowing and cooling to her light's colour; set off by her near
// (fuse), blinking fast and white-hot.
shader_type spatial;
render_mode unshaded, shadows_disabled, depth_draw_always;

instance uniform float shown = 0.0;
instance uniform float state = -1.0;
instance uniform float fuse = 0.0;
instance uniform vec3 ring_color = vec3(1.0, 0.77, 0.42);
instance uniform float seed = 0.0;

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Its body: near black, a little lifted toward its rim, a soft highlight up and to the left, a thin pale rim.
	vec3 body = vec3(0.012, 0.012, 0.016) + vec3(0.03, 0.03, 0.035) * pow(1.0 - facing, 2.0);
	vec3 to_light = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float gloss = pow(clamp(dot(to_light, normalize(vec3(-0.45, 1.0, -0.35))), 0.0, 1.0), 24.0);
	float rim = pow(1.0 - facing, 6.0);
	vec3 col = body + vec3(0.5) * gloss + vec3(0.42, 0.43, 0.46) * rim * 0.35;
	// Its detonator, on its crown.
	vec3 up = to_light;
	float spark = smoothstep(0.86, 0.93, dot(up, normalize(vec3(0.0, 1.0, 0.45))));
	float armed = state >= 0.0 ? 1.0 : 0.0;
	float defused = clamp(state, 0.0, 1.0);
	float rate = fuse > 0.5 ? 18.0 : mix(4.0, 1.2, defused);
	float blink = armed > 0.5 ? step(0.5, fract(TIME * rate + seed)) : 0.35;
	vec3 hot = fuse > 0.5 ? vec3(1.0, 0.85, 0.7) : mix(vec3(1.0, 0.12, 0.06), ring_color, defused);
	col += mix(vec3(0.25, 0.08, 0.04), hot * 1.6, armed) * spark * blink;
	ALBEDO = col;
	ALPHA = clamp(shown, 0.0, 1.0);
}
