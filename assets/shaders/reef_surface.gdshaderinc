// The reef's skin: one look for every solid surface of a level — the seabed, the walls, the arches, the cave roofs,
// boulders and weak rock — worked out in world space from height and slope, so pieces made separately meet seamlessly:
// chalk (reef_surface below). Colours come from the depth's look (reef_* globals, set by ReefLook, linear). The rs_*
// noises are shared with other shaders (reef_rock's lumps).

#include "res://assets/shaders/sunlight.gdshaderinc"
#include "res://assets/shaders/level_frame.gdshaderinc"
#include "res://assets/shaders/reef_art.gdshaderinc"
#include "res://assets/shaders/reef_show.gdshaderinc"

global uniform vec3 reef_water_shallow;
global uniform vec3 reef_water_deep;
global uniform vec3 reef_murk;
global uniform vec3 reef_sand;
global uniform vec3 reef_rock;
global uniform vec3 reef_coral;
global uniform vec3 reef_coral2;
global uniform vec3 reef_algae;
global uniform vec3 reef_sponge;
global uniform vec3 reef_butter;
global uniform vec3 reef_sun;
global uniform float reef_caustics;
global uniform float reef_menace;
// The corruption (docs/CORRUPTION.md): this level's field, R = ink density, G = the gold of corruption dying (set per
// material by LevelView; off on any level that is not the one she is on or diving into).
uniform sampler2D corruption_map : filter_linear, repeat_disable;
uniform float corruption_on = 0.0;

struct ReefSkin {
	vec3 albedo;
	vec3 emission;
	vec3 normal;    // world space, perturbed by sand ripples and coral heads
	float rough;
	float caustic;  // extra sunlight from the caustics (the shaders' light() adds it where the sun reaches)
};

float rs_hash3(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

float rs_noise(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(rs_hash3(i), rs_hash3(i + vec3(1, 0, 0)), f.x), mix(rs_hash3(i + vec3(0, 1, 0)), rs_hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(rs_hash3(i + vec3(0, 0, 1)), rs_hash3(i + vec3(1, 0, 1)), f.x), mix(rs_hash3(i + vec3(0, 1, 1)), rs_hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float rs_fbm(vec3 p) {
	float s = 0.5 * rs_noise(p);
	s += 0.25 * rs_noise(p * 2.03 + 17.1);
	s += 0.125 * rs_noise(p * 4.07 + 31.7);
	return s / 0.875;
}

// The reef as chalk: one flat colour for the rock and one for the floor (the depth's own, drained toward grey and
// lifted: ReefLook's reef_rock and reef_sand), broken only by a few cheap noise masks: broad soft blotches, faint
// strata smeared along the height, and a fine dusty grain (gone where a pixel spans it). Matte, unrelieved, no
// caustics. local: the point in its level's own coordinates (level_frame.gdshaderinc); patterns are drawn at its
// absolute position, so a level looks the same wherever it is placed. px: how many metres one pixel spans here.
vec3 rs_chalk(vec3 c) {
	float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
	return mix(vec3(l), c, 0.45) * 1.15 + 0.02;
}

ReefSkin reef_surface(vec3 local, vec3 n, float time, float px) {
	ReefSkin s;
	vec3 w = lf_pattern(local);
	float h = local.y;
	// The floor (flat, under the swim level) one chalk, the rock another.
	float floorK = smoothstep(0.72, 0.92, n.y) * (1.0 - smoothstep(-0.4, 0.5, h));
	vec3 c = mix(rs_chalk(reef_rock), rs_chalk(reef_sand), floorK);
	// The masks: blotches, strata, grain.
	float blot = rs_noise(w * 0.08);
	float strata = rs_noise(vec3(w.x * 0.35, w.y * 2.2, w.z * 0.35) + 7.0);
	float grain = rs_noise(w * 4.5 + 3.0) * (1.0 - smoothstep(0.06, 0.2, px));
	c *= 0.84 + 0.28 * blot;
	c *= 0.95 + 0.1 * strata * (1.0 - floorK);
	c *= 0.96 + 0.08 * grain;
	// The impassable reef wall around the square: darker and cooler toward the edge of the world.
	float edge = min(min(local.x, local.z), min(reef_extent.x - local.x, reef_extent.y - local.z));
	float rim = 1.0 - smoothstep(0.0, 9.0, edge);
	c = mix(c, c * 0.55 + reef_murk * 0.2, rim * 0.6);
	s.albedo = c;
	s.normal = n;
	s.rough = 1.0;
	s.caustic = 0.0;
	s.emission = vec3(0.0);
	// The corruption leaves the floor itself as it is (docs/CORRUPTION.md): its mark is the ink dripping up out of it
	// (corruption_shards.gdshader), and when it is cleansed only those drops light up.
	return s;
}

// The showcase reef (reef_show.gdshaderinc): where her light shows it (and by as much as the reef is awake), the skin as
// it is (nat) over the drawn one (s), lit from where she is; a wave's front lights it as it passes. Returns how much
// it shows (0 off a showcase reef), for the light() pass.
float reef_show_skin(inout ReefSkin s, ReefSkin nat, vec3 world, vec3 cam) {
	if (reef_showcase <= 0.0) return 0.0;
	float show = reef_show(world, cam);
	s.albedo = mix(s.albedo, nat.albedo, show);
	s.normal = normalize(mix(s.normal, nat.normal, show));
	s.caustic = mix(s.caustic, nat.caustic, show);
	float lit = reef_lit(world, cam);
	if (lit > 0.0) {
		vec3 to = normalize(her_show_aura.xyz + vec3(0.0, 0.8, 0.0) - world);
		float ndl = clamp(dot(nat.normal, to), 0.0, 1.0);
		// (Watercolour, look 7: seen from straight overhead, the walls' tops face up, away from her; her light brings
		// their wash out whole.)
		if (reef_art == 7) ndl = 1.0;
		s.emission += nat.albedo * her_show_light * lit * (0.3 + 0.7 * ndl) * 1.25;
	}
	s.emission += (nat.albedo * 0.9 + 0.08) * her_show_light * reef_wave_front(world) * 1.4;
	// Awake, the reef has a faint light of its own again.
	s.emission += nat.albedo * reef_awake(world) * 0.1;
	return show;
}
