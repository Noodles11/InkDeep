// The woven reef's paint (docs/proposals/REEF-REDO-PROPOSAL.md §4, the look prototype): a living illustration. Each coral is
// laid in two or three soft flat tones of its own colour, held back toward stone and the water, broken by a faint
// brush stroke; its edges, turned from the eye, dissolve into the water. Depth reads from colour alone: the deeper a
// piece lies under her plane, the more of the water's colour it takes, in soft steps (2.2 m layers, as the floor's);
// what stands over her plane is a dark soft silhouette, and opens round her so it never hides her.
// The pieces are drawn after the water pass (topdown_post, priority −100), so they keep this paint (no felt-tip line,
// no marker fill) and lay the water's veil, the grade and the vignette on themselves.
// Vertex colour: r = how much light reaches in (ambient occlusion), g = how freely it bends (0 rigid → 1 tip), b = how
// high up the piece (0 foot → 1 top). Instance custom: r = kind (/16), g = its own random, b = unused, a = phase.

#include "res://assets/shaders/level_frame.gdshaderinc"
#include "res://assets/shaders/flow_field.gdshaderinc"
global uniform int reef_art;

global uniform vec3 reef_water_deep;
global uniform vec3 reef_murk;
global uniform vec3 reef_rock;
global uniform vec3 reef_coral;
global uniform vec3 reef_coral2;
global uniform vec3 reef_algae;
global uniform vec3 reef_sponge;
global uniform vec3 reef_butter;
global uniform vec3 reef_sand;
global uniform vec3 reef_sun;
global uniform float reef_fog;
global uniform float reef_saturation;
global uniform float reef_vignette;
global uniform float reef_dark;
global uniform float sea_surface_y;
global uniform vec4 her_glow;
global uniform float her_glow_strength;
global uniform vec3 her_warm;
global uniform vec3 her_light;
global uniform float her_lantern;

varying vec3 wr_world;
varying vec3 wr_normal;
varying vec4 wr_paint;
varying vec2 wr_kind;
varying vec2 wr_uv;

