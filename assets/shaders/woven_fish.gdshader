// A woven reef's small schooling fish (WovenFish), in the reef's paint (woven_reef.gdshaderinc): their bodies beat,
// the tail most, faster while they bolt. Vertex colour: r = light (a darker back), g = how far back along the body
// (0 nose → 1 tail), b = belly to back. Instance custom: r = kind (fish), g = colour, b = fright, a = phase.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always, fog_disabled, shadows_disabled;

#include "res://assets/shaders/woven_reef.gdshaderinc"

void vertex() {
	float back = COLOR.g;
	float beat = TIME * (9.0 + 10.0 * INSTANCE_CUSTOM.b) + INSTANCE_CUSTOM.a * 6.2831;
	VERTEX.x += sin(beat - back * 3.0) * 0.035 * back * back * (1.0 + INSTANCE_CUSTOM.b);
	wr_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wr_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	wr_paint = vec4(COLOR.r, 0.0, COLOR.b, 1.0);
	wr_kind = INSTANCE_CUSTOM.rg;
	wr_uv = vec2(0.0);
}

void fragment() {
	vec4 c = wr_fragment(CAMERA_POSITION_WORLD, FRAGCOORD.xy, SCREEN_UV, VIEWPORT_SIZE, TIME, FRONT_FACING);
	if (c.a < 0.05) discard;
	ALBEDO = c.rgb;
	ALPHA = c.a;
}
