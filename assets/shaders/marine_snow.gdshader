// The water's plankton (MarineSnow): tiny plain dots mixed over the scene, one dull grey at every depth, with no
// glow, no glitter and no twinkle.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float fade_far = 9.0;

varying float v_fade;

void vertex() {
	float d = length((VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz);
	v_fade = smoothstep(0.2, 0.8, d) * (1.0 - smoothstep(fade_far * 0.5, fade_far, d));
}

void fragment() {
	ALBEDO = vec3(0.28, 0.33, 0.35);
	ALPHA = (1.0 - smoothstep(0.55, 1.0, length(UV * 2.0 - 1.0))) * 0.5 * v_fade;
}
