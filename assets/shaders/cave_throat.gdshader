// A cave opening's far end (PortalView, docs/RPG-PROPOSAL.md §2.3): a sheet across the back of its passage in its
// arch's shape, plain dark (the passage darkens into it in the ground's own shader).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// The arch in metres on the sheet: its half-span, the height its round top springs from (over the sheet's foot), and
// the sheet's height.
uniform float half_width = 1.85;
uniform float spring = 2.25;
uniform float height = 4.2;

void fragment() {
	vec2 m = vec2((UV.x - 0.5) * 2.0 * half_width, (1.0 - UV.y) * height);
	float r = length(vec2(m.x, max(m.y - spring, 0.0)));
	ALBEDO = vec3(0.004, 0.006, 0.01);
	ALPHA = 1.0 - smoothstep(half_width - 0.12, half_width, r);
}
