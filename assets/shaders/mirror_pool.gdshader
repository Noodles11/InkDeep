// The bay's mirror (docs/RPG-PROPOSAL.md §8): a still pool of polished nacre on the floor. It mirrors the water and
// the sky above (the reef's own colours: by the bay's summer night, a moon-blue sheen with the moon's ripples sliding
// over it and a few stars), thin-film nacre colours (rose, gold, sea green, lilac) round its lip and with the angle;
// and over it she shows in it too, her own glow mirrored under her, rings spreading from it when she is near. Lighter
// than the floor round it where a cave mouth is dark: it must never read as the Leak's.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled;

#include "res://assets/shaders/light_field.gdshaderinc"

global uniform vec3 her_light;
global uniform vec3 her_body;
global uniform vec3 reef_water_shallow;
global uniform vec3 reef_sun;

/// How near she is (0 out of reach → 1 over it).
uniform float near = 0.0;
/// The pool's radius in world metres (UV 0..1 spans its diameter).
uniform float radius = 1.7;

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Thin-film nacre colours, sliding with t.
vec3 nacre(float t) {
	return 0.7 + 0.28 * cos(6.2831 * (t + vec3(0.0, 0.33, 0.67)));
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	if (r > 1.0) discard;
	vec3 view = normalize((INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xyz);
	float grazing = 1.0 - clamp(view.y, 0.0, 1.0);
	// Growth lines, faint, round the middle as in a shell's inside.
	float lines = sin(r * 30.0 + sin(atan(p.y, p.x) * 3.0) * 0.6) * 0.5 + 0.5;
	vec3 film = nacre(r * 0.6 + grazing * 0.8 + lines * 0.05 + (v_world.x + v_world.z) * 0.02 + TIME * 0.01);
	// The water and sky above, mirrored: the sea's own colour lifted, the sun's (or the moon's) ripples sliding over it,
	// and a few stars twinkling in it.
	vec2 w = v_world.xz * 1.6;
	float ripple = sin(w.x + TIME * 0.7 + sin(w.y * 1.3 + TIME * 0.5)) * sin(w.y * 0.9 - TIME * 0.6 + sin(w.x * 1.1)) * 0.5 + 0.5;
	vec3 sky = reef_water_shallow * 1.5 + reef_sun * (0.08 + 0.3 * ripple * ripple);
	vec2 cell = floor(UV * 26.0);
	float seed = fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
	float star = step(0.93, seed) * (1.0 - smoothstep(0.05, 0.22, length(fract(UV * 26.0) - 0.5)))
		* (0.6 + 0.4 * sin(TIME * (1.5 + seed * 3.0) + seed * 40.0));
	sky += reef_sun * star * 1.2;
	float lip = smoothstep(0.55, 0.97, r);
	vec3 face = mix(sky, film * (0.25 + 0.75 * length(reef_water_shallow)), 0.3 + 0.4 * lip);

	// Her, mirrored: her glow under where she floats, in her colours, a ring of her body's round it.
	vec2 d = v_world.xz - her_light_at.xz;
	float dist = length(d);
	float her = exp(-dist * dist / 0.3) * near;
	float halo = exp(-dist * dist / 1.4) * near;
	float rings = (sin(dist * 13.0 - TIME * 2.6) * 0.5 + 0.5) * smoothstep(radius * 1.05, 0.2, dist) * smoothstep(0.15, 0.6, dist);

	// Its brightness is its own (what it mirrors), so her lamp close over it does not wash it white; with her over it,
	// its sheen dims to a deep nacre, and she shows the clearer in it.
	float still = 1.0 - 0.6 * near;
	ALBEDO = face * 0.3 * still;
	ROUGHNESS = 0.4;
	METALLIC = 0.0;
	SPECULAR = 0.2;
	EMISSION = face * 0.42 * still * (1.0 - 0.3 * halo) + her_light * (her * 0.45 + rings * halo * 0.22) + her_body * halo * 0.18;
	// Its lip softens into the rim.
	ALPHA = 1.0 - smoothstep(0.94, 1.0, r);
}
