// A level's frame (DESIGN-TOPDOWN §4.6). The level below sits under the current one, 8 m down and moved so its start
// lies under the hole; when Clementine has dived, the whole world is moved back to the origin in one frame. So every
// level shader works in its level's own coordinates:
//   level_offset  where the level sits in the world right now (its node's translation)
//   level_origin  a fixed absolute position (xz) of the level's own origin, for patterns: they never jump when the
//                 level is moved
//   portal        the dive's opening: world xz centre, radius, and mode (0 off, 1 this level is cut away inside it,
//                 2 only the inside is drawn) — the level above opens like an iris onto the level below
//   see_through   her world xz, how far round her the reef is cut open, and how open (0–1); see_through_y the
//   see_through_y height of her plane — what stands between the camera and her plane round her (a wall in front of
//                 her, a tunnel's roof, the coral on them) is cut away as she swims (lf_see_cut), so nothing hides her
// Instance-free uniforms: set per material by LevelView.

uniform vec3 level_offset = vec3(0.0);
uniform vec2 level_origin = vec2(0.0);
uniform vec4 portal = vec4(0.0);
uniform vec4 see_through = vec4(0.0);
uniform float see_through_y = 0.0;
// The map round her for the cutout (LevelView.SeeHeights; box: x, z of its corner, its span in metres, 0 for none):
// see_heights its height (rock, roofs and lids), see_keep 1 where the cut takes the rock over it (the walls in front of
// her, whole: LevelView.SeeKeep), 0 where it stays.
uniform sampler2D see_heights : filter_linear, repeat_disable;
uniform sampler2D see_keep : filter_nearest, repeat_disable;
uniform vec3 see_heights_box = vec3(0.0);

// The dissolve threshold at a pixel (0..1): interleaved gradient noise, fine and even, with no grid to it (an ordered
// 4×4 dither showed as a screen door over a fade).
float lf_dither(vec2 frag) {
	return fract(52.9829189 * fract(dot(floor(frag), vec2(0.06711056, 0.00583715))));
}

// A world position in this level's own coordinates.
vec3 lf_local(vec3 world) { return world - level_offset; }

// A local position where patterns are drawn: absolute across the dive.
vec3 lf_pattern(vec3 local) { return vec3(local.x + level_origin.x, local.y, local.z + level_origin.y); }

// The cutout: while something stands between the camera and her, everything on the camera's side of her (from
// see_through.z metres behind her, out past the screen's edges) is cut off flat LF_SEE_OVER over her level's swim plane
// (see_through_y): the walls and peaks in front of her gone down to their stumps, showing the reef's cap there (a flat
// dotted fill drawn inside all the rock at that height at all times, topdown_ground's cap), and outlined. What stands
// behind her stays whole. It is on (see_through.w > 0) or off: no transition.

// How open it is, and the height it cuts at (the caps' own: LevelView.CapHeight).
float lf_see_open() { return clamp(see_through.w, 0.0, 1.0); }
const float LF_SEE_OVER = 0.5;
float lf_see_height() { return see_through_y + LF_SEE_OVER; }
// (The lowest height anything is cut at.)
float lf_see_slice() { return lf_see_height(); }

// How far (metres) a point stands on the camera's side of the cut's back line (> 0: on the camera's side, cut there).
float lf_see_front(vec3 world, vec3 cam) {
	vec2 toward = normalize(cam.xz - see_through.xy + vec2(1e-5, 0.0));
	return dot(world.xz - see_through.xy, toward) + see_through.z;
}

// Whether the cut takes what stands over a point (at any height over the cut): on the map round her, as it says (whole
// walls, LevelView.SeeKeep); past it, what lies on the camera's side of her.
bool lf_see_column(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0) return false;
	if (see_heights_box.z > 0.0) {
		vec2 uv = (world.xz - see_heights_box.xy) / see_heights_box.z;
		if (uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0) return texture(see_keep, uv).r > 0.5;
	}
	return lf_see_front(world, cam) > 0.0;
}

// How far (metres) a point is from the cut away (< 0 inside it, > 0 outside), for the cut and its outline.
float lf_see_dist(vec3 world, vec3 cam) {
	if (!lf_see_column(world, cam)) return 1e3;
	return lf_see_height() - world.y;
}

// lf_see_dist, and whether the point may be cut at all (in .y).
vec2 lf_see_edge(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0) return vec2(1e3, 0.0);
	return vec2(lf_see_dist(world, cam), 1.0);
}

// True where something in front of her is cut away: its edge sharp.
bool lf_see_cut(vec3 world, vec3 cam, vec2 frag) {
	return see_through.w > 0.0 && lf_see_dist(world, cam) < 0.0;
}

// The outline along the cut (0..1), on what is left at its edge.
float lf_see_rim(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0) return 0.0;
	float e = lf_see_dist(world, cam);
	return e >= 0.0 && e < 0.3 ? 1.0 : 0.0;
}

// The outline's colour (the cut's dots').
const vec3 LF_SEE_RIM = vec3(0.26, 0.27, 0.3);

// True where the portal hides this pixel; its edge dissolves over a few metres.
bool lf_portal_cut(vec3 world, vec2 frag) {
	if (portal.w < 0.5) return false;
	float inside = 1.0 - smoothstep(-3.0, 3.0, distance(world.xz, portal.xy) - portal.z);
	float t = lf_dither(frag);
	return portal.w < 1.5 ? inside > t : 1.0 - inside > t;
}
