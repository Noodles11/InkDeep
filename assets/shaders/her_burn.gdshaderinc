// Her shots' light burning the corrupted (docs/ADVENTURE.md §8; PlaneHerLight.cs), shared by every shader of a corrupted
// body.
//
// Where her light reaches a corrupted body — within its reach, the side of the body facing it — it lights that side in
// her light's colours, as a lamp lights what it shines on: no paint over the ink, only light on it, softest round the turn
// of the body and at the light's edge, streaming over it like a current pressing on it (her_burn_paint). (The sim lights
// only what is in sight of her light; the game sends only lights burning something.)
//   her_burn0…3  a burning shot's light (world xyz), and how far it reaches (w; 0: none)
//   her_beam_at  her beam's origin (world xyz) and how far it reaches to the rock (w; 0: off or burning nothing)
//   her_beam_dir its direction (xyz) and half-angle (w, radians): it burns what lies in its cone
//   her_light    her light's colour (HerColor)
//   her_burn_tint the colour of her light burning (rgb; BeamView: her beam's) and its colours' bits (w: 1 ember, 2 bloom, 4 frost)
//   her_aura     her aura (world xyz) and its reach (w): what is seen only in her light shows within it (her_reveal)
//   her_beam_lit her beam's origin and its reach to the rock while it shines (w; 0 off), its cone as her_beam_dir's;
//                her_beam2_lit/_dir and her_beam3_lit/_dir: Rear Fin's and Lighthouse's beams, the same

#include "res://assets/shaders/light_field.gdshaderinc"

global uniform vec4 her_burn0;
global uniform vec4 her_burn1;
global uniform vec4 her_burn2;
global uniform vec4 her_burn3;
global uniform vec4 her_beam_at;
global uniform vec4 her_beam_dir;
global uniform vec3 her_light;
global uniform vec4 her_burn_tint;
global uniform vec4 her_aura;
global uniform vec4 her_beam_lit;
global uniform vec4 her_beam2_lit;
global uniform vec4 her_beam2_dir;
global uniform vec4 her_beam3_lit;
global uniform vec4 her_beam3_dir;

float hb_hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

float hb_noise(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = mix(mix(hb_hash(i), hb_hash(i + vec3(1, 0, 0)), f.x), mix(hb_hash(i + vec3(0, 1, 0)), hb_hash(i + vec3(1, 1, 0)), f.x), f.y);
	float b = mix(mix(hb_hash(i + vec3(0, 0, 1)), hb_hash(i + vec3(1, 0, 1)), f.x), mix(hb_hash(i + vec3(0, 1, 1)), hb_hash(i + vec3(1, 1, 1)), f.x), f.y);
	return mix(a, b, f.z);
}

// One light on a point (to: from the point to the light; past: how far, metres, the point lies past the light's edge):
// how much of it falls there, soft at its edge and round the turn of the body (level, on her plane: the side, not the top), how squarely the point faces it, and which way it runs there; the strongest kept.
void hb_take(vec3 to, float past, vec3 n, inout float lit, inout float facing_out, inout vec3 way_out) {
	float d = max(length(to), 1e-3);
	// (Level: the light lies on her plane, so it lights a body's side, not its top.)
	vec2 flat_to = to.xz / max(length(to.xz), 1e-3);
	vec3 from_light = normalize(vec3(flat_to.x, 0.08, flat_to.y));
	float facing = dot(normalize(n), from_light);
	float l = (1.0 - smoothstep(-0.15, 0.15, past)) * smoothstep(-0.08, 0.3, facing);
	if (l > lit) {
		facing_out = facing;
		way_out = -to / d;
	}
	lit = max(lit, l);
}

void hb_light(vec4 light, vec3 world, vec3 n, inout float lit, inout float facing, inout vec3 way) {
	if (light.w <= 0.0) return;
	vec3 to = light.xyz - world;
	hb_take(to, length(to) - light.w, n, lit, facing, way);
}

// Her beam on a point: its edge the nearer of the end of its reach and the side of its cone (across, in metres there).
void hb_beam(vec3 world, vec3 n, inout float lit, inout float facing, inout vec3 way) {
	if (her_beam_at.w <= 0.0) return;
	vec3 to = her_beam_at.xyz - world;
	vec2 flat_to = -to.xz;
	float flat_d = max(length(flat_to), 1e-3);
	float angle = acos(clamp(dot(flat_to / flat_d, normalize(her_beam_dir.xz)), -1.0, 1.0));
	hb_take(to, max(length(to) - her_beam_at.w, flat_d * (angle - her_beam_dir.w)), n, lit, facing, way);
}