float wr_hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float wr_noise(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(wr_hash(i), wr_hash(i + vec3(1, 0, 0)), f.x), mix(wr_hash(i + vec3(0, 1, 0)), wr_hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(wr_hash(i + vec3(0, 0, 1)), wr_hash(i + vec3(1, 0, 1)), f.x), mix(wr_hash(i + vec3(0, 1, 1)), wr_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

// The vertex stage (world_vertex_coords): soft things sway with the surge and bend in the water she stirs.
void wr_vertex(inout vec3 vertex, vec4 color, vec4 custom, float time) {
	float flex = color.g;
	vec3 local = lf_local(vertex);
	if (flex > 0.0) {
		float ph = custom.a * 6.2831;
		vertex.x += sin(time * 0.9 + ph + local.y * 1.3) * 0.07 * flex;
		vertex.z += sin(time * 0.7 + ph * 1.7 + local.y * 1.1) * 0.05 * flex;
		vec2 b = flow_bend(vertex.xz, min(flex * 2.2, 1.6), max(-local.y, 0.0));
		vertex.xz += b;
		vertex.y -= length(b) * 0.35;
	}
}

// Each shader's vertex() hands its varyings over with this (varyings are set in vertex() itself).
#define WR_VARYINGS wr_world = VERTEX; wr_normal = NORMAL; wr_paint = COLOR; wr_kind = INSTANCE_CUSTOM.rg; wr_uv = UV;

// The piece's own colour, by its kind, varied a little piece by piece.
vec3 wr_base(float kind, float rnd, vec3 p) {
	int k = int(kind * 16.0 + 0.5);
	vec3 c;
	float patches = wr_noise(p * 0.45) * 0.6 + wr_noise(p * 1.3) * 0.4;
	if (k == 0 || k == 4 || k == 5) {
		// Old reef rock, crusted with coral and algae in patches, pocked with polyp cups.
		c = mix(reef_rock * 0.55, reef_coral2 * 0.6, 0.25 + 0.2 * rnd);
		c = mix(c, reef_algae * 0.7, smoothstep(0.5, 0.8, patches) * 0.55);
		c = mix(c, reef_coral * 0.8, smoothstep(0.68, 0.88, wr_noise(p * 0.8 + 11.0)) * 0.45);
		float cups = smoothstep(0.72, 0.8, wr_noise(p * 7.0));
		c *= 1.0 - 0.3 * cups;
	} else if (k == 1) c = mix(reef_coral, reef_butter, 0.3 * rnd);
	else if (k == 2) {
		// A table's plate: rings of growth round its middle.
		c = mix(reef_butter * 0.75, reef_coral2 * 0.7, 0.35 + 0.4 * rnd);
		c *= 0.9 + 0.12 * sin(length(p.xz - floor(p.xz / 9.0) * 9.0) * 3.0);
	}
	else if (k == 3) c = mix(reef_rock, reef_butter, 0.2) * 0.85;
	else if (k == 6) {
		// A brain coral: its valleys meander.
		c = mix(reef_coral2 * 0.55, reef_butter * 0.55, 0.3 + 0.5 * rnd);
		float m = sin(p.x * 2.6 + 4.0 * wr_noise(p * 0.9)) + sin(p.z * 2.3 - 3.0 * wr_noise(p * 1.1 + 4.0));
		c *= 1.0 - 0.18 * (1.0 - smoothstep(0.0, 0.45, abs(m)));
	}
	else if (k == 7) c = mix(reef_coral2, reef_coral, rnd) * 0.8;
	else if (k == 8) c = mix(reef_sponge * 0.55, reef_coral * 0.6, 0.3 + 0.4 * rnd);
	else if (k == 10) {
		// Reef fish: silvery blue to the depth's coral, a darker back.
		c = mix(vec3(0.42, 0.55, 0.62), mix(reef_coral, reef_butter, 0.5), rnd * 0.7);
		return c;
	}
	else c = mix(reef_algae * 0.6, reef_coral * 0.65, 0.35 * rnd);
	// Held back: toward its own grey and a shade down, so the creatures carry the picture.
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	return mix(vec3(l), c, 0.75) * 0.8;
}

// The paint, its veil of water, its light; returns the colour and whether it is cut away (alpha 0 → discard).
vec4 wr_fragment(vec3 cam, vec2 frag, vec2 screen_uv, vec2 viewport, float time, bool front) {
	vec3 world = wr_world;
	if (lf_portal_cut(world, frag)) return vec4(0.0);
	vec3 local = lf_local(world);
	float h = local.y;
	vec3 view = normalize(cam - world);
	vec3 n = normalize(wr_normal);
	if (!front) n = -n;

	// Over her plane, between the camera and her: opened round her (the dither's edge), so it never hides her.
	if (h > 0.6 && her_glow_strength >= 0.0) {
		vec3 d = world - cam;
		if (d.y < -1e-3) {
			vec3 hid = cam + d * ((her_glow.y - 0.3 - cam.y) / d.y);
			float r = distance(hid.xz, her_glow.xz);
			float cut = (1.0 - smoothstep(2.6, 4.8, r)) * smoothstep(0.6, 1.4, h);
			if (cut > lf_dither(frag)) return vec4(0.0);
		}
	}

	vec3 pat = lf_pattern(local);
	vec3 base = wr_base(wr_kind.x, wr_kind.y, pat);
	// The light in a few soft flat tones (wrapped round, so the shade is never black), lit from above.
	vec3 sun = normalize(vec3(0.55, 0.75, 0.35));
	float ndl = dot(n, sun) * 0.5 + 0.5;
	float tone = 0.5 * smoothstep(0.36, 0.5, ndl) + 0.5 * smoothstep(0.66, 0.8, ndl);
	// (The watercolour study, look 7: one flat wash, no modelling, no stroke.)
	bool wash = reef_art == 7;
	if (wash) tone = 0.6;
	vec3 shade = mix(base * 0.5, reef_water_deep * 0.55, 0.35);
	vec3 col = mix(shade, base, 0.25 + 0.75 * tone);
	// A rim of light along its tops.
	if (!wash) col += base * smoothstep(0.82, 0.97, n.y) * 0.12;
	// Light reaching in: deep among the branches, at the foot, darker.
	col *= mix(0.5, 1.0, wr_paint.r);
	// A faint brush stroke, laid along the piece's height.
	float stroke = wr_noise(vec3(pat.x * 2.2, pat.y * 0.5, pat.z * 2.2)) * 0.65 + wr_noise(pat * 5.0) * 0.35;
	if (!wash) col *= 0.93 + 0.14 * stroke;

	// Her light on it: a warm lift of its own colour, near her.
	float near = distance(world, her_glow.xyz);
	float lit = exp(-pow(near / max(her_glow.w * 1.1, 0.5), 2.0)) * clamp(her_glow_strength * 3.0, 0.0, 1.0);
	col = col * (1.0 + her_warm * lit * 0.8 * her_lantern) + her_warm * lit * 0.03;

	// The water: under her plane, flat soft layers of it (2.2 m), each hazier; the reds go first.
	float below = max(-h - 0.6, 0.0);
	float x = below / 2.2;
	float seen = (floor(x) + smoothstep(0.75, 1.0, fract(x))) * 2.2;
	float column_in = clamp((sea_surface_y - world.y) / 20.0, 0.0, 1.5);
	col *= mix(vec3(1.0), vec3(0.64, 0.9, 1.05), column_in * (1.0 - 0.6 * lit));
	col = mix(col, reef_water_deep * 0.7, 0.13 * column_in);
	vec3 water = mix(reef_water_deep, reef_murk, smoothstep(2.0, 16.0, seen));
	float veil = mix(0.18, 0.5, reef_fog) * 0.75;
	float fog = smoothstep(0.0, 1.8, seen) * (veil + (0.88 - veil) * (1.0 - exp(-seen * 0.07 * (0.6 + reef_fog))));
	col = mix(col, water, fog * (1.0 - 0.45 * lit));
	// Edges turned from the eye dissolve into the water round them.
	float rim = pow(1.0 - clamp(abs(dot(n, view)), 0.0, 1.0), 2.5);
	col = mix(col, water, rim * (0.25 + 0.35 * fog));
	// Over her plane: a soft dark silhouette, the murk's own colour, the higher the more, and a little see-through.
	float above = max(h - 1.0, 0.0);
	float over = smoothstep(0.0, 2.5, above);
	col = mix(col, mix(col * 0.6, reef_murk * 0.4, 0.7), over * 0.8);

	// The grade and the vignette (as the water pass lays them on everything else).
	float luma = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(luma), col, reef_saturation);
	float wl = max(dot(reef_water_deep, vec3(0.299, 0.587, 0.114)), 0.01);
	col = mix(col, reef_water_deep / wl * luma, (1.0 - smoothstep(0.0, 0.3, luma)) * 0.45 * (1.0 - lit));
	vec2 dv = (screen_uv - 0.5) * vec2(viewport.x / viewport.y, 1.0);
	float vig = smoothstep(0.32, 1.05, length(dv) * 1.3);
	col = mix(col, reef_murk * 0.25, vig * reef_vignette * 1.3);
	// The sheet's paper, as the drawn look's.
	float tooth = wr_hash(vec3(floor(frag * 0.5), 1.0));
	col *= 0.965 + 0.04 * tooth;
	return vec4(max(col, vec3(0.0)), 1.0 - 0.3 * over);
}
