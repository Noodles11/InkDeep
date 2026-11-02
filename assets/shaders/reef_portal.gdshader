// The open reef's ways through (docs/RPG-PROPOSAL.md §2–3), on the floor: a current station (dark 0), a bright
// swirl of streaming water; a cave mouth (dark 1), the Leak's black throat ringed by slow violet swirls.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 tint : source_color = vec3(0.6, 0.95, 1.0);
uniform float dark = 0.0;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float disc = 1.0 - smoothstep(0.6, 1.0, r);
	if (dark > 0.5) {
		float swirl = sin(a * 4.0 + r * 7.0 - TIME * 1.3) * 0.5 + 0.5;
		vec3 ink = vec3(0.025, 0.015, 0.05);
		vec3 rim = vec3(0.42, 0.22, 0.7);
		ALBEDO = mix(ink, rim, smoothstep(0.25, 0.95, r) * swirl * 0.85);
		ALPHA = disc * mix(0.95, 0.55, smoothstep(0.4, 1.0, r));
	} else {
		float swirl = sin(a * 6.0 + r * 10.0 - TIME * 3.2) * 0.5 + 0.5;
		float ring = 1.0 - smoothstep(0.0, 0.12, abs(r - 0.72));
		ALBEDO = tint * (0.75 + 0.5 * swirl);
		ALPHA = disc * (0.18 + 0.42 * swirl * smoothstep(0.05, 0.75, r)) + ring * 0.45 * disc;
	}
}
