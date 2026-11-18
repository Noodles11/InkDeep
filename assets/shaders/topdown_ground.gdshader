// The level's ground: the reef's skin (reef_surface.gdshaderinc) on the heightfield. What stands between the camera and
// Clementine is sliced off flat just over her plane (level_frame's lf_see_cut); the cut shows the cap, a flat dotted
// fill laid inside all the rock at that height at all times (its vertices' colour alpha CAP_ALPHA: LevelView.Caps), so
// it is only ever seen where the rock over it is cut away, and outlined.
shader_type spatial;
// (Both sides: where a cut stops part way along a wall, the rock's inside seen through it is drawn solid, as the cap.)
render_mode cull_disabled;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform bool debug_normals = false;

// The level's cave openings (LevelView.AddDoors, docs/DIVE.md §6): each one's threshold (xy, map metres) and
// the way into it (zw), and the passages' depth, open half-width and arch top. The deeper into one, the darker, until
// its back is black: the dark of the cave beyond.
// The cap (the cutout's inside), as the old cave chart was inked: solid near-black (see_cap), a fine-liner along its
// edge, and bundles of long hatching strokes in a band inside it along the edge (see_line; see_band metres deep, the
// odd pebble past it). The cut's edge on the rock outlined in the same ink (see_line_width metres, or a couple of
// pixels). The cap's vertices carry how far into the rock they lie (colour red, in metres over CAP_DEPTH).
uniform vec3 see_cap = vec3(0.025, 0.028, 0.035);
// The coloured ink drawing (InkLook): the cut a cross-section as an engraver draws one: pale, its strokes in dark ink.
uniform bool ink_cap = false;
uniform vec3 see_line = vec3(0.26, 0.27, 0.3);
// The hatching, in world metres: its band's depth into the rock, a bundle's size, the gap between its strokes; and a
// stroke's weight in pixels.
uniform float see_band = 1.3;
uniform float see_cell = 0.75;
uniform float see_gap = 0.2;
uniform float see_stroke_px = 0.6;
uniform float see_ink_px = 1.6;
uniform float see_line_width = 0.18;
const float CAP_DEPTH = 6.0;

uniform vec4 doors[8];
uniform int door_count = 0;
uniform vec3 door_size = vec3(4.5, 1.7, 2.8);

varying vec3 world;
// Where the point stood before the grown wall pushed it (the cutout decides by it, so a bulge never strays across the cut).
varying vec2 base_xz;
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

// The grown wall: as it rises, its face is pushed out and drawn in along its own facing (bulges, undercuts and
// overhangs, a head swelling over a narrower neck), stepping back in ledges every few metres of height. Measured in the
// world's metres, so neighbouring pieces of ground agree; nothing moves at its foot (the rock she meets at her plane).
uniform float grow = 3.2;
uniform float grow_ledge = 1.1;
const float GROW_LEDGE_STEP = 4.5;

