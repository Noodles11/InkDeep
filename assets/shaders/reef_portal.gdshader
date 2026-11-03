// The open reef's current station (docs/RPG-PROPOSAL.md §2–3), on the floor: a bright swirl of streaming water.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 tint : source_color = vec3(0.6, 0.95, 1.0);

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float disc = 1.0 - smoothstep(0.6, 1.0, r);
	float swirl = sin(a * 6.0 + r * 10.0 - TIME * 3.2) * 0.5 + 0.5;
	float ring = 1.0 - smoothstep(0.0, 0.12, abs(r - 0.72));
	ALBEDO = tint * (0.75 + 0.5 * swirl);
	ALPHA = disc * (0.18 + 0.42 * swirl * smoothstep(0.05, 0.75, r)) + ring * 0.45 * disc;
}
