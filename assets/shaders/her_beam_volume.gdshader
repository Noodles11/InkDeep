// Her beam as a flashlight's in the water (BeamView, BeamShadow): light scattered by the water itself, marched along
// each pixel's line of sight through the cone. At each step the water there glows by as much light as gets to it:
// none where the ray of her light to it was stopped first (shadow: the reach of each ray of a grid over the cone, so
// every creature casts its own round shadow down the beam, and rock and the seabed stop it), brightest down the cone's
// middle and soft to nothing at its rim, dimming with the distance as light does (spreading out, and taken up by the
// water), and a little brighter seen against the way it travels (the water scatters it mostly forward). Tiny motes of
// plankton drift in it, sparkling only where her light reaches them. The light on what it lands on is the spotlight's
// and the reef's own (rs_beam), shadowed alike. The march stops at the reef in front (the depth), so nothing behind
// rock shows and nothing has a hard edge.
//   tint       her light's colour
//   strength   how bright (0: off; the view eases it in and out)
//   origin     her bell (world), reach the beam's full reach (m)
//   axis       its way (world, level), half_angle the cone's (radians), span the shadow grid's half-width (radians)
//   shadow     each ray's reach as a share of the full reach (r): x across the cone (−span..span), y up it
//   The mesh is the cone's hull (BeamView.LayHull), its normals outward: drawn only on its far side, never depth-tested.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
uniform float strength = 0.0;
uniform vec3 origin = vec3(0.0);
uniform vec3 axis = vec3(1.0, 0.0, 0.0);
uniform float half_angle = 0.38;
uniform float span = 0.41;
uniform float reach = 16.0;
uniform sampler2D shadow : filter_linear, repeat_disable, hint_default_white;
// How thick the water's glow is per metre of lit water, how much of the light the water takes up per metre, and how
// forward it scatters (Henyey–Greenstein g).
uniform float density = 0.09;
uniform float absorb = 0.05;
uniform float forward = 0.35;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;

const int STEPS = 28;

varying vec3 v_world;
varying vec3 v_out;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_out = mat3(MODEL_MATRIX) * NORMAL;
}

float bv_hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

// How much of her light gets to a point (v: from her bell), 0..1: inside the cone, softly to its rim, and not shaded.
float light_at(vec3 v, vec3 across, out float dist) {
	dist = length(v);
	if (dist <= 1e-3 || dist >= reach) return 0.0;
	float x = dot(v, axis), z = dot(v, across);
	float angle = acos(clamp(x / dist, -1.0, 1.0));
	if (angle >= half_angle) return 0.0;
	// A flashlight's spot: a hot middle, falling away softly to nothing at its rim.
	float spot = 1.0 - smoothstep(half_angle * 0.25, half_angle, angle);
	spot *= 0.7 + 0.3 * spot;
	// The shadow: this point's ray in the grid, a few around it (a soft edge, the light a little more than a point).
	float az = atan(z, x), el = atan(v.y, length(vec2(x, z)));
	vec2 uv = vec2(az, el) / (2.0 * span) + 0.5;
	// (The grid's texels blend, so the shadow's edge is soft, the softer the farther.)
	float soft = 0.15 + 0.05 * dist;
	float lit = smoothstep(-soft, soft, texture(shadow, uv).r * reach - dist);
	// Light spreads out and the water takes it up; not over her own bell.
	float fall = exp(-absorb * dist) / (1.0 + 0.012 * dist * dist);
	float near = smoothstep(0.15, 0.9, dist);
	// (And it runs out at its reach, fading over the far half of it, so no edge shows where it ends.)
	float out_ = 1.0 - smoothstep(reach * 0.5, reach, dist);
	out_ *= out_;
	return spot * lit * fall * near * out_;
}

void fragment() {
	vec3 cam = CAMERA_POSITION_WORLD;
	vec3 to_back = v_world - cam;
	float t_back = length(to_back);
	vec3 d = to_back / t_back;
	// (Once per pixel: from its far side only.)
	if (dot(v_out, d) <= 0.0) discard;
	// The reef in front: the march ends there.
	float scene = texture(depth_tex, SCREEN_UV).r;
	vec4 at = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene, 1.0);
	float t_scene = length(at.xyz / at.w);
	float t_end = min(t_back, t_scene);
	// Where the line of sight comes into the cone (its side; else into its reach, through its far end).
	vec3 co = cam - origin;
	float k = cos(half_angle);
	k *= k;
	float da = dot(d, axis), ca = dot(co, axis);
	float a = da * da - k, b = 2.0 * (da * ca - k * dot(d, co)), c = ca * ca - k * dot(co, co);
	float t_in = -1.0;
	float disc = b * b - 4.0 * a * c;
	if (abs(a) > 1e-5 && disc >= 0.0) {
		float r = sqrt(disc);
		float t1 = (-b - r) / (2.0 * a), t2 = (-b + r) / (2.0 * a);
		float lo = min(t1, t2), hi = max(t1, t2);
		if (lo > 0.0 && dot(co + d * lo, axis) > 0.0 && lo < t_back - 0.01) t_in = lo;
		else if (hi > 0.0 && dot(co + d * hi, axis) > 0.0 && hi < t_back - 0.01) t_in = hi;
	}
	if (t_in < 0.0) {
		float bs = dot(co, d), cs = dot(co, co) - reach * reach;
		float ds = bs * bs - cs;
		t_in = ds > 0.0 ? max(-bs - sqrt(ds), 0.0) : t_back;
	}
	if (t_end <= t_in) discard;
	vec3 across = vec3(-axis.z, 0.0, axis.x);
	float step_len = (t_end - t_in) / float(STEPS);
	// Each pixel starts a little differently along its line (no banding).
	float jitter = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))) + TIME * 0.618);
	float g = forward, g2 = g * g;
	float glow = 0.0, sparkle = 0.0;
	vec3 drift = vec3(TIME * 0.11, TIME * 0.04, TIME * 0.07);
	for (int i = 0; i < STEPS; i++) {
		float t = t_in + (float(i) + jitter) * step_len;
		vec3 p = cam + d * t;
		vec3 v = p - origin;
		float dist;
		float l = light_at(v, across, dist);
		if (l <= 0.0) continue;
		// Seen against the way the light goes, the water scatters more of it to the eye.
		float cos_t = dot(v / dist, -d);
		float phase = (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cos_t, 1.5);
		glow += l * phase * step_len;
		// Plankton: a mote in this step's cell of the water, if near the line of sight, catching the light.
		vec3 cell_at = (p + drift) * 3.2;
		vec3 cell = floor(cell_at);
		float seed = bv_hash(cell);
		if (seed > 0.95) {
			vec3 mote = (cell + 0.5 + (vec3(bv_hash(cell + 3.1), bv_hash(cell + 7.7), bv_hash(cell + 1.3)) - 0.5) * 0.7) / 3.2 - drift;
			float off = length(cross(mote - cam, d));
			float twinkle = 0.5 + 0.5 * sin(TIME * (2.0 + 3.0 * seed) + seed * 50.0);
			sparkle += l * twinkle * (1.0 - smoothstep(0.012, 0.03, off));
		}
	}
	float haze = 1.0 - exp(-density * glow);
	ALBEDO = mix(tint, vec3(1.0), clamp(sparkle * 0.6, 0.0, 1.0));
	ALPHA = clamp(strength * (haze + sparkle * 0.8), 0.0, 1.0);
}
