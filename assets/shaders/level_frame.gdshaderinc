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

// The dissolve threshold at a pixel (0..1): interleaved gradient noise, fine and even, with no grid to it (an ordered
// 4×4 dither showed as a screen door over a fade).
float lf_dither(vec2 frag) {
	return fract(52.9829189 * fract(dot(floor(frag), vec2(0.06711056, 0.00583715))));
}

// A world position in this level's own coordinates.
vec3 lf_local(vec3 world) { return world - level_offset; }

// A local position where patterns are drawn: absolute across the dive.
vec3 lf_pattern(vec3 local) { return vec3(local.x + level_origin.x, local.y, local.z + level_origin.y); }

// The cutout (as Baldur's Gate 3's): whatever stands between the camera and her plane round her — rock, a tunnel's roof,
// the coral and sponges on it — is cut away in a clean round opening about her, so no wall ever hides her. It opens
// like an iris as something comes to hide her (see_through.w: 0 shut, 1 open, eased here) and closes as gently. Its
// edge is crisp: a thin band wavering with the rock (anchored to the world, never crawling over the screen) dissolves
// in a fine noise, not a screen-door grid, and the cut faces left round it carry a faint glow (lf_see_rim). Only what
// is on the camera's side of her and above her plane is cut: the floor she swims over and what stands behind her
// stay whole.

// How open the opening is (eased), and the lowest height anything is cut at: just over her plane.
float lf_see_open() { float w = see_through.w; return w * w * (3.0 - 2.0 * w); }
// (Just over the seabed: the rock is cut right down, its foot left as no stub round her; the floor itself is never cut.)
float lf_see_slice() { return see_through_y - 1.5; }

// How far (metres) from the opening's edge a point is, on her plane, as the camera sees it (< 0 inside the opening,
// > 0 outside); a long way out for what is not on the camera's side of her.
float lf_see_dist(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0) return 1e3;
	float band = see_through_y;
	vec3 d = world - cam;
	if (d.y > -1e-3) return 1e3;
	// Where the camera's ray to it crosses her plane (what it hides there; for the see floor, below her plane, what
	// stands over it along the ray).
	vec3 hid = cam + d * ((band - cam.y) / d.y);
	vec2 ahead = normalize(d.xz + vec2(1e-5));
	// (Only what stands on the camera's side of her is cut; the see floor under her plane is seen wherever the rock
	// over it along the ray is cut, beyond her too.)
	if (world.y > band && dot(world.xz - see_through.xy, ahead) > 0.4) return 1e3;
	vec2 off = hid.xz - see_through.xy;
	// Its rim wavers a little round it, as rock breaks (a few slow lobes, fixed to the world).
	float a = atan(off.y, off.x);
	float waver = 0.06 * sin(a * 3.0 + see_through.x * 0.7) + 0.04 * sin(a * 7.0 - see_through.y * 0.9) + 0.03 * sin(a * 13.0 + 1.7);
	return length(off) - see_through.z * lf_see_open() * (1.0 + waver);
}

// lf_see_dist, and whether the point may be cut at all (in .y, eased over its lowest height: nothing under the slice is
// cut, so the floor she swims over stays whole). Through the opening the seabed is seen running on under the rock
// (LevelView's see floor: the ground's own skin on a patch under her, drawn only inside the opening).
vec2 lf_see_edge(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0 || world.y < lf_see_slice()) return vec2(1e3, 0.0);
	return vec2(lf_see_dist(world, cam), smoothstep(lf_see_slice(), lf_see_slice() + 0.15, world.y));
}

// The edge's soft band (metres): the cut dissolves over it.
const float LF_SEE_SOFT = 0.3;

// True where something in front of her is cut away (lf_see_edge's opening).
bool lf_see_cut(vec3 world, vec3 cam, vec2 frag) {
	vec2 e = lf_see_edge(world, cam);
	if (e.y <= 0.0) return false;
	float cut = (1.0 - smoothstep(-LF_SEE_SOFT, 0.0, e.x)) * e.y;
	return cut > lf_dither(frag);
}

// The faint glow round the opening (0..1), on what is left at its edge.
float lf_see_rim(vec3 world, vec3 cam) {
	if (see_through.w <= 0.0 || world.y < lf_see_slice()) return 0.0;
	float e = lf_see_dist(world, cam);
	return (1.0 - smoothstep(0.0, 0.45, abs(e + 0.1))) * see_through.w;
}

// The cut faces' glow: a cool light, as if her light caught them.
const vec3 LF_SEE_RIM = vec3(0.35, 0.75, 0.95);

// True where the portal hides this pixel; its edge dissolves over a few metres.
bool lf_portal_cut(vec3 world, vec2 frag) {
	if (portal.w < 0.5) return false;
	float inside = 1.0 - smoothstep(-3.0, 3.0, distance(world.xz, portal.xy) - portal.z);
	float t = lf_dither(frag);
	return portal.w < 1.5 ? inside > t : 1.0 - inside > t;
}
