// A slight fisheye over the finished view (Fisheye.cs): the picture bends toward its edges, as through a dome port —
// barely in the middle, more toward the rim, the corners staying the corners (so nothing is lost off screen). Drawn over
// the whole 3D view (Clementine and her light included), under the HUD. Fisheye.Source does the same sums for the mouse.
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform float strength = 0.08;
// The crossover's ink look (docs/proposals/CROSSOVER.md §12): the paper's grain over the whole picture, her included (0 none):
// a fine tooth, fibres, and the faint unevenness of a sheet, fixed to the screen as paper is.
uniform float grain = 0.0;

float g_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float g_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(g_hash(i), g_hash(i + vec2(1.0, 0.0)), u.x), mix(g_hash(i + vec2(0.0, 1.0)), g_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 size = 1.0 / SCREEN_PIXEL_SIZE;
	vec2 a = vec2(size.x / size.y, 1.0);
	vec2 r = (SCREEN_UV - 0.5) * a;
	float rmax2 = dot(a * 0.5, a * 0.5);
	float r2 = dot(r, r);
	// Quartic: it bends mostly toward the rim.
	vec2 src = r * (1.0 + strength * r2 * r2) / (1.0 + strength * rmax2 * rmax2);
	vec3 c = textureLod(screen, src / a + 0.5, 0.0).rgb;
	if (grain > 0.0) {
		vec2 px = FRAGCOORD.xy;
		float tooth = g_hash(floor(px * 0.5));
		float fibre = g_noise(vec2(px.x * 0.05, px.y * 0.9)) * 0.5 + g_noise(vec2(px.x * 0.8, px.y * 0.04) + 11.0) * 0.5;
		float sheet = g_noise(px * 0.004) * 0.6 + g_noise(px * 0.015 + 9.0) * 0.4;
		c *= 1.0 - grain * (0.55 * (1.0 - tooth) + 0.3 * fibre + 0.4 * (1.0 - sheet));
		// (And a breath of paper's own light in the darks, as ink never quite covers it.)
		c += grain * 0.12 * vec3(0.55, 0.53, 0.5) * tooth * (1.0 - smoothstep(0.0, 0.2, dot(c, vec3(0.33))));
	}
	COLOR = vec4(c, 1.0);
}
