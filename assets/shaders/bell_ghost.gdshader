// An afterimage of Clementine as she dashes (BellView's ghosts): her bell's shape left behind in her light's colour,
// brightest at its rim, fading away (fade 1 → 0).
shader_type spatial;
render_mode unshaded, blend_add, cull_back, depth_draw_never, shadows_disabled;

global uniform vec3 her_light;
instance uniform float fade = 1.0;

void fragment() {
	float rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Mostly its rim, the middle only a breath: a see-through copy of her, not a disc.
	float a = fade * (0.04 + 0.75 * pow(rim, 2.4));
	ALBEDO = mix(her_light, vec3(1.0), 0.15) * a;
}
