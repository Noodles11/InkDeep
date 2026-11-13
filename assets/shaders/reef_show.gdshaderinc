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
//   her_beam_shadow where her own beam's light gets to (BeamShadow: each ray's reach past the creatures (g), a share of her_beam_frame.w; x
//                   across the cone, y up it, ±her_beam_axis.z radians), from her_beam_frame.xyz along her_beam_axis.xy
//                   (world xz; w 0: off)
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
global uniform sampler2D her_beam_shadow : filter_linear, repeat_disable;
global uniform vec4 her_beam_frame;
global uniform vec4 her_beam_axis;

// Her aura's light on a point (0..1): full round her, fading to its reach, only where it gets to.
float rs_aura(vec3 p) {
	if (her_show_aura.w <= 0.0) return 0.0;
	float d = distance(p.xz, her_show_aura.xz);
	return (1.0 - smoothstep(her_show_aura.w * 0.4, her_show_aura.w * 1.15, d)) * her_sight(p);
}

// How much of her own beam's light gets to a point of the reef (0..1): 0 in the shadow of a creature's body in its
// way, soft at the shadow's edge; 1 outside her own beam's grid (another ray's light). Her light
// on the reef is laid over it from above (rs_beam), so its shadows are too: the ray across her plane at the point's
// side, cast straight down onto it.
float rs_beam_shadow(vec3 p) {
	if (her_beam_axis.w <= 0.0) return 1.0;
	vec3 v = p - her_beam_frame.xyz;
	vec3 a = vec3(her_beam_axis.x, 0.0, her_beam_axis.y), across = vec3(-a.z, 0.0, a.x);
	float x = dot(v, a), z = dot(v, across), dist = length(vec2(x, z));
	if (x <= 0.0) return 1.0;
	float u = atan(z, x) / (2.0 * her_beam_axis.z) + 0.5;
	if (u < 0.0 || u > 1.0) return 1.0;
	float soft = 0.2 + 0.05 * dist;
	// (The creatures' shadows only: the rock's is her_sight's.)
	return smoothstep(-soft, soft, texture(her_beam_shadow, vec2(u, 0.5)).g * her_beam_frame.w - dist);
}

// Her beam's light on a point (0..1): strongest down its middle and near her, soft at the cone's sides, fading to its
// reach, cut off by the rock between and by what stands in its way (rs_beam_shadow).
float rs_beam(vec3 p) {
	if (her_beam_show.w <= 0.0 || her_show_dir.y <= 0.0) return 0.0;
	vec2 to = p.xz - her_beam_show.xz;
	float d = length(to);
	if (d >= her_beam_show.w) return 0.0;
	float angle = acos(clamp(dot(to / max(d, 1e-3), normalize(her_show_dir.xz)), -1.0, 1.0));
	float cone = d < 0.8 ? 1.0 : 1.0 - smoothstep(her_show_dir.w * 0.6, her_show_dir.w * 1.1, angle);
	// (Fading out to nothing over its last stretch, so no edge shows where it ends.)
	float fall = pow(1.0 - d / her_beam_show.w, 0.9) * (1.0 - smoothstep(her_beam_show.w * 0.55, her_beam_show.w, d));
	return cone * fall * her_show_dir.y * her_sight(p) * rs_beam_shadow(p);
}

// Where a point of the reef shows on her plane, as the eye sees it from the camera (cam): the point of her plane on the
// same line of sight. Her light lies on her plane; what lies under it (or over it) is lit as the eye sees it lit, so
// the light on the reef falls on the screen just where her beam and her glow are, not laid straight down under them
// (on the tilted view that would show as a pale shape below the beam).
vec3 rs_seen(vec3 p, vec3 cam) {
	float plane = her_show_aura.w > 0.0 ? her_show_aura.y : her_beam_show.y;
	vec3 d = p - cam;
	if (abs(d.y) < 1e-3) return p;
	return p + d * ((plane - p.y) / d.y);
}

// How much of her light falls on a point (0..1): her aura's or her beam's, where the eye sees it on her plane.
float reef_lit(vec3 p, vec3 cam) {
	if (reef_showcase <= 0.0) return 0.0;
	vec3 q = rs_seen(p, cam);
	return max(rs_aura(q), rs_beam(q));
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

// How much the reef shows as it is at a point (0..1): in her light, and by as much as it is awake: half its colour
// back while some of its leaks still pour, the whole painting once every one is sealed (the reward of cleansing it).
float reef_show(vec3 p, vec3 cam) {
	if (reef_showcase <= 0.0) return 0.0;
	float awake = reef_awake(p);
	awake *= 0.5 + 0.5 * smoothstep(0.9, 1.0, awake);
	return clamp(max(reef_lit(p, cam), max(awake, reef_wave_front(p))), 0.0, 1.0);
}

#endif
