// Her beam in the water (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.2; BeamView.cs): a fan of her light from her bell along her
// aim, laid flat over her plane. Bright at her, dimming as it goes (as the sim's falloff), full across the middle of
// the cone and fading out to its edges, a slow shimmer running down it. Rock stops it: past `stop` (a share of its
// reach) nothing shows.
//   tint      her light's colour
//   strength  how bright (0: off; the view eases it in and out)
//   half_angle the cone's half-angle (radians)
//   stop      where rock stops it, as a share of its reach (1: nothing in the way)
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
uniform float strength = 0.0;
uniform float half_angle = 0.38;
uniform float stop = 1.0;
uniform float soft = 0.7;

varying vec2 local;

void vertex() {
	local = VERTEX.xz;
}

float bh_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = fract(sin(dot(i, vec2(127.1, 311.7))) * 43758.5453);
	float b = fract(sin(dot(i + vec2(1.0, 0.0), vec2(127.1, 311.7))) * 43758.5453);
	float c = fract(sin(dot(i + vec2(0.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
	float d = fract(sin(dot(i + vec2(1.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	float along = length(local);
	float across = abs(atan(local.y, local.x)) / half_angle;
	float fall = pow(max(1.0 - along, 0.0), 1.6);
	float edge = 1.0 - smoothstep(soft, 1.0, across);
	float cut = 1.0 - smoothstep(stop - 0.015, stop + 0.01, along);
	// Not over her own bell: it begins a little out from her heart.
	float near = smoothstep(0.0, 0.05, along);
	float shimmer = 0.8 + 0.2 * bh_noise(vec2(along * 14.0 - TIME * 1.6, across * 3.0 + TIME * 0.3));
	ALBEDO = tint;
	ALPHA = clamp(strength * fall * edge * cut * near * shimmer, 0.0, 1.0);
}
