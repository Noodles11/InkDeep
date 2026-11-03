// A cave's fluorescent plankton (GlowPlankton): a tiny speck of living light, its colour and how brightly it glows now
// in COLOR (rgb, a), soft at its edge, bright enough at its heart to bloom a little. Added over the dark.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float fade_far = 70.0;

varying float v_fade;

void vertex() {
	float d = length((VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz);
	v_fade = smoothstep(0.3, 1.2, d) * (1.0 - smoothstep(fade_far * 0.6, fade_far, d));
}

void fragment() {
	float r = length(UV * 2.0 - 1.0);
	float core = 1.0 - smoothstep(0.0, 0.35, r);
	float halo = 1.0 - smoothstep(0.2, 1.0, r);
	ALBEDO = COLOR.rgb * (core * 2.4 + halo * halo * 0.5) * COLOR.a * v_fade;
}
