// Her beam in the water (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.2; BeamView.cs): a rave laser's fan of her light from her
// bell along her aim, laid flat over her plane. Its mesh is the fan's lines, each out to what stopped it (BeamView casts
// them), so it ends on the edge of what it falls on. Thin bright lines fan out from her through a soft haze, each
// flickering on its own beat; bright at her and dimming as they go (as the sim's falloff); full across the middle of the
// cone and fading at its sides; and where a line is stopped its end glows, tracing the edge of what it touches.
//   tint       her light's colour
//   strength   how bright (0: off; the view eases it in and out)
//   half_angle the cone's half-angle (radians)
//   line_step  the angle between two of its laser lines (radians)
//   UV.x       how far along its own line (1 at its end); UV.y 1 where something stopped it
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
uniform float strength = 0.0;
uniform float half_angle = 0.38;
uniform float line_step = 0.03;
uniform float soft = 0.75;
/// Its full reach (metres): the glow at a stopped line's end is drawn so many metres deep, whatever the line's length.
uniform float reach = 16.0;

varying vec2 local;

void vertex() {
	local = VERTEX.xz;
}

float bh_hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

float bh_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = fract(sin(dot(i, vec2(127.1, 311.7))) * 43758.5453);
	float b = fract(sin(dot(i + vec2(1.0, 0.0), vec2(127.1, 311.7))) * 43758.5453);
	float c = fract(sin(dot(i + vec2(0.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
	float d = fract(sin(dot(i + vec2(1.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	float along = length(local);
	float angle = atan(local.y, local.x);
	float across = abs(angle) / max(half_angle, 1e-3);
	float fall = pow(max(1.0 - along, 0.0), 1.3);
	float edge = 1.0 - smoothstep(soft, 1.0, across);
	// Not over her own bell: it begins a little out from her heart.
	float near = smoothstep(0.0, 0.04, along);
	// The laser lines: thin, sharp, evenly fanned from her, each flickering on its own beat.
	float u = angle / line_step;
	float t = abs(fract(u) - 0.5) * 2.0;
	float w = clamp(fwidth(u) * 1.5, 0.04, 0.9);
	float line = 1.0 - smoothstep(w * 0.5, w, t);
	float id = floor(u);
	float flick = 0.65 + 0.35 * sin(TIME * (5.0 + 4.0 * bh_hash(id)) + bh_hash(id + 7.0) * 40.0);
	// The haze the lines pass through, a slow shimmer running down it.
	float haze = 0.3 * (0.8 + 0.2 * bh_noise(vec2(along * 14.0 - TIME * 1.6, angle * 9.0 + TIME * 0.3)));
	// Where a line is stopped, its end glows: the edge of what it touches.
	float to_end = (1.0 - UV.x) * along / max(UV.x, 1e-3) * reach;
	float hit = (1.0 - smoothstep(0.04, 0.3, to_end)) * smoothstep(0.5, 0.9, UV.y);
	// Motes of the water's plankton lit where they cross it, twinkling as they turn.
	vec2 cell_at = local * 46.0 + vec2(TIME * 0.35, TIME * 0.12);
	vec2 cell = floor(cell_at), in_cell = fract(cell_at) - 0.5;
	float seed = fract(sin(dot(cell, vec2(41.3, 289.1))) * 43758.5453);
	vec2 spot = vec2(fract(seed * 7.13), fract(seed * 13.7)) - 0.5;
	float mote = step(0.86, seed) * (1.0 - smoothstep(0.04, 0.12, length(in_cell - spot * 0.6)));
	mote *= 0.6 + 0.4 * sin(TIME * 3.0 + seed * 40.0);
	float lines = line * flick;
	ALBEDO = mix(tint, vec3(1.0), clamp(lines * 0.35 + hit * 0.3, 0.0, 1.0)) + vec3(mote * 0.6);
	ALPHA = clamp(strength * edge * near * (fall * (haze + lines * 1.7) + hit * (0.45 + 0.55 * fall) + mote * 2.2 * (0.4 + 0.6 * fall)), 0.0, 1.0);
}
