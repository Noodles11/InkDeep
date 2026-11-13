// The water (THEME-BIBLE Â§6.1 depth focus, Â§6.2 light, Â§6.9 post stack), drawn over the reef as one full-screen pass.
// Everything is judged by its height against the swim level (0), where Clementine floats; that layer stays sharp.
//   Below it the world blurs, more the deeper it lies; above it rock blurs into soft foreground silhouettes. The reef's
//   colour is the same at every height, over the layer and under it, seen through one even water (turquoise in the
//   Shallows, murk further down).
//   God rays rake across the view: shafts of sunlight marched along each view ray through the water column, parallel
//   to the sun, ending in the caustics they make below, cut off where the reef shades the water (SunMap). Never into
//   the lens: the camera looks down.
//   Her light glows in the water along every view ray, shadowed by the reef (light_field, DESIGN-TOPDOWN §5.3); her bell
//   and her bubbles throw caustics on the floor.
//   A faint refraction wobble, a grade (saturation, cool shadows, sunlit highlights) and a vignette tinted by the
//   water, all deepening with Menace.
// A full-screen quad in the transparent pass, drawn first, so Clementine and the other glowing things land on top sharp.
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"
#include "res://assets/shaders/light_field.gdshaderinc"
#include "res://assets/shaders/reef_show.gdshaderinc"

global uniform vec3 reef_water_deep;
global uniform vec3 reef_murk;
// A cave's dark (ReefLook.Dark): never black, the outlines of what is there drawn faintly.
global uniform float reef_dark;
global uniform vec3 reef_sun;
global uniform float reef_rays;
global uniform float reef_caustics;
global uniform float reef_menace;
global uniform float reef_saturation;
global uniform float reef_vignette;
global uniform float reef_fog;
/// Clementine's bell in world space and her glow pool's radius; how strong her glow on the floor is (BellView).
global uniform vec4 her_glow;
global uniform float her_glow_strength;
/// The depth's lantern (ReefLook.Lantern): how much her light carries against the dark.
global uniform float her_lantern;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float swim_level = 0.0;
/// Heights within this band of the swim level stay in focus (the bell floats ~0.7 m above it).
uniform float focus_above = 1.6;
uniform float focus_below = 0.6;
uniform float below_blur = 5.2;
uniform float above_blur = 3.4;
uniform float deep_fog = 0.06;
/// One reef at every height (the game's own look): all of it is seen through this many metres of water, its colour the
/// same over her plane and under it, and only its blur tells how deep it lies. What lies past far_water under her plane
/// is no reef of this level (the level below, down the shaft): it sinks into the water as far things do.
uniform float reef_water = 3.0;
uniform float far_water = 28.0;
// Debug (F1 "Floor fog", --dbg-nofog): off, the floor below her plane is seen clear — no murk, no depth blur.
uniform bool floor_fog = true;
// Prism Pearl's split (CameraRig.Aberrate): the picture's red and blue pulled apart from its middle for a moment, 0..1.
uniform float aberration = 0.0;
// An art direction under study (reef_art.gdshaderinc, docs/ART-LOOKS.md): painted, depth is told by flat layers of
// water, not by a lens's blur (look 4: by felt-tip stippling, the water a light veil; 0 and 5: the game's own water).
global uniform int reef_art;
// The showcase reef (reef_show.gdshaderinc, docs/VISUAL-SHOWCASE.md): its colours kept through the water, her light
// whiter (its reef_showcase global comes with the include).
// The game's look (CameraRig.SetShowcase): a thick fog under her plane (below_fog: how much of it, thickening over
// below_fog_depth metres down), and the reef drained of its colour and dimmed outside her light (unlit_saturation,
// unlit_dim: what is left of them there), so she is the one who brings the colour, and her light reads.
// Watercolour (look 7): how soft all that lies under her plane runs (mip levels), and how dark its wash of water is
// (a share of the depth's water colour).
uniform float paper_blur = 3.0;
uniform float paper_dark = 0.62;
uniform float below_fog = 0.0;
uniform float below_fog_depth = 5.0;
uniform float unlit_saturation = 1.0;
uniform float unlit_dim = 1.0;
// Readability (docs/GAME.md §11): outside her light the reef's values are drawn together toward a quiet mid-dark
// (unlit_flatten: how far), the brightest of the reef is rolled off so nothing of it outshines her (highlight_knee: the
// brightness it starts at, 0 off).
uniform float unlit_flatten = 0.0;
uniform float highlight_knee = 0.0;
// Her seat: the reef right under her held darker (her_seat: by how much at its middle, over about a metre), so her
// bell stands off her own pool of light instead of melting into it.
uniform float her_seat = 0.0;
// The mountains' tall tops (DESIGN-TOPDOWN §4.1): above peak_from metres over her plane, rock softens further out of
// focus, rising to full over peak_span — so the range reads as distant and calm, and the floor she plays on stays the
// sharpest thing on screen.
uniform float peak_from = 4.0;
uniform float peak_span = 6.0;
uniform float peak_blur = 2.2;
/// Where the camera follows her (the swim layer the bubbles' halos lie on).
uniform vec3 player_pos = vec3(0.0);
/// The bubbles' halos' strength (her own glow in the water is light_field's in-scatter).
uniform float halo_strength = 0.16;
/// Light bubbles (docs/archive/LIGHT-BUBBLES.md): up to 16 small halos on the swim layer, from her bubbles and the blooms they
/// leave where they pop: x, z (world), radius (metres), strength (a share of her halo's, ≤ 0.4).
uniform vec4 bubble_lights[16];
// Pearls' glows (xz, radius, strength): a clam room's filling the room and spilling from its mouth (ClamRoomView), and
// each pearl lying loose or on a stand its own, laid over the water's veil so it reads through the depth; round each
// the reef keeps its colour, as in her light.
uniform vec4 clam_glows[12];
uniform int clam_count = 0;
// Each glow in its pearl's colour.
uniform vec3 clam_colors[12];
uniform int bubble_count = 0;
// Fish far under her plane (DeepShoals): x, z (world), how far under it, length; their heading (xy), tail's beat, softness.
uniform vec4 deep_fish[16];
uniform vec4 deep_fish_ways[16];
uniform int deep_fish_count = 0;
/// How much a fish's shadow darkens what lies under it.
uniform float deep_fish_shade = 0.65;
/// Swells in the water where she set off, turned or dashed (WaterSwells): x, z (world), age 0 → 1, strength; and the
/// way she was going (x, z; none for a ring) and its reach (a share of 2.6 m; 0 for 1). A faint spreading ring of
/// refraction at her plane, strongest behind her.
uniform vec4 swells[16];
uniform vec4 swell_dirs[16];
uniform int swell_count = 0;
// The currents (CurrentView): over her plane, how far along its course each point lies (m), how far off to its side (m),
// how strongly it shows and which current it is; and each current's roll along its course (m) and strength.
uniform sampler2D current_map : filter_linear, repeat_disable;
uniform sampler2D current_ids : filter_nearest, repeat_disable;
uniform vec2 current_flows[6];
uniform int current_count = 0;
// The leaks (AdventureView): where each is on her plane (x, z) and how open (0..1).
uniform vec3 leaks[8];
uniform int leak_count = 0;
// A way down (x, z, radius of its opening, how much): below the rim there is no seabed, only water.
uniform vec4 hole = vec4(0.0);
// Blasts' marks (CameraRig.AddBlur): where a burst of her light went off, a small round smear of the picture on her
// plane — a zoom blur drawn in toward its middle — that fades: world x, z, age (0 → 1), radius (m).
uniform vec4 blurs[4];
uniform int blur_count = 0;
// Her light and the warm tint of her glow (HerColor.cs, data/clementine_color.cfg).
global uniform vec3 her_light;
global uniform vec3 her_warm;
#define bubble_color her_light
/// A big-merge kill: a white lift at the screen's edges only, 1 → 0 over 60 ms.
uniform float edge_flash = 0.0;
/// The faint refraction wobble (0 with reduced motion).
/// The absolute position (xz) of the world's origin: the god rays never jump after a dive.
uniform vec2 world_origin = vec2(0.0);
uniform float wobble = 1.0;
/// Verification (--dbg-lightview): 1 shows where the sun reaches (yellow) and her light reaches (orange) on what is drawn.
uniform int debug_view = 0;
// The camera's tilt (CameraRig): how far south of a point what lies a metre under it shows (0.577 tilted 60°, 0 from
// straight overhead), and how much a level disc is squashed up the screen (0.866; 1 overhead).
uniform float look_slant = 0.577;
uniform float look_squash = 0.866;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

