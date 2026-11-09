// A freed murkling's core (docs/LIGHT-AND-GRAPH-PROPOSAL.md §4.2; CoreView.cs): the light it had drunk, hanging where
// its ink shell splashed apart. Not fire: a soft ball of her light, brightest at its heart, its edge melting into the
// water, swelling and thinning as it spreads out over its second.
//   tint  her light's colour
//   fade  how much of its time is gone (0 new, 1 gone)
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
instance uniform float fade = 0.0;

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float heart = pow(facing, 2.2);
	float left = 1.0 - fade;
	float pulse = 0.85 + 0.15 * sin(TIME * 11.0);
	ALBEDO = mix(tint, vec3(1.0), heart * 0.6);
	ALPHA = clamp(heart * left * left * pulse * 1.4, 0.0, 1.0);
}
