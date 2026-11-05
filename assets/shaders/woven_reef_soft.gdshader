// The woven reef's soft things (woven_reef.gdshaderinc): sea fans (their net cut by `lattice`) and soft-coral tufts,
// thin and two-sided.
shader_type spatial;
render_mode unshaded, world_vertex_coords, cull_disabled, depth_draw_always, fog_disabled, shadows_disabled;

#include "res://assets/shaders/woven_reef.gdshaderinc"

uniform float lattice = 0.0;

void vertex() {
	wr_vertex(VERTEX, COLOR, INSTANCE_CUSTOM, TIME);
	WR_VARYINGS
}

void fragment() {
	if (lattice > 0.0) {
		// A sea fan's net: veins fanning from its stalk, crossed by finer links.
		float veins = abs(fract(wr_uv.x * 13.0 + wr_noise(vec3(wr_uv * 7.0, 0.0)) * 0.5) - 0.5);
		float links = abs(fract(wr_uv.y * 16.0 + sin(wr_uv.x * 31.0) * 0.2) - 0.5);
		if (veins > 0.14 && links > 0.16 && wr_uv.y > 0.12) discard;
	}
	vec4 c = wr_fragment(CAMERA_POSITION_WORLD, FRAGCOORD.xy, SCREEN_UV, VIEWPORT_SIZE, TIME, FRONT_FACING);
	if (c.a < 0.05) discard;
	ALBEDO = c.rgb;
	ALPHA = c.a;
}
