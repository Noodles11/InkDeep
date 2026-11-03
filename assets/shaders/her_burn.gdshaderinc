// Her searchlight burning the corrupted (docs/ADVENTURE.md §8; PlaneHerLight.cs), shared by every shader of a corrupted
// body.
//
// Where her beam falls on a corrupted body — inside its cone and its reach, the side of the body facing her bell, nothing
// of the reef in between — the ink sears. Not fire (it is under water): the ink bleaches and boils off. Its black thins to a pale,
// glassy crust lit in her own colour from within, crazed by a crawling net of white-hot seams, fizzing with tiny
// boiling specks; where the burn ends the crust meets the dark ink in one hard bright seam, its edge torn a little,
// with a scorched dark band just past it. A body half in her beam shows the beam's edge across it so; a body wholly
// in it shows it where its lit side turns away from her.
//   her_burn   the beam's start, her bell's opening (world xyz), and how far it burns (w; 0: it burns nothing)
//   her_beam   the way it shines (xyz, unit) and the cosine of its half-angle (w)
//   her_light  her light's colour (HerColor)

#include "res://assets/shaders/light_field.gdshaderinc"

global uniform vec4 her_burn;
global uniform vec4 her_beam;
global uniform vec3 her_light;

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

// The burn on a point of a corrupted body (world position and normal), as much of it as amount allows (0..1).
void her_burn_paint(vec3 world, vec3 n, float spot, float amount, inout vec3 albedo, inout vec3 emission, inout float roughness) {
	if (her_burn.w <= 0.0 || amount <= 0.0) return;
	vec3 to = her_burn.xyz - world;
	float d = length(to);
	// The beam's edge: how far (metres, across the beam) the point lies outside its cone, or past its reach, torn a
	// little and wavering.
	float torn = (hb_noise(world * 4.0 + vec3(0.0, TIME * 0.8, spot)) - 0.5) * 0.12;
	// (A fan, taller than wide: up and down count half, as PlaneHerLight's BeamFlat.)
	vec3 flat_ = vec3(1.0, 0.5, 1.0);
	float off = acos(clamp(dot(normalize(-to * flat_), normalize(her_beam.xyz * flat_)), -1.0, 1.0)) - acos(clamp(her_beam.w, -1.0, 1.0));
	float past = max(off * d, d - her_burn.w) + torn;
	float inside = 1.0 - smoothstep(-0.012, 0.012, past);
	// Its side facing her bell (a little more from above, so a body is lit over its top, where the camera sees it); the
	// line where it turns away torn the same.
	vec3 from_her = normalize(to / max(d, 1e-3) + vec3(0.0, 0.35, 0.0));
	float facing = dot(normalize(n), from_her) + (hb_noise(world * 7.0 + spot) - 0.5) * 0.14;
	float front = smoothstep(-0.015, 0.03, facing);
	float sight = smoothstep(0.35, 0.65, her_sight(world));
	float lit = inside * front * sight * amount;
	// The seam: where the burn ends, across the reach's edge or round the turn of its body.
	float seam = max((1.0 - smoothstep(0.0, 0.03, abs(past))) * front, (1.0 - smoothstep(0.0, 0.05, abs(facing - 0.008))) * inside) * sight * amount;
	// Just past it, on the dark side, the ink scorched darker.
	float scorch = max(smoothstep(0.0, 0.03, past) * (1.0 - smoothstep(0.03, 0.12, past)) * front,
		(1.0 - smoothstep(-0.12, -0.02, facing)) * smoothstep(-0.3, -0.12, facing) * inside) * sight * amount;
	vec3 c = her_light;
	vec3 hot = mix(c, vec3(1.0), 0.35) * 1.2;
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
	emission += lit * (c * (0.55 + 0.6 * boil) * (0.45 + 0.75 * cell) * glow + hot * (craze * 2.4 + fizz * 3.0)) * flicker;
	emission = mix(emission, emission * 0.3, scorch);
	emission += hot * seam * 4.5 * flicker;
}
