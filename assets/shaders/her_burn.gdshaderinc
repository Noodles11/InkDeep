// Her shots' light burning the corrupted (docs/ADVENTURE.md §8; PlaneHerLight.cs), shared by every shader of a corrupted
// body.
//
// Where a shot's light reaches a corrupted body — within its reach, the side of the body facing the shot — the ink sears.
// Not fire (it is under water): the ink bleaches and boils off. Its black thins to a pale, glassy crust lit in her own
// colour from within, crazed by a crawling net of white-hot seams, fizzing with tiny boiling specks; where the burn
// ends the crust meets the dark ink in one hard bright seam, its edge torn a little, with a scorched dark band just past
// it. A body half in a shot's light shows the light's edge across it so; a body wholly in it shows it where its lit side
// turns away from the shot. (The sim lights only what is in sight of a shot; the game sends only shots burning something.)
//   her_burn0…3  a burning shot's light (world xyz), and how far it reaches (w; 0: none)
//   her_beam_at  her beam's origin (world xyz) and how far it reaches to the rock (w; 0: off or burning nothing)
//   her_beam_dir its direction (xyz) and half-angle (w, radians): it burns what lies in its cone
//   her_light    her light's colour (HerColor)
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

// The crazing: how near a point lies to the seam between two cells (1 on it), the cells drifting slowly.
float hb_craze(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int z = -1; z <= 1; z++)
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec3 c = vec3(float(x), float(y), float(z));
		vec3 o = vec3(hb_hash(i + c), hb_hash(i + c + 17.0), hb_hash(i + c + 41.0));
		o = 0.5 + 0.42 * sin(TIME * 0.9 + 6.2831 * o);
		float d = length(c + o - f);
		if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
	}
	return 1.0 - smoothstep(0.0, 0.09, d2 - d1);
}

// One shot's light on a point (world position, normal n): how much of it is lit, its seam and its scorch, and how
// squarely it faces the light; the strongest light kept.
void hb_light(vec4 light, vec3 world, vec3 n, float spot, inout float lit, inout float seam, inout float scorch, inout float facing_out) {
	if (light.w <= 0.0) return;
	vec3 to = light.xyz - world;
	float d = length(to);
	// The light's edge: how far (metres) the point lies past its reach, torn a little and wavering.
	float torn = (hb_noise(world * 4.0 + vec3(0.0, TIME * 0.8, spot)) - 0.5) * 0.12;
	float past = d - light.w + torn;
	float inside = 1.0 - smoothstep(-0.012, 0.012, past);
	// Its side facing the shot (a little more from above, so a body is lit over its top, where the camera sees it); the
	// line where it turns away torn the same.
	vec3 from_light = normalize(to / max(d, 1e-3) + vec3(0.0, 0.35, 0.0));
	float facing = dot(normalize(n), from_light) + (hb_noise(world * 7.0 + spot) - 0.5) * 0.14;
	float front = smoothstep(-0.015, 0.03, facing);
	float l = inside * front;
	if (l > lit) facing_out = facing;
	lit = max(lit, l);
	// The seam: where the burn ends, across the reach's edge or round the turn of its body.
	seam = max(seam, max((1.0 - smoothstep(0.0, 0.03, abs(past))) * front, (1.0 - smoothstep(0.0, 0.05, abs(facing - 0.008))) * inside));
	// Just past it, on the dark side, the ink scorched darker.
	scorch = max(scorch, max(smoothstep(0.0, 0.03, past) * (1.0 - smoothstep(0.03, 0.12, past)) * front,
		(1.0 - smoothstep(-0.12, -0.02, facing)) * smoothstep(-0.3, -0.12, facing) * inside));
}

