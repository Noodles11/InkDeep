// The Crack, open (DESIGN-TOPDOWN §4.6): the arena floor simply stops, and in the rift lies the next depth's own water,
// flush with the ground round it, not a pit. Drawn over the water pass, so it is that depth's colour and not this one's.
// Its edge melts into the floor, and a slow shimmer moves through it. Opens as the sand over it drains; the dive's iris
// cuts it away with the level.
//   UV.x: how far out from the middle (0 → 1 at the rim).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://assets/shaders/level_frame.gdshaderinc"

/// The next depth's water.
uniform vec3 water : source_color = vec3(0.2, 0.5, 0.6);
/// How far it has opened (0 none, 1 open).
uniform float open = 0.0;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (lf_portal_cut(world, FRAGCOORD.xy)) discard;
	vec2 q = world.xz * 0.35;
	float shimmer = sin(q.x + TIME * 0.5 + 1.7 * sin(q.y * 0.8 - TIME * 0.3)) * sin(q.y * 1.3 - TIME * 0.4);
	ALBEDO = water * (1.0 + 0.06 * shimmer);
	ALPHA = open * 0.62 * (1.0 - smoothstep(0.25, 1.0, UV.x));
}