// A soft blur: rings of taps spread wide over a moderately coarse mip (fine mips smoothed by distance, never the
// coarsest ones, whose big texels show as blocky steps). Fixed tap pattern, so nothing crawls over the screen.
vec3 blurred(vec2 uv, float amount) {
	if (amount < 0.05) return textureLod(screen_tex, uv, 0.0).rgb;
	vec2 px = 1.0 / vec2(textureSize(screen_tex, 0));
	float lod = min(amount, 3.0);
	float r = exp2(amount) * 0.7;
	vec3 sum = textureLod(screen_tex, uv, lod).rgb;
	float w = 1.0;
	for (int ring = 1; ring <= 3; ring++) {
		float rr = r * float(ring) / 3.0;
		float k = 1.0 - float(ring) * 0.22;
		for (int i = 0; i < 8; i++) {
			float a = float(i) * 0.785398 + float(ring) * 0.39;
			sum += textureLod(screen_tex, uv + vec2(cos(a), sin(a)) * rr * px, lod).rgb * k;
			w += k;
		}
	}
	return sum / w;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

// A fish's vague shadow at this point of its own depth, 0..1: one soft shape for them all, a long body bending as it swims
// and a fanned tail beating behind it, in units of its length (nose +0.5).
float fish_shadow(vec2 p, vec4 fish, vec4 way) {
	vec2 o = (p - fish.xy) / fish.w;
	float u = dot(o, way.xy), v = dot(o, vec2(-way.y, way.x));
	if (abs(u) > 1.3 || abs(v) > 0.9) return 0.0;
	float soft = way.w * way.w;
	float aft = clamp(0.5 - u, 0.0, 1.3);
	v += sin(aft * 5.0 - way.z) * 0.07 * aft * aft;
	float body = exp(-(u * u / 0.11 + v * v / 0.012) / soft);
	float t = u + 0.5;
	float fan = 0.05 + 0.4 * max(-t, 0.0);
	float tail = exp(-(v * v) / (fan * fan * soft)) * smoothstep(-0.44, -0.16, t) * (1.0 - smoothstep(-0.06, 0.08, t));
	return max(body, tail * 0.8) / sqrt(way.w);
}

// How far into the view this pixel's surface lies (metres).
float view_depth(vec2 at, mat4 inv_projection) {
	vec4 v = inv_projection * vec4(at * 2.0 - 1.0, texture(depth_tex, at).r, 1.0);
	return -v.z / v.w;
}

// Felt tip and highlighter (look 6, docs/ART-LOOKS.md): the picture as a minimal illustration. What is drawn before
// this pass (the reef, the creatures, the bosses, the kelp) is filled in a few flat marker tones and inked with a felt
// tip's wavering line wherever its depth breaks (a silhouette against what lies behind it); her glow and her bubbles'
// light are laid like a highlighter, vivid and flat-edged; the whole sheet has a paper's grain. What is drawn after it
// (Clementine, her light's shots, the particles, the shimmer of hot water) keeps its own look.

// The marker's fill: the light stepped into a few flat tones (in perceived brightness, the step between them soft) as
// far as steps says (the creatures at her plane; the reef's walls and floor keep their even shading, or its gradients
// would step into stripes), its colour lifted by sat (the creatures vivid, the reef round them held back).
vec3 art_marker_fill(vec3 c, float sat, float steps) {
	float l = max(dot(c, vec3(0.299, 0.587, 0.114)), 1e-4);
	float x = sqrt(l) * 4.0;
	float q = (floor(x) + smoothstep(0.65, 1.0, fract(x))) / 4.0;
	q = mix(l, max(q * q, l * 0.55), steps);
	vec3 o = c * (q / l);
	float lo = dot(o, vec3(0.299, 0.587, 0.114));
	return max(mix(vec3(lo), o, sat), vec3(0.0));
}

// The height over her plane (the swim level) of what is drawn at this point of the screen.
float art_height(vec2 at, mat4 inv_projection, mat4 inv_view) {
	vec4 v = inv_projection * vec4(at * 2.0 - 1.0, texture(depth_tex, at).r, 1.0);
	return (inv_view * vec4(v.xyz / v.w, 1.0)).y - swim_level;
}

// The felt tip's line: how much ink lies here, where the depth breaks between this pixel and its neighbours (a
// silhouette): a step of a third of a metre or more (the camera hangs some 30 m off, so a slope's own gentle change
// never draws one). Only what swims or lies at her plane is inked: a break whose nearer side stands well above or below
// it (a wall's lip over the floor) draws nothing, so the reef has no line. The line runs along both sides of the
// break, round the whole shape, its width wavering as a hand-held tip's does.
float art_outline(vec2 uv, vec2 frag, mat4 inv_projection, mat4 inv_view, vec2 viewport) {
	float wob = value_noise(frag * 0.045) * 0.7 + value_noise(frag * 0.011 + 5.0) * 0.3;
	vec2 px = (0.55 + 0.7 * wob) / viewport;
	float zc = view_depth(uv, inv_projection);
	float hc = art_height(uv, inv_projection, inv_view);
	float step_m = 0.0;
	for (int k = 0; k < 8; k++) {
		float a = float(k) * 0.785398;
		vec2 at = uv + vec2(cos(a), sin(a)) * px;
		float zk = view_depth(at, inv_projection);
		float near_h = zk < zc ? art_height(at, inv_projection, inv_view) : hc;
		if (near_h > 1.5 || near_h < -1.3) continue;
		step_m = max(step_m, abs(zk - zc));
	}
	return smoothstep(0.2, 0.45, step_m) * (0.82 + 0.18 * value_noise(frag * 0.3));
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float depth = texture(depth_tex, uv).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	float h = world.y - swim_level;
	// Nothing drawn (reverse-Z far plane): the open deep.
	bool open_deep = depth <= 1e-6;
	float below = open_deep ? 40.0 : max(-h - focus_below, 0.0);
	if (!floor_fog && !open_deep) below = 0.0;
	float above = open_deep ? 0.0 : max(h - focus_above, 0.0);
	// Soften the blur's edge where high rock meets lower ground: take the highest of a few nearby depths, so the
	// boundary is a smooth margin instead of the mesh's stair-stepped silhouette.
	{
		vec2 px = 3.0 / VIEWPORT_SIZE;
		for (int k = 0; k < 4; k++) {
			vec2 o = vec2(k == 0 ? px.x : k == 1 ? -px.x : 0.0, k == 2 ? px.y : k == 3 ? -px.y : 0.0);
			float dk = texture(depth_tex, uv + o).r;
			if (dk <= 1e-6) continue;
			vec4 vk = INV_PROJECTION_MATRIX * vec4((uv + o) * 2.0 - 1.0, dk, 1.0);
			vk.xyz /= vk.w;
			float hk = (INV_VIEW_MATRIX * vec4(vk.xyz, 1.0)).y - swim_level;
			above = max(above, max(hk - focus_above, 0.0) * 0.85);
		}
	}

	// Refraction: the picture swims very slightly, more for what lies deeper.
	vec2 wob = vec2(sin(uv.y * 38.0 + TIME * 1.3 + uv.x * 9.0), cos(uv.x * 31.0 + TIME * 1.1 - uv.y * 7.0));
	vec2 suv = uv + wob * wobble * (0.0006 + 0.0012 * smoothstep(0.0, 6.0, below));
	// Her swells: where this pixel's ray crosses her plane, a ring of moving water spreading from where she pushed off —
	// the currents' shimmer (CurrentView): the picture wavers, bent by eddies of denser water boiling as they go, and is
	// shoved outward along the ring; the water thickens a shade where it passes. No surface ripple, no lines.
	float swell_density = 0.0;
	if (swell_count > 0 || current_count > 0 || leak_count > 0) {
		vec4 vm = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, 0.5, 1.0);
		vec3 ray = normalize((INV_VIEW_MATRIX * vec4(vm.xyz / vm.w, 0.0)).xyz);
		vec3 cam = INV_VIEW_MATRIX[3].xyz;
		vec3 sp = cam + ray * ((swim_level + 0.4 - cam.y) / min(ray.y, -1e-3));
		// The water's eddies (as the currents' own): slow swirls with fine shivers riding them.
		vec2 q = sp.xz;
		float big = value_noise(q * 0.42 + vec2(0.0, TIME * 0.22));
		float ea = value_noise(q * 1.1 + vec2(big * 1.5, TIME * 0.55) + 3.7);
		float eb = value_noise(q * 0.95 + vec2(-TIME * 0.45, big * 1.5) + 19.1);
		float fine = value_noise(q * 2.8 + vec2(TIME * 0.9, -TIME * 0.7) + 41.3);
		vec2 eddy = vec2(ea - 0.5, eb - 0.5) + (fine - 0.5) * vec2(0.55, -0.55);
		float patch = mix(0.5, 1.0, smoothstep(0.25, 0.7, big));
		vec2 bend = vec2(0.0);
		for (int i = 0; i < swell_count; i++) {
			vec4 s = swells[i];
			vec2 d = sp.xz - s.xy;
			float r = length(d);
			vec2 out_dir = d / max(r, 1e-3);
			float age = s.z;
			// Out to about 2.6 m (times its reach, z), quick at first (sharper still with a snap, w), easing; widening as
			// it goes; fading out.
			float reach = swell_dirs[i].z > 0.0 ? swell_dirs[i].z : 1.0;
			float snap = swell_dirs[i].w > 0.0 ? swell_dirs[i].w : 2.0;
			float front = mix(0.25, 2.6 * reach, 1.0 - pow(1.0 - age, snap));
			float width = (0.35 + 0.55 * age) * sqrt(reach);
			// A sharp leading edge, a softer wake behind it.
			float ahead = r - front;
			float band = exp(-pow(ahead / (ahead > 0.0 ? width * 0.6 : width * 1.3), 2.0));
			// Strongest behind her (the water she pushed), still there ahead of her; a ring with no way, even.
			float behind = dot(swell_dirs[i].xy, swell_dirs[i].xy) > 0.25 ? 0.45 + 0.55 * max(0.0, dot(out_dir, -swell_dirs[i].xy)) : 1.0;
			float k = s.w * band * behind * patch * (1.0 - age) * (1.0 - age);
			bend += (out_dir * 0.6 + eddy) * k;
			swell_density += k;
		}
		// Metres on her plane to the screen (east is right, south, +z, is down): the framing shows about 25.6 m top to
		// bottom.
		suv += bend * 2.5 / 25.6 * vec2(VIEWPORT_SIZE.y / VIEWPORT_SIZE.x, 1.0) * wobble;
		// The leaks: hot air over each one's disc, where the ink comes through (the framing's 60° tilt taken out of its
		// height).
		for (int i = 0; i < leak_count; i++) {
			vec2 d = (sp.xz - leaks[i].xy) * vec2(1.0, look_squash);
			if (dot(d, d) > 4.0) continue;
			float hot = (1.0 - smoothstep(0.35, 1.1, length(d))) * leaks[i].z;
			suv += (eddy * 0.9 + vec2(0.0, sin(TIME * 5.0 + sp.x * 3.0) * 0.12)) * hot * 0.6 / 25.6 * vec2(VIEWPORT_SIZE.y / VIEWPORT_SIZE.x, 1.0);
		}
		// The currents: the picture wavers, like the air over a hot road, bent by eddies of denser water rolling down the
		// course with the flow and boiling as they go. It only bends: the water stays as bright as it is.
		if (current_count > 0) {
			vec2 cuv = sp.xz / 140.0;
			vec4 cm = texture(current_map, cuv);
			vec2 flow = current_flows[clamp(int(texture(current_ids, cuv).a + 0.5), 0, 5)];
			if (cm.b * flow.y > 0.001 && cuv.x > 0.0 && cuv.y > 0.0 && cuv.x < 1.0 && cuv.y < 1.0) {
				vec2 m = vec2(cm.r - flow.x, cm.g);
				float t = TIME;
				float cbig = value_noise(m * vec2(0.32, 0.42) + vec2(0.0, t * 0.22));
				float ca = value_noise(m * vec2(0.9, 1.25) + vec2(cbig * 1.5, t * 0.55) + 3.7);
				float cb = value_noise(m * vec2(1.05, 0.85) + vec2(-t * 0.45, cbig * 1.5) + 19.1);
				float cfine = value_noise(m * vec2(2.6, 3.1) + vec2(t * 0.9, -t * 0.7) + 41.3);
				vec2 cbend = vec2(ca - 0.5, cb - 0.5) + (cfine - 0.5) * vec2(0.55, -0.55);
				float cmask = cm.b * flow.y * mix(0.35, 1.0, smoothstep(0.25, 0.7, cbig));
				suv += cbend * 0.5 * cmask / 25.6 * vec2(VIEWPORT_SIZE.y / VIEWPORT_SIZE.x, 1.0);
			}
		}
	}

	float hole_deep = 0.0;
	if (hole.w > 0.0) {
		hole_deep = hole.w * (1.0 - smoothstep(hole.z * 0.72, hole.z * 1.02, distance(world.xz, hole.xy))) * (1.0 - smoothstep(-5.0, -2.0, world.y));
	}

	// Painted, the depths below her are stated as flat layers of water (each a step deeper, hazier), barely blurred.
	float layered = below;
	// (Watercolour, look 7: its own water, below.)
	bool paper = reef_art == 7;
	// (Water: all that lies under the walls' line, as the reef's surface paints it, reef_art.gdshaderinc's art_water.)
	float paper_deep = !paper ? 0.0 : open_deep ? 1.0 : 1.0 - smoothstep(0.06, 0.14, h);
	if (reef_art > 0 && reef_art != 4 && reef_art != 5 && !paper && !open_deep) {
		float x = below / 2.2;
		layered = (floor(x) + smoothstep(0.8, 1.0, fract(x))) * 2.2;
	}
	// The deeper, the softer: a few metres down (a shallow seabed's dunes) only a little, its full blur by about 14 m.
	// Drawn in felt tip (look 4), the deep is not blurred at all: the stippling states it.
	float lod = below_blur * (reef_art == 4 ? 0.0 : reef_art > 0 && reef_art != 5 ? 0.12 : 1.0) * (below > 0.0 ? 0.2 + 0.8 * smoothstep(0.0, 14.0, below) : 0.0) * smoothstep(0.0, 0.8, below)
		+ above_blur * smoothstep(0.0, 9.0, above);
	float peak = open_deep ? 0.0 : smoothstep(peak_from, peak_from + peak_span, h);
	lod += peak_blur * peak;
	// Watercolour: the walls stay crisp at every height; all that lies under her plane runs soft, wet into wet.
	if (paper) lod = paper_blur * smoothstep(0.5, 4.0, below);
	vec3 col = blurred(suv, lod);
	// Blasts' marks: inside each, the picture smeared in toward its middle (a zoom blur), strongest at its heart,
	// softening out to its rim and fading as it ages — painterly, never a bright disc.
	if (blur_count > 0) {
		vec4 bm = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, 0.5, 1.0);
		vec3 bray = normalize((INV_VIEW_MATRIX * vec4(bm.xyz / bm.w, 0.0)).xyz);
		vec3 bcam = INV_VIEW_MATRIX[3].xyz;
		vec3 bp = bcam + bray * ((swim_level - bcam.y) / min(bray.y, -1e-3));
		for (int i = 0; i < blur_count; i++) {
			vec4 b = blurs[i];
			float r = distance(bp.xz, b.xy);
			float k = (1.0 - smoothstep(b.w * 0.35, b.w, r)) * (1.0 - b.z) * (1.0 - b.z);
			if (k < 0.002) continue;
			vec4 c = PROJECTION_MATRIX * VIEW_MATRIX * vec4(b.x, swim_level, b.y, 1.0);
			vec2 cuv = c.xy / c.w * 0.5 + 0.5;
			vec3 acc = col;
			for (int t = 1; t <= 6; t++) acc += blurred(mix(suv, cuv, float(t) / 6.0 * 0.18 * k), lod + 0.8 * k);
			col = mix(col, acc / 7.0, clamp(k * 1.6, 0.0, 1.0));
		}
	}

	// Felt tip and highlighter (look 6): the marker's flat tones, and the felt tip's line round every silhouette. Laid on
	// before the water, so what lies deep has its line softened by the water over it, as all else there.
	if (reef_art == 6 && !open_deep) {
		float reef = max(smoothstep(0.9, 2.2, below), smoothstep(0.6, 2.0, above));
		col = art_marker_fill(col, mix(1.25, 0.85, reef), 1.0 - reef);
		// The boldest line on what is at her plane (the creatures, the bosses); the floor's own things under it lightly,
		// so the reef never takes the eye from them.
		float line = art_outline(uv, FRAGCOORD.xy, INV_PROJECTION_MATRIX, INV_VIEW_MATRIX, VIEWPORT_SIZE) * mix(1.0, 0.3, smoothstep(0.9, 2.2, below));
		// Navy ink on the lit page; in a cave's dark a fluorescent line instead, as a highlighter on black paper.
		col = mix(col, mix(vec3(0.025, 0.035, 0.085), vec3(0.12, 0.55, 0.7), reef_dark), line * mix(0.7, 0.35, reef_dark));
	}

	// How much water this pixel is seen through (the painted looks: as deep as it lies).
	bool one_reef = (reef_art == 0 || reef_art == 5 || paper) && !open_deep;
	float seen = !floor_fog ? 0.0 : one_reef ? mix(reef_water, below, smoothstep(far_water, far_water + 6.0, below)) : layered;
	// Everything is seen through water: reds fade and a turquoise in-scatter rises with the water between the sea
	// surface and what this pixel shows.
	float column_in = clamp((sea_surface_y - (open_deep ? -40.0 : one_reef ? swim_level - seen : world.y)) / 20.0, 0.0, 1.5);
	// What her own light falls on keeps its warmth: her light travels a few metres, not down from the surface.
	float lit = open_deep ? 0.0 : her_clear(world);
	column_in *= 1.0 - 0.55 * reef_showcase;
	col *= mix(vec3(1.0), vec3(0.64, 0.9, 1.05), column_in * (1.0 - 0.6 * lit));
	col = mix(col, reef_water_deep * 0.7, 0.13 * column_in);

	// The water over it: clear in the Shallows (a light veil), thick murk deeper down (reef_fog); and past the reef, the
	// deeps sink into it, the depth's murk far below.
	vec3 water = mix(reef_water_deep, reef_murk, smoothstep(2.0, 16.0, seen));
	float veil = mix(0.18, 0.5, reef_fog) * (reef_art > 0 && reef_art != 5 && !paper ? 0.7 : 1.0);
	float fog = smoothstep(0.0, 1.8, seen) * (veil + (0.85 - veil) * (1.0 - exp(-seen * deep_fog * (0.6 + reef_fog))));
	// Drawn in felt tip (look 4), the water a lighter veil, so the stippling under it still reads.
	if (reef_art == 4) fog *= 0.55;
	// The water she lights is a thinner veil over what she shows, never none: what lies deeper still sinks into the water
	// in her light, so the reef keeps its depth round her.
	col = mix(col, water, fog * (1.0 - 0.45 * lit));
	// Under her plane, the water thickens into a fog that hides the depths (thinner where her light reaches down).
	if (below_fog > 0.0 && !paper && !open_deep && below > 0.0) {
		float under = below_fog * smoothstep(0.15, below_fog_depth, below) * (1.0 - 0.5 * lit);
		col = mix(col, mix(reef_water_deep, reef_murk, 0.2), under);
	}
	// Watercolour: where nothing is drawn (the open deep), the water's dark wash and its paper, as the reef's surface
	// paints them under her plane (reef_art.gdshaderinc's art_wash).
	if (paper && open_deep) {
		float deep = 1.0;
		vec3 dark = mix(reef_water_deep, reef_murk, 0.45) * paper_dark;
		col = mix(col, dark, deep * (0.86 - 0.3 * lit));
		vec4 pm = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, 0.5, 1.0);
		vec3 pray = normalize((INV_VIEW_MATRIX * vec4(pm.xyz / pm.w, 0.0)).xyz);
		vec3 pcam = INV_VIEW_MATRIX[3].xyz;
		vec2 on_plane = (pcam + pray * ((swim_level - pcam.y) / min(pray.y, -1e-3))).xz + world_origin;
		float tooth = value_noise(on_plane * 9.0) * 0.55 + value_noise(on_plane * 21.0 + 13.0) * 0.45;
		float mottle = value_noise(on_plane * 0.9 + 5.0) * 0.6 + value_noise(on_plane * 2.3 + 17.0) * 0.4;
		// (And the wash itself uneven: broad soft clouds where it dried lighter and darker.)
		float cloud = value_noise(on_plane * 0.13 + 71.0) * 0.6 + value_noise(on_plane * 0.37 + 9.0) * 0.4;
		float hollow = smoothstep(0.35, 0.75, tooth);
		col *= mix(1.0, (0.9 + 0.12 * mottle) * (1.0 - 0.26 * hollow) * (1.0 + 0.08 * (1.0 - hollow)) * (0.7 + 0.6 * cloud), deep);
	}
	// Fish far under her plane: their vague shadows on all that lies below it, where this pixel's ray crosses their depth (over the
	// water's veil, which would wash them away).
	if (deep_fish_count > 0 && below > 0.0) {
		vec4 fm = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, 0.5, 1.0);
		vec3 fray = normalize((INV_VIEW_MATRIX * vec4(fm.xyz / fm.w, 0.0)).xyz);
		vec3 fcam = INV_VIEW_MATRIX[3].xyz;
		float down = 1.0 / min(fray.y, -1e-3);
		float shadow = 0.0;
		for (int i = 0; i < deep_fish_count; i++) {
			vec2 at = fcam.xz + fray.xz * (swim_level - deep_fish[i].z - fcam.y) * down;
			shadow = max(shadow, fish_shadow(at, deep_fish[i], deep_fish_ways[i]));
		}
		col *= 1.0 - deep_fish_shade * shadow * smoothstep(0.0, 1.5, below);
	}
	// A way down: the ground gives way to the water itself.
	col = mix(col, reef_water_deep, hole_deep);
	// Her swells thicken the water a shade where they pass.
	col = mix(col, reef_water_deep, clamp(swell_density, 0.0, 1.0) * 0.05);

	// Her glow lands on the floor (DESIGN-TOPDOWN §5): in daylight bioluminescence reads not as brightness but as a
	// warm tint on what is near, and as shadow contrast. A broad warm pool (her_glow.w metres, showing on pale sand
	// too) lets the reef advertise her; a small soft dark blob straight under the bell (about 1.2× its radius) gives her
	// bright body the dark anchor it needs to pop. Laid on after the water's veil so neither is washed out; only on what
	// lies below her (never rock over her head), fading as the gap below her grows. She herself is drawn after this
	// pass, so the shadow never darkens her.
	if (her_glow_strength > 0.0 && !open_deep) {
		float gap = her_glow.y - world.y;
		// Round her as the eye sees her: the point of the floor behind her on the screen (the camera is tilted, so what lies
		// straight under her shows south of her).
		float d = distance(world.xz, her_glow.xz - vec2(0.0, look_slant * gap));
		float k = her_glow_strength * smoothstep(0.1, 0.5, gap) * (1.0 - smoothstep(5.0, 10.0, gap));
		// Only where her light reaches: the reef's walls and rises keep it from what lies behind them.
		float pool = exp(-pow(d / (her_glow.w * 0.55), 2.0)) * k * her_sight(world);
		vec3 warm = her_warm;
		if (reef_art == 6) {
			// A highlighter's wash: flat to its edge, the edge a little ragged, the colour vivid.
			float r = d / (her_glow.w * 0.45) + (value_noise(world.xz * 1.3) - 0.5) * 0.12;
			pool = (1.0 - smoothstep(0.78, 0.9, r)) * k * her_sight(world);
			// Her light's own colour, paled toward white, not the orange of her warm tint.
			warm = mix(her_light, vec3(1.0), 0.45);
			col = mix(col, col * warm * 1.15 + warm * 0.04, pool * 0.18);
		}
		else col = mix(col, col * warm * 1.15 + warm * 0.06, pool * 0.65);
		float shadow = (1.0 - smoothstep(0.1, 0.62, d)) * k * (1.0 - smoothstep(2.0, 5.0, gap));
		col *= 1.0 - 0.6 * shadow;
	}
	vec3 cam = CAMERA_POSITION_WORLD;
	vec3 ray = normalize(world - cam);

	// God rays: march the view ray through the water column, from the sea surface down to what it hits, summing the
	// shaft density (parallel to the sun, drifting with the swell) where the sun reaches. Longer water paths gather more
	// light; rock that shades the water leaves it dark, so the light falls in beams past the walls and through arches.
	vec3 hit = open_deep ? cam + ray * 90.0 : world;
	float t0 = ray.y < -1e-3 ? max((sea_surface_y - cam.y) / ray.y, 0.0) : 0.0;
	float t1 = max(dot(hit - cam, ray), t0);
	float span = t1 - t0;
	float shafts = 0.0;
	if (reef_rays > 0.0 && span > 0.1) {
		float jitter = hash12(FRAGCOORD.xy + fract(TIME) * 61.0);
		for (int i = 0; i < 10; i++) {
			vec3 q = cam + ray * (t0 + span * (float(i) + jitter) / 10.0);
			// Only where the sun reaches: rock, arches and cave roofs cut the shafts off (SunMap).
			shafts += sun_shafts(q + vec3(world_origin.x, 0.0, world_origin.y), TIME) * sun_lit(q);
		}
		shafts /= 10.0;
	}
	float column = smoothstep(0.0, 26.0, span);
	col += reef_sun * shafts * column * reef_rays * light_strength * 0.22 * (1.0 - 0.5 * smoothstep(0.0, 8.0, above));

	// Her glow scatters in the water her light passes through, along this pixel's whole view ray (light_field): a soft
	// glow round her.
	vec3 on_layer = cam + ray * ((player_pos.y - cam.y) / min(ray.y, -1e-3));
	float ign = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
	// (The drawn look's bubbles' halos are stronger, for their fluorescence; her own glow in the water fainter and
	// nearer white, so the floor round her is not washed orange.)
	float glow_boost = reef_art == 6 ? 1.35 : 1.0;
	vec3 her_tint = reef_art == 6 ? mix(her_warm, vec3(1.0), 0.4) : mix(her_warm, vec3(0.95, 0.97, 1.0), 0.55 * reef_showcase);
	float her_k = reef_art == 6 ? 0.55 : 1.0;
	col += her_tint * her_inscatter(cam, ray, t0, open_deep ? t0 + 120.0 : t1, ign) * her_k;
	// Her light: a faint glow in the water all round her, and where it falls on the reef a warm lift of what is there
	// (its own colour brightened, not a haze laid over it), fading as a lamp's light does.
	col += her_tint * her_water(cam, ray, t0, open_deep ? t0 + 120.0 : t1, ign) * 0.15 * her_k;
	if (!open_deep) {
		float on = her_reach(world) * her_k;
		col = col * (1.0 + her_tint * on * (her_seat > 0.0 ? 0.42 : 0.6) * her_lantern) + her_tint * on * on * 0.05 * her_lantern;
		vec2 seat = on_layer.xz - player_pos.xz;
		col *= 1.0 - her_seat * exp(-dot(seat, seat) / 1.7);
	}
	// Her bubbles carry light: each a small halo of its own on the same layer, the same depth-darkness scaling as hers
	// (subtle in the bright Shallows, glorious in the deep).
	float bubbles = 0.0;
	for (int i = 0; i < bubble_count; i++) {
		vec4 b = bubble_lights[i];
		vec2 ob = on_layer.xz - b.xy;
		bubbles += exp(-dot(ob, ob) / (b.z * b.z)) * b.w;
	}
	col += bubble_color * bubbles * halo_strength * glow_boost * glow_boost * (0.35 + 0.65 * fog) * (1.0 - smoothstep(0.0, 4.0, above)) * (0.6 + 0.8 * reef_menace);
	// The clam rooms' light, each in its pearl's colour: lifting what lies in them toward it, and a haze of it in the
	// water over them, so a room shows from afar what it holds.
	vec3 clam = vec3(0.0);
	float pearl_shown = 0.0;
	for (int i = 0; i < clam_count; i++) {
		vec4 g = clam_glows[i];
		vec2 og = on_layer.xz - g.xy;
		float near_glow = exp(-dot(og, og) / (g.z * g.z));
		clam += clam_colors[i] * near_glow * g.w;
		pearl_shown = max(pearl_shown, near_glow * min(g.w * 3.0, 1.0));
	}
	// (Gently: lifted hard, the room and its pearl wash out and the pearl's pattern is lost.)
	col = col * (1.0 + clam * 0.45) + clam * 0.1;

	// Grade: the depth's saturation; shadows cooled toward the water, highlights a touch of sun. Outside her light (her
	// glow and her beam, and what the sealed leaks have woken) the reef keeps little of its colour and some of its light.
	float shown = open_deep ? 0.0 : max(max(lit, reef_show(world)), pearl_shown * 0.9);
	col *= mix(unlit_dim, 1.0, shown);
	float luma = dot(col, vec3(0.299, 0.587, 0.114));
	// (Its values drawn together where she does not light it: darks lifted a little, lights held down.)
	if (unlit_flatten > 0.0) {
		float flat_to = mix(luma, 0.2 * unlit_dim, unlit_flatten * (1.0 - shown) * (paper ? 0.0 : 1.0));
		col *= flat_to / max(luma, 1e-3);
		luma = flat_to;
	}
	// (The reef's brightest rolled off: her own body and light, drawn after this pass, stay the brightest on screen.)
	if (highlight_knee > 0.0) {
		float over_knee = max(luma - highlight_knee, 0.0);
		float rolled = 1.0 / (1.0 + 2.2 * over_knee);
		col *= rolled;
		luma *= rolled;
	}
	col = mix(vec3(luma), col, reef_saturation * mix(paper ? max(unlit_saturation, 0.55) : unlit_saturation, 1.0, shown));
	// Shadows take the water's hue at their own brightness (cool, not lifted).
	float wl = max(dot(reef_water_deep, vec3(0.299, 0.587, 0.114)), 0.01);
	// (Not what her own light falls on: her warmth reaches the eye.)
	col = mix(col, reef_water_deep / wl * luma, (1.0 - smoothstep(0.0, 0.3, luma)) * 0.45 * (1.0 - lit) * (1.0 - 0.5 * reef_showcase));
	col = mix(col, col * (reef_sun * 0.2 + 0.82), smoothstep(0.5, 1.0, luma) * 0.3 * (1.0 - lit));

	// A cave's dark is never black: where the depth breaks (an edge of rock, a ledge, a creature's rim) a faint cool line
	// shows what is there, all but hidden; not where her light already shows it.
	if (reef_dark > 0.0) {
		vec2 px = 1.5 / VIEWPORT_SIZE;
		float zc = view_depth(uv, INV_PROJECTION_MATRIX);
		float e = abs(view_depth(uv + vec2(px.x, 0.0), INV_PROJECTION_MATRIX) - zc) + abs(view_depth(uv - vec2(px.x, 0.0), INV_PROJECTION_MATRIX) - zc)
			+ abs(view_depth(uv + vec2(0.0, px.y), INV_PROJECTION_MATRIX) - zc) + abs(view_depth(uv - vec2(0.0, px.y), INV_PROJECTION_MATRIX) - zc);
		float edge = smoothstep(0.015, 0.09, e / max(zc, 1.0));
		col += vec3(0.2, 0.25, 0.4) * edge * 0.22 * reef_dark * (1.0 - lit);
	}

	// The sheet's paper (look 6): a fine tooth, and the faint unevenness of a wash dried on it.
	if (reef_art == 6) {
		float tooth = hash12(floor(FRAGCOORD.xy * 0.5));
		float wash = value_noise(FRAGCOORD.xy * 0.006) * 0.6 + value_noise(FRAGCOORD.xy * 0.02 + 9.0) * 0.4;
		col *= 0.955 + 0.05 * tooth + 0.06 * (wash - 0.5);
	}
	// Vignette, oval to the screen, tinted by the deep water; heavier as the depths darken (and for the drawn look's
	// contrast).
	vec2 d = (uv - 0.5) * vec2(VIEWPORT_SIZE.x / VIEWPORT_SIZE.y, 1.0);
	float v = smoothstep(0.32, 1.05, length(d) * 1.3);
	col = mix(col, reef_murk * 0.25, v * reef_vignette * (reef_art == 6 ? 1.3 : 1.0));
	// A big-merge kill lifts the screen's edges toward white for an instant (never its middle).
	col = mix(col, vec3(1.0, 0.97, 0.9), edge_flash * 0.55 * smoothstep(0.45, 1.15, length(d) * 1.3));

	// Chromatic aberration: red pushed out, blue pulled in, more toward the edges (the prism's split).
	if (aberration > 0.001) {
		vec2 dir = (uv - 0.5) * aberration * 0.035;
		vec3 base = textureLod(screen_tex, uv, 0.0).rgb;
		float r = textureLod(screen_tex, uv + dir, 0.0).r, b = textureLod(screen_tex, uv - dir, 0.0).b;
		col += (vec3(r, base.g, b) - base);
	}

	if (debug_view == 1 && !open_deep) {
		float g = dot(textureLod(screen_tex, uv, 0.0).rgb, vec3(0.3, 0.6, 0.1));
		col = vec3(g * 0.35) + vec3(0.9, 0.8, 0.1) * sun_lit(world) * 0.35 + vec3(1.0, 0.35, 0.0) * her_vis(world) * smoothstep(her_light_at.w, her_light_at.w * 0.8, distance(world, her_light_at.xyz)) * 0.6;
	}
	if (debug_view == 7) col = vec3(art_outline(uv, FRAGCOORD.xy, INV_PROJECTION_MATRIX, INV_VIEW_MATRIX, VIEWPORT_SIZE)) * (reef_art == 6 ? vec3(1.0, 0.2, 0.2) : vec3(0.2, 1.0, 0.2));
	ALBEDO = max(col, vec3(0.0));
}