// Her beam on a point: as one shot's light (lit on the side facing her), its edge the nearer of the end of its reach
// and the side of its cone (measured across, in metres at that distance).
void hb_beam(vec3 world, vec3 n, float spot, inout float lit, inout float seam, inout float scorch, inout float facing_out) {
	if (her_beam_at.w <= 0.0) return;
	vec3 to = her_beam_at.xyz - world;
	vec2 flat_to = -to.xz;
	float d = length(to);
	float flat_d = max(length(flat_to), 1e-3);
	float angle = acos(clamp(dot(flat_to / flat_d, normalize(her_beam_dir.xz)), -1.0, 1.0));
	float torn = (hb_noise(world * 4.0 + vec3(0.0, TIME * 0.8, spot)) - 0.5) * 0.12;
	float past = max(d - her_beam_at.w, flat_d * (angle - her_beam_dir.w)) + torn;
	float inside = 1.0 - smoothstep(-0.012, 0.012, past);
	vec3 from_light = normalize(to / max(d, 1e-3) + vec3(0.0, 0.35, 0.0));
	float facing = dot(normalize(n), from_light) + (hb_noise(world * 7.0 + spot) - 0.5) * 0.14;
	float front = smoothstep(-0.015, 0.03, facing);
	float l = inside * front;
	if (l > lit) facing_out = facing;
	lit = max(lit, l);
	seam = max(seam, max((1.0 - smoothstep(0.0, 0.03, abs(past))) * front, (1.0 - smoothstep(0.0, 0.05, abs(facing - 0.008))) * inside));
	scorch = max(scorch, max(smoothstep(0.0, 0.03, past) * (1.0 - smoothstep(0.03, 0.12, past)) * front,
		(1.0 - smoothstep(-0.12, -0.02, facing)) * smoothstep(-0.3, -0.12, facing) * inside));
}

// How much of her light falls on a point (0 none, 1 well within it), for the corrupted, seen only in her light
// (docs/LIGHT-AND-GRAPH-PROPOSAL.md §4.1): her aura round her, her beam along its cone to the rock, a burning shot's.
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

float her_reveal(vec3 world) {
	float s = her_aura.w > 0.0 ? 1.0 - smoothstep(her_aura.w * 0.8, her_aura.w, length(world.xz - her_aura.xz)) : 0.0;
	s = max(s, max(hb_cone(her_beam_lit, her_beam_dir, world), max(hb_cone(her_beam2_lit, her_beam2_dir, world), hb_cone(her_beam3_lit, her_beam3_dir, world))));
	s = max(s, max(max(hb_within(her_burn0, world), hb_within(her_burn1, world)), max(hb_within(her_burn2, world), hb_within(her_burn3, world))));
	return s;
}

// The burn on a point of a corrupted body (world position and normal), as much of it as amount allows (0..1).
void her_burn_paint(vec3 world, vec3 n, float spot, float amount, inout vec3 albedo, inout vec3 emission, inout float roughness) {
	if (amount <= 0.0 || her_burn0.w + her_burn1.w + her_burn2.w + her_burn3.w + her_beam_at.w <= 0.0) return;
	float lit = 0.0, seam = 0.0, scorch = 0.0, facing = -1.0;
	hb_light(her_burn0, world, n, spot, lit, seam, scorch, facing);
	hb_light(her_burn1, world, n, spot, lit, seam, scorch, facing);
	hb_light(her_burn2, world, n, spot, lit, seam, scorch, facing);
	hb_light(her_burn3, world, n, spot, lit, seam, scorch, facing);
	hb_beam(world, n, spot, lit, seam, scorch, facing);
	// Where one light's burn overlaps another's edge, no scorch shows inside the burn.
	scorch *= 1.0 - lit;
	lit *= amount;
	seam *= amount;
	scorch *= amount;
	if (lit + seam + scorch <= 0.0) return;
	vec3 c = her_light;
	vec3 hot = mix(c, vec3(1.0), 0.3);
	vec3 p = world * 3.4 + vec3(spot * 3.1);
	float craze = hb_craze(p + vec3(0.0, TIME * 0.35, 0.0));
	float boil = hb_noise(world * 11.0 + vec3(TIME * 2.2, -TIME * 1.7, spot));
	float fizz = step(0.965, hb_hash(floor(world * 40.0) + floor(TIME * 12.0))) * boil;
	float flicker = 0.85 + 0.15 * sin(TIME * 19.0 + spot * 7.0 + world.x * 3.0);
	// The crust: bleached pale, glassy, lit from within in her colour (molten amber in the cracks' cells, a cooler
	// pale glaze over them), brightest toward her.
	float glow = 0.45 + 0.55 * clamp(facing * 1.6, 0.0, 1.0);
	float cell = hb_noise(p * 0.8 + vec3(TIME * 0.2));
	albedo = mix(albedo, c * 0.25 + vec3(0.05), lit);
	roughness = mix(roughness, 0.25, lit);
	emission = mix(emission, emission * 0.2, lit);
	emission += lit * (c * (0.4 + 0.45 * boil) * (0.45 + 0.75 * cell) * glow + hot * (craze * 1.5 + fizz * 2.0)) * flicker;
	emission = mix(emission, emission * 0.3, scorch);
	emission += hot * seam * 2.8 * flicker;
}
