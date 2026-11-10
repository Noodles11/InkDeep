// The level's ground: the reef's skin (reef_surface.gdshaderinc) on the heightfield. What stands between the camera and
// Clementine is cut open round her (level_frame's lf_see_cut), a faint glow round the opening; the seabed is seen on
// under the rock through it (see_floor: this skin on a patch under her, drawn only inside the opening).
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform bool debug_normals = false;
/// The see floor (LevelView.SeeThrough): a patch of seabed under her, drawn only inside the opening.
instance uniform float see_floor = 0.0;

// The level's cave openings (LevelView.AddDoors, docs/RPG-PROPOSAL.md §2.3): each one's threshold (xy, map metres) and
// the way into it (zw), and the passages' depth, open half-width and arch top. The deeper into one, the darker, until
// its back is black: the dark of the cave beyond.
uniform vec4 doors[8];
uniform int door_count = 0;
uniform vec3 door_size = vec3(4.5, 1.7, 2.8);

varying vec3 world;
varying vec3 world_normal;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}


// An art direction under study (reef_art.gdshaderinc): the reef painted another way.
void art(inout ReefSkin s, vec3 local, float hw, float px) {
	if (reef_art <= 0) return;
	vec3 w = lf_pattern(local);
	s.albedo = art_paint(s.albedo, w, local, s.normal, reef_water_deep, reef_rock, reef_sand);
	s.albedo = mix(s.albedo, s.albedo * 0.4 + vec3(0.85, 0.82, 0.74) * 0.6, art_rim(local, s.normal, hw));
	vec2 ink = art_ink(w, local, s.normal, hw, px);
	s.albedo = mix(s.albedo, art_beyond(reef_water_deep), ink.y);
	s.albedo = mix(s.albedo, art_ink_colour(reef_water_deep), ink.x);
	s.caustic = art_caustic(s.caustic);
}

// How far into an opening's dark a point of the ground lies (0 outside, 1 black): along its passage, under its roof.
float door_dark(vec3 p) {
	float dark = 0.0;
	for (int i = 0; i < door_count; i++) {
		vec2 d = p.xz - doors[i].xy;
		vec2 into = doors[i].zw;
		float inward = dot(d, into);
		float off = abs(d.x * into.y - d.y * into.x);
		float k = smoothstep(-0.4, 2.2, inward)
			* (1.0 - smoothstep(door_size.y + 0.6, door_size.y + 1.4, off))
			* (1.0 - smoothstep(door_size.z + 0.2, door_size.z + 0.8, p.y))
			* (1.0 - smoothstep(door_size.x + 1.0, door_size.x + 1.6, inward));
		dark = max(dark, k);
	}
	return dark;
}

void fragment() {
	// (Not from the sun's eye: the rock's shadows stay as they are.)
	if (lf_portal_cut(world, FRAGCOORD.xy) || !IN_SHADOW_PASS && lf_see_cut(world, CAMERA_POSITION_WORLD, FRAGCOORD.xy)) discard;
	if (see_floor > 0.5 && lf_see_dist(world, CAMERA_POSITION_WORLD) > 0.0) discard;
	vec3 local = lf_local(world);
	float px = length(fwidth(world.xz));
	ReefSkin s = reef_surface(local, normalize(world_normal), TIME, px);
	ReefSkin nat = s;
	art(s, local, fwidth(local.y), px);
	float show = reef_show_skin(s, nat, world);
	float dark = door_count > 0 ? door_dark(local) : 0.0;
	s.albedo *= 1.0 - 0.95 * dark;
	s.emission *= 1.0 - dark;
	s.caustic *= 1.0 - dark;
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	// (And how much the showcase shows it as it is, in .y: lit plainly there, not in the drawn look's steps.)
	// (.z: the see floor, lit as the open floor is, out of the rock's shadow over it.)
	BACKLIGHT = vec3(s.caustic * 1.5, show, see_floor);
	ALBEDO = s.albedo;
	EMISSION = s.emission + LF_SEE_RIM * lf_see_rim(world, CAMERA_POSITION_WORLD) * 0.35;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
	if (debug_normals) { ALBEDO = vec3(0.0); EMISSION = (s.normal * 0.5 + 0.5) * 0.6; }
}

void light() {
	float plain = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float ndl = mix(art_light(plain), plain, BACKLIGHT.y);
	// The sun also brings the caustics; other lights (her glow) light the reef plainly.
	float extra = LIGHT_IS_DIRECTIONAL ? BACKLIGHT.x : 0.0;
	float att = LIGHT_IS_DIRECTIONAL && BACKLIGHT.z > 0.5 ? 1.0 : ATTENUATION;
	DIFFUSE_LIGHT += (ndl + extra) * att * LIGHT_COLOR / PI;
	// Her light (and other near lights) leaves only a faint dull sheen on the matte rock and sand.
	if (!LIGHT_IS_DIRECTIONAL) {
		vec3 hv = normalize(LIGHT + VIEW);
		float spec = pow(clamp(dot(NORMAL, hv), 0.0, 1.0), 10.0) * (1.0 - ROUGHNESS) * ndl;
		SPECULAR_LIGHT += spec * ATTENUATION * LIGHT_COLOR * 0.4;
	}
}
