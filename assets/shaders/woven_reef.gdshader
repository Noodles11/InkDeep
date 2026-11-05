// The woven reef's coral (woven_reef.gdshaderinc): the rigid pieces, closed meshes.
shader_type spatial;
render_mode unshaded, world_vertex_coords, cull_back, depth_draw_always, fog_disabled, shadows_disabled;

#include "res://assets/shaders/woven_reef.gdshaderinc"

void vertex() {
	wr_vertex(VERTEX, COLOR, INSTANCE_CUSTOM, TIME);
	WR_VARYINGS
}

void fragment() {
	vec4 c = wr_fragment(CAMERA_POSITION_WORLD, FRAGCOORD.xy, SCREEN_UV, VIEWPORT_SIZE, TIME, FRONT_FACING);
	if (c.a < 0.05) discard;
	ALBEDO = c.rgb;
	ALPHA = c.a;
}
