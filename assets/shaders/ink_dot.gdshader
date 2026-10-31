// A speck of the ink in the water (AdventureView): a tiny plain purple dot like the plankton, drifting a little, on any of
// the water's layers, with no glow. Camera-facing quads in one MultiMesh; the instance's scale is its size in metres
// across, INSTANCE_CUSTOM.x a seed.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying vec3 v_ink;

void vertex() {
	float seed = INSTANCE_CUSTOM.x;
	float t = TIME * (0.25 + 0.3 * fract(seed * 7.3));
	vec3 drift = vec3(sin(t + seed * 6.28), sin(t * 0.8 + seed * 11.0) * 0.6, cos(t * 0.9 + seed * 17.0)) * 0.12;
	v_ink = mix(vec3(0.5, 0.2, 0.82), vec3(0.34, 0.12, 0.6), step(0.5, fract(seed * 13.7)));
	vec3 at = MODEL_MATRIX[3].xyz + drift;
	float size = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * size, INV_VIEW_MATRIX[1] * size, INV_VIEW_MATRIX[2] * size, vec4(at, 1.0));
}

void fragment() {
	ALBEDO = v_ink;
	ALPHA = (1.0 - smoothstep(0.55, 1.0, length(UV * 2.0 - 1.0))) * 0.85;
}
