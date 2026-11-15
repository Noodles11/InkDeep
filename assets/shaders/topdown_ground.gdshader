// The level's ground: the reef's skin (reef_surface.gdshaderinc) on the heightfield. What stands between the camera and
// Clementine is sliced off flat just over her plane (level_frame's lf_see_cut); the cut is filled solid (see_floor: a
// plane laid at the cut's height, drawn only where rock stood over it) and outlined.
shader_type spatial;
// (Both sides: where the cutout opens the rock, its far faces seen from inside are its solid inside.)
render_mode cull_disabled;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform bool debug_normals = false;
/// The see floor (LevelView.SeeThrough): the cut's fill, a plane at the cut's height round her.
instance uniform float see_floor = 0.0;

// The level's cave openings (LevelView.AddDoors, docs/DIVE.md §6): each one's threshold (xy, map metres) and
// the way into it (zw), and the passages' depth, open half-width and arch top. The deeper into one, the darker, until
// its back is black: the dark of the cave beyond.
// The cutout's inside (level_frame's lf_see_cut): the rock it cuts through shows solid, a flat grey faintly dotted
// (see_dot, every see_dot_step pixels), and its edge outlined in the dots' colour (see_line_width metres, or a couple
// of pixels): quiet, plainly a cut. The see floor caps only what lies under rock (level_frame's see_heights).
uniform vec3 see_cap = vec3(0.12, 0.13, 0.15);
uniform vec3 see_dot = vec3(0.18, 0.19, 0.22);
uniform float see_dot_step = 7.0;
uniform float see_line_width = 0.18;

uniform vec4 doors[8];
uniform int door_count = 0;
uniform vec3 door_size = vec3(4.5, 1.7, 2.8);

varying vec3 world;
varying vec3 world_normal;

// The cube reef's tall walls (ReefStreamView): rock above rise_from metres rises by up to rise metres (fully by rise_to),
// so every wall stands on up out of view as one tall structure; its normals tilted to match. A vertex colour's alpha
// below 1 rises less (an arch's slab, alpha 0, not at all).
uniform float rise = 0.0;
uniform float rise_from = 1.4;
uniform float rise_to = 3.2;

void vertex() {
	if (rise > 0.0) {
		float t = clamp((VERTEX.y - rise_from) / (rise_to - rise_from), 0.0, 1.0);
		float k = t * t * (3.0 - 2.0 * t) * COLOR.a;
		float stretch = 1.0 + rise * COLOR.a * 6.0 * t * (1.0 - t) / (rise_to - rise_from);
		VERTEX.y += rise * k;
		NORMAL = normalize(vec3(NORMAL.x * stretch, NORMAL.y, NORMAL.z * stretch));
	}
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
	// The cut's fill (the see floor, laid at the cut's height): solid where rock was cut off over it, dotted; outlined
	// round its rim, where the rock's face meets the cut. Elsewhere nothing.
	float line_width = max(see_line_width, 2.0 * length(fwidth(world.xz)));
	bool outline = false;
	bool cap = !FRONT_FACING;
	if (see_floor > 0.5) {
		if (see_through.w <= 0.0 || lf_see_front(world, CAMERA_POSITION_WORLD) < 0.0 || see_heights_box.z <= 0.0 || !lf_see_takes(world)) discard;
		vec2 huv = (world.xz - see_heights_box.xy) / see_heights_box.z;
		float under = huv.x < 0.0 || huv.y < 0.0 || huv.x > 1.0 || huv.y > 1.0 ? -100.0 : texture(see_heights, huv).r;
		if (under <= world.y) discard;
		cap = true;
		// (Its back edge, where the cut stops short of her; round its rim the rock's own line shows.)
		outline = lf_see_front(world, CAMERA_POSITION_WORLD) < line_width;
	}
	// Its edge on what is left of the rock: a sharp line along the cut.
	else if (!IN_SHADOW_PASS && see_through.w > 0.0) {
		// (A couple of pixels wide however steep the rock; measured on the cut's own distance, not where rock kept
		// whole meets it.)
		float edge = max(lf_see_height() - world.y, -lf_see_front(world, CAMERA_POSITION_WORLD));
		outline = lf_see_takes(world) && edge >= 0.0 && (edge < line_width || edge / max(fwidth(edge), 1e-4) < 2.0);
	}
	vec3 local = lf_local(world);
	float px = length(fwidth(world.xz));
	ReefSkin s = reef_surface(local, normalize(world_normal), TIME, px);
	ReefSkin nat = s;
	if (reef_art == 7) s.normal = normalize(world_normal);
	art(s, local, fwidth(local.y), px);
	// Watercolour (look 7): the simple wash is the reef as her light shows it (nat): no fine relief, no glow of its
	// own. All that lies under the walls' line is the water's dark wash. Out of her light the reef is held near the
	// water's tone (s), so reef and water part only faintly until she lights them.
	if (reef_art == 7) {
		vec3 w7 = lf_pattern(local);
		float wk = art_water(local);
		vec3 wash = art_wash(w7, reef_water_deep, reef_murk);
		s.rough = 0.95;
		nat = s;
		nat.albedo = mix(s.albedo, wash * 0.3, wk);
		s.albedo = mix(mix(wash * 1.5, s.albedo, 0.16) * 0.4, wash * 0.3, wk);
		s.emission = mix(mix(wash * 1.5, nat.albedo, 0.16) * 0.55, wash * 0.8, wk);
		nat.emission = s.emission;
	}
	float show = reef_show_skin(s, nat, world, CAMERA_POSITION_WORLD);
	float dark = door_count > 0 ? door_dark(local) : 0.0;
	s.albedo *= 1.0 - 0.95 * dark;
	s.emission *= 1.0 - dark;
	s.caustic *= 1.0 - dark;
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	// (And how much the showcase shows it as it is, in .y: lit plainly there, not in the drawn look's steps.)
	// (.z: the see floor, lit as the open floor is, out of the rock's shadow over it.)
	BACKLIGHT = vec3(s.caustic * 1.5, reef_art == 7 ? 0.0 : show, see_floor);
	ALBEDO = s.albedo;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
	if (cap || outline) {
		// Unlit, flat: the cut through solid reef, dotted; its edge a line.
		vec2 cell = mod(FRAGCOORD.xy, see_dot_step) - see_dot_step * 0.5;
		float dotted = 1.0 - smoothstep(see_dot_step * 0.12, see_dot_step * 0.12 + 0.8, length(cell));
		ALBEDO = vec3(0.0);
		EMISSION = outline ? see_dot : mix(see_cap, see_dot, dotted);
		BACKLIGHT = vec3(0.0);
		SPECULAR = 0.0;
		ROUGHNESS = 1.0;
	}
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