float gr_hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float gr_noise(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(gr_hash(i), gr_hash(i + vec3(1, 0, 0)), f.x), mix(gr_hash(i + vec3(0, 1, 0)), gr_hash(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(gr_hash(i + vec3(0, 0, 1)), gr_hash(i + vec3(1, 0, 1)), f.x), mix(gr_hash(i + vec3(0, 1, 1)), gr_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z) * 2.0 - 1.0;
}

// How far (metres) the face at p is pushed out along its facing.
float gr_push(vec3 p) {
	float big = gr_noise(p * vec3(0.075, 0.055, 0.075)) * 0.7 + gr_noise(p * vec3(0.16, 0.12, 0.16) + 7.3) * 0.3;
	float lumps = gr_noise(p * vec3(0.35, 0.3, 0.35) + 3.1);
	// (Ledges: the face steps back a little at each, a band of height wide, where they come round on its own wavering.)
	float h = p.y / GROW_LEDGE_STEP + gr_noise(p * 0.05 + 11.0) * 0.8;
	float ledge = smoothstep(0.0, 0.3, fract(h)) - 0.5;
	return grow * big + 0.6 * lumps + grow_ledge * ledge;
}

void vertex() {
	base_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	is_cap = abs(COLOR.a - 0.5) < 0.1 ? 1.0 : 0.0;
	cap_in = COLOR.r * CAP_DEPTH;
	if (rise > 0.0 && is_cap < 0.5) {
		float t = clamp((VERTEX.y - rise_from) / (rise_to - rise_from), 0.0, 1.0);
		float k = t * t * (3.0 - 2.0 * t) * COLOR.a;
		float stretch = 1.0 + rise * COLOR.a * 6.0 * t * (1.0 - t) / (rise_to - rise_from);
		VERTEX.y += rise * k;
		NORMAL = normalize(vec3(NORMAL.x * stretch, NORMAL.y, NORMAL.z * stretch));
		// Grown: pushed along its facing (flat ground and the wall's foot not at all), its normal bent to match.
		vec2 facing = NORMAL.xz;
		float steep = length(facing);
		if (grow > 0.0 && steep > 0.05 && k > 0.0) {
			vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
			vec2 out_dir = facing / steep;
			// (Grows in over the first metres of its rise, so its foot stays where the rock is.)
			float amount = smoothstep(0.0, 0.12, k) * steep;
			float d = gr_push(wp);
			VERTEX.xz += out_dir * d * amount;
			// The face's slope from how the push changes with height and along it.
			const float e = 0.8;
			float dy = (gr_push(wp + vec3(0.0, e, 0.0)) - gr_push(wp - vec3(0.0, e, 0.0))) / (2.0 * e);
			vec3 side = normalize(vec3(-out_dir.y, 0.0, out_dir.x));
			float ds = (gr_push(wp + side * e) - gr_push(wp - side * e)) / (2.0 * e);
			NORMAL = normalize(NORMAL + (vec3(0.0, -dy, 0.0) - side * ds) * amount * steep);
		}
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

// The cap's ink at a point (w: world metres; d: metres into the rock from its edge; ppm: pixels a metre): bundles of
// long parallel strokes in a band along the edge, each bundle (a cell see_cell metres across) at its own angle, a few
// left bare, each stopping at its own depth; past the band the odd pebble. All of it laid in world metres, fixed to the
// rock as the camera moves; only the strokes' weight is in pixels (see_stroke_px).
float see_hatch(vec2 w, float d, float ppm) {
	float band = see_band * (1.0 + 0.28 * sin(w.x * 0.21 + w.y * 0.13) + 0.18 * sin(w.y * 0.37 - w.x * 0.11));
	if (d > band + 0.8) return 0.0;
	if (d > band + 0.1) {
		float pc = see_cell * 0.7;
		vec2 id = floor(w / pc);
		if (see_hash(id + 5.3) > 0.12) return 0.0;
		vec2 c = (id + 0.3 + 0.4 * vec2(see_hash(id + 1.7), see_hash(id + 8.2))) * pc;
		float r = see_cell * (0.08 + 0.06 * see_hash(id + 2.9));
		return 1.0 - smoothstep(0.35, 0.9, abs(length(w - c) - r) * ppm);
	}
	vec2 g = w / see_cell, gi = floor(g);
	float best = 1e9;
	vec2 seed = vec2(0.0), id = vec2(0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = gi + vec2(float(x), float(y));
		vec2 s = n + 0.15 + 0.7 * vec2(see_hash(n), see_hash(n + 17.1));
		float dd = length(g - s);
		if (dd < best) { best = dd; seed = s; id = n; }
	}
	if (see_hash(id + 9.1) < 0.1) return 0.0;
	float ang = see_hash(id + 3.7) * 3.14159;
	vec2 dir = vec2(cos(ang), sin(ang));
	float u = dot(w - seed * see_cell, dir) / see_gap;
	// (Its distance from the nearest stroke's middle, in pixels: the weight stays the same at any zoom.)
	float off_px = abs(fract(u) - 0.5) * see_gap * ppm;
	float stroke = 1.0 - smoothstep(see_stroke_px * 0.5, see_stroke_px * 0.5 + 0.55, off_px);
	float reach = band * (0.7 + 0.3 * see_hash(id + 1.3));
	return stroke * (1.0 - smoothstep(reach - 0.05, reach + 0.03, d));
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
	vec3 cut_at = vec3(base_xz.x, world.y, base_xz.y);
	if (lf_portal_cut(world, FRAGCOORD.xy) || !IN_SHADOW_PASS && lf_see_cut(cut_at, CAMERA_POSITION_WORLD, FRAGCOORD.xy)) discard;
	// The rock's inside (its far faces: a wall risen past the camera, or seen through a cut) is never drawn: a wall
	// shows only its outer side, and where only its inside would show, the cap inside it shows instead.
	if (is_cap < 0.5 && !FRONT_FACING) discard;
	// The cap: unlit, inked, everywhere inside the rock at the cut's height (not from the sun's eye).
	if (is_cap > 0.5) {
		if (IN_SHADOW_PASS) discard;
		float ppm = 1.0 / max(length(fwidth(world.xz)) * 0.7071, 1e-4);
		float d_px = cap_in * ppm;
		// The fine-liner along its edge, wavering a little; the hatching inside.
		float liner = 1.0 - smoothstep(see_ink_px * (0.75 + 0.5 * see_hash(floor(world.xz * 0.5))) * 0.5, see_ink_px + 0.5, d_px);
		float ink = max(liner, 0.85 * see_hatch(world.xz, cap_in, ppm));
		ALBEDO = vec3(0.0);
		EMISSION = ink_cap ? mix(vec3(0.40, 0.50, 0.60), vec3(0.008, 0.012, 0.025), max(liner, 0.85 * see_hatch(world.xz, cap_in * 0.35, ppm))) : mix(see_cap, see_line, ink);
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
		outline = lf_see_column(cut_at, CAMERA_POSITION_WORLD) && edge >= 0.0 && (edge < line_width || edge / max(fwidth(edge), 1e-4) < 2.0);
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
	// Grown rock (the risen walls): live rock's lumps and pits over the face, finer than its mesh can carry; its
	// hollows darker. Not on the floor, nor low by her plane.
	if (rise > 0.0 && grow > 0.0) {
		vec3 wn = normalize(world_normal);
		float wall = (1.0 - smoothstep(0.55, 0.85, wn.y)) * smoothstep(rise_from + 0.5, rise_from + 6.0, world.y);
		if (wall > 0.01) {
			vec3 q = world * 0.55;
			const float e = 0.35;
			float c0 = gr_noise(q), c1 = gr_noise(q * 2.3 + 5.1) * 0.5;
			vec3 g = vec3(gr_noise(q + vec3(e, 0, 0)) - gr_noise(q - vec3(e, 0, 0)), gr_noise(q + vec3(0, e, 0)) - gr_noise(q - vec3(0, e, 0)),
				gr_noise(q + vec3(0, 0, e)) - gr_noise(q - vec3(0, 0, e))) / (2.0 * e);
			s.normal = normalize(s.normal - (g - wn * dot(g, wn)) * 0.55 * wall);
			float pit = smoothstep(0.25, 0.75, -(c0 + c1));
			s.albedo *= 1.0 - 0.45 * pit * wall;
		}
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
		EMISSION = ink_cap ? vec3(0.008, 0.012, 0.025) : see_line;
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
