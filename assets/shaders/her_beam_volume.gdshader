// Her beam as a flashlight's (BeamView.LayVolume): a round cone of her light from her bell along her aim, reaching up
// and down from her plane as well as either side, each way only as far as the line of her light that way gets (rock or
// a body stops it). Its sides are a faint haze, thicker where the eye looks through more of it, bright at her and
// fading out along it; inside it tiny motes of the water's plankton drift and twinkle where the light catches them. Its
// far end glows softly where the light lands on what stopped it, as bright however far it came (the light on the reef
// does not dim with the distance), and every end melts away without a hard edge.
//   tint      her light's colour
//   strength  how bright (0: off; the view eases it in and out)
//   UV.x      how far along her aim (a share of its full reach); UV.y how far along its own way (1 at its end)
//   COLOR.g   1 where that way was stopped (its end glows there)
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
uniform float strength = 0.0;
// (Where the cone goes into rock it fades out over this many metres before it: no hard line where they meet.)
uniform float soft_meet = 2.5;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;

varying vec3 v_world;
varying vec3 v_normal;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float bv_hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

void fragment() {
	float s = clamp(UV.x, 0.0, 1.0), u = UV.y;
	float fall = pow(1.0 - s, 1.2);
	// Not over her own bell: it begins a little out from her heart.
	float near = smoothstep(0.0, 0.05, s);
	float stopped = COLOR.g;
	// The haze: brightest down its middle (the eye looks through most of it there), soft to nothing at its sides.
	vec3 view = normalize(CAMERA_POSITION_WORLD - v_world);
	float facing = abs(dot(normalize(v_normal), view));
	float haze = 0.055 * pow(facing, 1.5) * fall * near;
	// Tiny motes of plankton drifting in it, each twinkling as it turns.
	vec3 drift = vec3(TIME * 0.11, TIME * 0.04, TIME * 0.07);
	vec3 cell_at = (v_world + drift) * 3.2;
	vec3 cell = floor(cell_at), in_cell = fract(cell_at) - 0.5;
	float seed = bv_hash(cell);
	vec3 spot = vec3(bv_hash(cell + 3.1), bv_hash(cell + 7.7), bv_hash(cell + 1.3)) - 0.5;
	float mote = step(0.8, seed) * (1.0 - smoothstep(0.05, 0.13, length(in_cell - spot * 0.7)));
	mote *= (0.5 + 0.5 * sin(TIME * (2.0 + 3.0 * seed) + seed * 50.0)) * (0.35 + 0.65 * fall) * near * pow(facing, 0.7);
	// Its far end where the light lands on something: as bright however far it came (no falloff there), a soft glow
	// swelling toward it and melting away at its very edge (no hard rim anywhere it ends).
	float land = stopped * smoothstep(0.5, 0.9, u) * (1.0 - smoothstep(0.9, 1.0, u)) * 0.12 * facing;
	float ends = 1.0 - smoothstep(0.82, 1.0, u);
	haze *= ends;
	mote *= ends;
	ALBEDO = mix(tint, vec3(1.0), clamp(mote * 0.6, 0.0, 1.0));
	float scene = texture(depth_tex, SCREEN_UV).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene, 1.0);
	float meet = smoothstep(0.0, soft_meet, -behind.z / behind.w + VERTEX.z);
	ALPHA = clamp(strength * (haze + mote * 2.6 + land) * meet, 0.0, 1.0);
}
