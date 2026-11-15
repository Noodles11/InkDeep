// The level's ground: the reef's skin (reef_surface.gdshaderinc) on the heightfield. What stands between the camera and
// Clementine is sliced off flat just over her plane (level_frame's lf_see_cut); the cut shows the cap, a flat dotted
// fill laid inside all the rock at that height at all times (its vertices' colour alpha CAP_ALPHA: LevelView.Caps), so
// it is only ever seen where the rock over it is cut away, and outlined.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform bool debug_normals = false;

// The level's cave openings (LevelView.AddDoors, docs/DIVE.md §6): each one's threshold (xy, map metres) and
// the way into it (zw), and the passages' depth, open half-width and arch top. The deeper into one, the darker, until
// its back is black: the dark of the cave beyond.
// The cap (the cutout's inside), as the old cave chart was inked: solid near-black (see_cap), a fine-liner along its
// edge, and bundles of short hatching strokes in a band inside it along the edge (see_line; see_band_px pixels deep, the
// odd pebble past it). The cut's edge on the rock outlined in the same ink (see_line_width metres, or a couple of
// pixels). The cap's vertices carry how far into the rock they lie (colour red, in metres over CAP_DEPTH).
uniform vec3 see_cap = vec3(0.025, 0.028, 0.035);
uniform vec3 see_line = vec3(0.26, 0.27, 0.3);
uniform float see_band_px = 18.0;
uniform float see_ink_px = 1.6;
uniform float see_line_width = 0.18;
const float CAP_DEPTH = 6.0;

uniform vec4 doors[8];
uniform int door_count = 0;
uniform vec3 door_size = vec3(4.5, 1.7, 2.8);

varying vec3 world;
varying vec3 world_normal;
// The cap's vertices are marked by their colour's alpha (LevelView.CapColour); how far into the rock (metres).
varying float is_cap;
varying float cap_in;

// The cube reef's tall walls (ReefStreamView): rock above rise_from metres rises by up to rise metres (fully by rise_to),
// so every wall stands on up out of view as one tall structure; its normals tilted to match. A vertex colour's alpha
// below 1 rises less (an arch's slab, alpha 0, not at all).
uniform float rise = 0.0;
uniform float rise_from = 1.4;
uniform float rise_to = 3.2;

void vertex() {
	is_cap = abs(COLOR.a - 0.5) < 0.1 ? 1.0 : 0.0;
	cap_in = COLOR.r * CAP_DEPTH;
	if (rise > 0.0 && is_cap < 0.5) {
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

float see_hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

// The cap's ink at a point (w: world metres; d_px: pixels into the rock from its edge; ppm: pixels a metre): bundles of
// short parallel strokes in a band along the edge, each bundle (a cell ~10 px across) at its own angle, a few left
// bare, each stopping at its own depth; past the band the odd pebble.
float see_hatch(vec2 w, float d_px, float ppm) {
	float mpp = 1.0 / ppm;
	float band = see_band_px * (1.0 + 0.28 * sin(w.x * 0.21 + w.y * 0.13) + 0.18 * sin(w.y * 0.37 - w.x * 0.11));
	if (d_px > band + 12.0) return 0.0;
	if (d_px > band + 1.5) {
		vec2 g = w / (7.0 * mpp), id = floor(g);
		if (see_hash(id + 5.3) > 0.12) return 0.0;
		vec2 c = (id + 0.3 + 0.4 * vec2(see_hash(id + 1.7), see_hash(id + 8.2))) * 7.0 * mpp;
		float r = 1.1 + 0.9 * see_hash(id + 2.9);
		return 1.0 - smoothstep(0.35, 0.9, abs(length(w - c) * ppm - r));
	}
	float cell = 10.0 * mpp;
	vec2 g = w / cell, gi = floor(g);
	float best = 1e9;
	vec2 seed = vec2(0.0), id = vec2(0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = gi + vec2(float(x), float(y));
		vec2 s = n + 0.15 + 0.7 * vec2(see_hash(n), see_hash(n + 17.1));
		float d = length(g - s);
		if (d < best) { best = d; seed = s; id = n; }
	}
	if (see_hash(id + 9.1) < 0.1) return 0.0;
	float ang = see_hash(id + 3.7) * 3.14159;
	vec2 dir = vec2(cos(ang), sin(ang));
	float u = dot(w - seed * cell, dir) / (2.9 * mpp);
	float stroke = 1.0 - smoothstep(0.3, 0.85, abs(fract(u) - 0.5) * 2.9);
	float reach = band * (0.7 + 0.3 * see_hash(id + 1.3));
	return stroke * (1.0 - smoothstep(reach - 1.0, reach + 0.5, d_px));
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
	// The cap: unlit, inked, everywhere inside the rock at the cut's height (not from the sun's eye).
	if (is_cap > 0.5) {
		if (IN_SHADOW_PASS) discard;
		float ppm = 1.0 / max(length(fwidth(world.xz)) * 0.7071, 1e-4);
		float d_px = cap_in * ppm;
		// The fine-liner along its edge, wavering a little; the hatching inside.
		float liner = 1.0 - smoothstep(see_ink_px * (0.75 + 0.5 * see_hash(floor(world.xz * 0.5))) * 0.5, see_ink_px + 0.5, d_px);
		float ink = max(liner, 0.85 * see_hatch(world.xz, d_px, ppm));
		ALBEDO = vec3(0.0);
		EMISSION = mix(see_cap, see_line, ink);
		BACKLIGHT = vec3(0.0);
		SPECULAR = 0.0;
		ROUGHNESS = 1.0;
		NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	} else {
	// The cut's edge on what is left of the rock: a sharp line along it.
	float line_width = max(see_line_width, 2.0 * length(fwidth(world.xz)));
	bool outline = false;
	if (!IN_SHADOW_PASS && see_through.w > 0.0) {
		// (A couple of pixels wide however steep the rock; measured on the cut's own distance, not where rock kept
		// whole meets it.)
		float edge = lf_see_height() - world.y;
		outline = lf_see_column(world, CAMERA_POSITION_WORLD) && edge >= 0.0 && (edge < line_width || edge / max(fwidth(edge), 1e-4) < 2.0);
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
	BACKLIGHT = vec3(s.caustic * 1.5, reef_art == 7 ? 0.0 : show, 0.0);
	ALBEDO = s.albedo;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
	if (outline) {
		// Unlit, flat: the cut's edge a line in the cap's ink.
		ALBEDO = vec3(0.0);
		EMISSION = see_line;
		BACKLIGHT = vec3(0.0);
		SPECULAR = 0.0;
		ROUGHNESS = 1.0;
	}
	if (debug_normals) { ALBEDO = vec3(0.0); EMISSION = (s.normal * 0.5 + 0.5) * 0.6; }
	}
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
