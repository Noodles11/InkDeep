// A cave opening's far end (PortalView, docs/RPG-PROPOSAL.md §2.3): a sheet across the back of its passage in its
// arch's shape (the passage darkens into it in the ground's own shader). A region mouth's is the Leak's own black, its
// slow violet ink stirring; a leak cave's way out shows the water outside shining in, soft and cool.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// The arch in metres on the sheet: its half-span, the height its round top springs from (over the sheet's foot), and
// the sheet's height.
uniform float half_width = 1.85;
uniform float spring = 2.25;
uniform float height = 4.2;
uniform float way_out = 0.0;

void fragment() {
	vec2 m = vec2((UV.x - 0.5) * 2.0 * half_width, (1.0 - UV.y) * height);
	float r = length(vec2(m.x, max(m.y - spring, 0.0)));
	float inside = 1.0 - smoothstep(half_width - 0.12, half_width, r);
	vec3 dark = mix(vec3(0.01, 0.003, 0.02), vec3(0.006, 0.01, 0.02), way_out);
	// Round the arch's middle, a little over the floor.
	vec2 c = vec2(m.x, m.y - spring * 0.75);
	float d = length(c) / half_width;
	float a = atan(c.x, c.y);
	float ink = (sin(a * 3.0 + d * 6.0 - TIME * 0.7) * 0.5 + 0.5) * (1.0 - smoothstep(0.15, 1.1, d));
	vec3 leak = dark + vec3(0.3, 0.1, 0.55) * ink * 0.3;
	float glow = pow(1.0 - smoothstep(0.0, 1.25, d), 1.4);
	vec3 outside = mix(dark, vec3(0.32, 0.55, 0.72), glow * (0.75 + 0.1 * sin(TIME * 0.8 + m.x)));
	ALBEDO = mix(leak, outside, way_out);
	ALPHA = inside;
}
