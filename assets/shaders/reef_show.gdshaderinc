// The showcase reef (docs/VISUAL-SHOWCASE.md): her light shows the reef.
//
// Outside her light the reef is the drawn look's dim, drained silhouettes. Where her light falls — her aura round her,
// her beam along its cone, both cut off by the rock between (her_sight, light_field.gdshaderinc) — the reef shows as
// it is: its rock, its crust and its sand in their own colours, lit from where she is. So she paints the reef with her
// light as she swims and looks about.
//
// The reef wakes as she seals its leaks (reef_wake, 0 none to 1 all): a share of that showing stays everywhere, the
// more the more leaks are sealed. Each sealed leak sends a wave of her light out across the reef from where it was
// (reef_wave), and the reef behind the wave's front is as awake as it will be once the wave has passed.
//
//   reef_showcase   1 on a showcase reef, 0 elsewhere (every function here is 0 then)
//   her_show_aura   her aura (world xyz) and its reach (w; 0 none)
//   her_beam_show   her beam's origin (world xyz) and its full reach (w; 0 off)
//   her_show_dir    its direction (x, z in xz), how far it has eased on (y, 0..1), its half-angle (w, radians)
//   her_show_light  her light's colour
//   reef_wake       how awake the reef is (0..1)
//   reef_wave       a wave's middle (xy: world xz), its front's radius (z; 0 none) and how awake the reef is behind it (w)
#ifndef REEF_SHOW_INC
#define REEF_SHOW_INC

#include "res://assets/shaders/light_field.gdshaderinc"

global uniform float reef_showcase;
global uniform vec4 her_show_aura;
global uniform vec4 her_beam_show;
global uniform vec4 her_show_dir;
global uniform vec3 her_show_light;
global uniform float reef_wake;
global uniform vec4 reef_wave;

// Her aura's light on a point (0..1): full round her, fading to its reach, only where it gets to.
float rs_aura(vec3 p) {
	if (her_show_aura.w <= 0.0) return 0.0;
	float d = distance(p.xz, her_show_aura.xz);
	return (1.0 - smoothstep(her_show_aura.w * 0.4, her_show_aura.w * 1.15, d)) * her_sight(p);
}

// Her beam's light on a point (0..1): strongest down its middle and near her, soft at the cone's sides, fading to its
// reach, cut off by the rock between.
float rs_beam(vec3 p) {
	if (her_beam_show.w <= 0.0 || her_show_dir.y <= 0.0) return 0.0;
	vec2 to = p.xz - her_beam_show.xz;
	float d = length(to);
	if (d >= her_beam_show.w) return 0.0;
	float angle = acos(clamp(dot(to / max(d, 1e-3), normalize(her_show_dir.xz)), -1.0, 1.0));
	float cone = d < 0.8 ? 1.0 : 1.0 - smoothstep(her_show_dir.w * 0.6, her_show_dir.w * 1.1, angle);
	float fall = pow(1.0 - d / her_beam_show.w, 0.9);
	return cone * fall * her_show_dir.y * her_sight(p);
}

// How much of her light falls on a point (0..1): her aura's or her beam's.
float reef_lit(vec3 p) {
	if (reef_showcase <= 0.0) return 0.0;
	return max(rs_aura(p), rs_beam(p));
}

// How awake the reef is at a point (0..1): behind a wave's front, as awake as the wave leaves it.
float reef_awake(vec3 p) {
	if (reef_showcase <= 0.0) return 0.0;
	float w = reef_wake;
	if (reef_wave.z > 0.0 && distance(p.xz, reef_wave.xy) < reef_wave.z) w = max(w, reef_wave.w);
	return w;
}

// A wave's front over a point (0..1): a band of her light a few metres deep behind its edge, fading as it runs out.
float reef_wave_front(vec3 p) {
	if (reef_showcase <= 0.0 || reef_wave.z <= 0.0) return 0.0;
	float d = distance(p.xz, reef_wave.xy);
	float band = smoothstep(reef_wave.z - 7.0, reef_wave.z - 0.8, d) * (1.0 - smoothstep(reef_wave.z - 0.8, reef_wave.z, d));
	return band * (1.0 - smoothstep(70.0, 170.0, reef_wave.z));
}

// How much the reef shows as it is at a point (0..1): in her light, and by as much as it is awake.
float reef_show(vec3 p) {
	if (reef_showcase <= 0.0) return 0.0;
	return clamp(max(reef_lit(p), max(reef_awake(p) * 0.5, reef_wave_front(p))), 0.0, 1.0);
}

#endif