// Her light's colours on what it lights (PlaneColour.cs): one colour as it is; two or three mixed, each in its own
// patches drifting over the lit side, so every colour of it shows.
const vec3 HB_EMBER = vec3(1.0, 0.42, 0.16);
const vec3 HB_BLOOM = vec3(0.42, 1.0, 0.5);
const vec3 HB_FROST = vec3(0.55, 0.8, 1.0);
vec3 hb_tint(vec3 world, float spot) {
	float bits = her_burn_tint.w;
	float r = mod(bits, 2.0), g = mod(floor(bits / 2.0), 2.0), b = mod(floor(bits / 4.0), 2.0);
	if (r + g + b < 2.0) return her_burn_tint.rgb;
	float n1 = hb_noise(world * 3.0 + vec3(TIME * 0.6, 0.0, spot));
	float n2 = hb_noise(world * 3.0 + vec3(spot, TIME * 0.5, 7.3));
	vec3 w = vec3(r * (0.3 + n1), g * (0.3 + n2), b * (1.3 - n1));
	w = w * w * w;
	return (HB_EMBER * w.x + HB_BLOOM * w.y + HB_FROST * w.z) / max(w.x + w.y + w.z, 1e-4);
}

// How much of her light falls on a point (0 none, 1 well within it), for the corrupted, seen only in her light
// (docs/LIGHT-AND-GRAPH.md §4.1): her aura round her, her beam along its cone to the rock, a burning shot's.
float hb_within(vec4 light, vec3 world) {
	return light.w > 0.0 ? 1.0 - smoothstep(light.w * 0.8, light.w, length(world - light.xyz)) : 0.0;
}

float hb_cone(vec4 lit, vec4 dir, vec3 world) {
	if (lit.w <= 0.0) return 0.0;
	vec2 to = world.xz - lit.xz;
	float d = length(to);
	float angle = acos(clamp(dot(to / max(d, 1e-3), normalize(dir.xz)), -1.0, 1.0));
	float cone = (1.0 - smoothstep(lit.w - 0.6, lit.w, d)) * (1.0 - smoothstep(dir.w * 0.8, dir.w, angle));
	return d < 0.6 ? 1.0 : cone;
}

// Her reveal's two parts: her aura's (and her burning shots'), and her beams' (which a body in the way stops: the sim
// tells each creature whether a beam of hers reaches it, PlaneWorld.BeamSees).
float her_reveal_aura(vec3 world) {
	float s = her_aura.w > 0.0 ? 1.0 - smoothstep(her_aura.w * 0.8, her_aura.w, length(world.xz - her_aura.xz)) : 0.0;
	return max(s, max(max(hb_within(her_burn0, world), hb_within(her_burn1, world)), max(hb_within(her_burn2, world), hb_within(her_burn3, world))));
}
float her_reveal_beams(vec3 world) {
	return max(hb_cone(her_beam_lit, her_beam_dir, world), max(hb_cone(her_beam2_lit, her_beam2_dir, world), hb_cone(her_beam3_lit, her_beam3_dir, world)));
}
float her_reveal(vec3 world) {
	return max(her_reveal_aura(world), her_reveal_beams(world));
}

// Her light on a point of a corrupted body (world position and normal), as much of it as amount allows (0..1): lit as
// light lights a surface, in her light's colours, brightest where it faces the light squarely and falling softly to
// nothing round its turn, the current of it streaming over the body away from the light, and where it strikes
// squarely a soft brighter press of it. The ink's own look stays under it.
void her_burn_paint(vec3 world, vec3 n, float spot, float amount, inout vec3 albedo, inout vec3 emission, inout float roughness) {
	if (amount <= 0.0 || her_burn0.w + her_burn1.w + her_burn2.w + her_burn3.w + her_beam_at.w <= 0.0) return;
	float lit = 0.0, facing = -1.0;
	vec3 way = vec3(1.0, 0.0, 0.0);
	hb_light(her_burn0, world, n, lit, facing, way);
	hb_light(her_burn1, world, n, lit, facing, way);
	hb_light(her_burn2, world, n, lit, facing, way);
	hb_light(her_burn3, world, n, lit, facing, way);
	hb_beam(world, n, lit, facing, way);
	lit *= amount;
	if (lit <= 0.0) return;
	vec3 c = hb_tint(world, spot);
	float face = clamp(facing, 0.0, 1.0);
	vec3 q = world * 5.0 - way * TIME * 2.6;
	float stream = hb_noise(q + vec3(spot)) * 0.6 + hb_noise(q * 2.3 + vec3(0.0, spot, 3.1)) * 0.4;
	float pulse = 0.92 + 0.08 * sin(TIME * 13.0 + spot * 5.0);
	float shade = lit * face * sqrt(face);
	albedo = mix(albedo, albedo * 0.9 + c * 0.03, lit);
	roughness = mix(roughness, 0.4, lit);
	emission += c * shade * (0.5 + 0.5 * stream) * 0.9 * pulse;
	emission += c * lit * pow(face, 6.0) * 0.45 * pulse;
}
