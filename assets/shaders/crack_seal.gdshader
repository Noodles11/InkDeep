// A cracked seal's crack (CrackSealView, docs/DIVE.md §6.1): a jagged line of faint light up the seal's rock, cut with
// the rock round it where the cutout takes the wall (so it never shows over the cut's cap).
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

#include "res://assets/shaders/level_frame.gdshaderinc"

uniform vec3 glow : source_color = vec3(0.75, 0.92, 1.0);
// How bright its light is now (it breathes).
uniform float energy = 0.6;

varying vec3 world;
void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	if (lf_see_cut(world, CAMERA_POSITION_WORLD, FRAGCOORD.xy)) discard;
	ALBEDO = glow * (0.6 + energy);
}
