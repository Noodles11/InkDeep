// Her light bubble (LightBubbleTuning; CombatView): a clear sphere filled with white light. Its skin all but unseen face
// on, a bright thin rim where it turns away (as a bubble's film catches the light), and inside a soft white glow,
// brightest at its heart, its light shifting a little as it flies.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;

instance uniform float seed = 0.0;
instance uniform vec3 light_color = vec3(1.0);

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Its film: a thin rim, wavering.
	float around = atan(NORMAL.y, NORMAL.x);
	float rim = pow(1.0 - facing, 4.0) * (0.85 + 0.15 * sin(around * 3.0 + TIME * 5.0 + seed));
	// The light in it: a glow from its heart outward.
	float heart = pow(facing, 2.5);
	float shimmer = 0.9 + 0.1 * sin(TIME * 13.0 + seed * 7.0);
	ALBEDO = light_color * (rim * 1.1 + heart * 0.85 * shimmer);
	ALPHA = 1.0;
}
