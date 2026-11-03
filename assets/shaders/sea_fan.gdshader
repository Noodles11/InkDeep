// The Tide Fan (FanView; docs/QUESTS-PROPOSAL.md §2.2): a great old gorgonian sea fan, a lacy lattice of red-orange
// branches spreading up from its stem in a broad fan, its polyps tiny pale stars along them. It sways in the bay's
// gentle current, more toward its top. Only the lattice is drawn; the water shows through its holes.
//   UV: the upright quad (x across, y 0 at the top → 1 at its foot).
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform vec3 branch : source_color = vec3(0.86, 0.36, 0.22);
uniform vec3 polyp : source_color = vec3(1.0, 0.86, 0.72);
uniform float seed = 0.0;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// The lattice: the edges between cells of a stretched cell pattern (thin where two cells meet).
float lattice(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 c = vec2(float(x), float(y));
		vec2 o = vec2(hash(i + c + seed), hash(i + c + 17.0 + seed));
		float d = length(c + o - f);
		if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
	}
	return d2 - d1;
}

void vertex() {
	float up = 1.0 - UV.y;
	VERTEX.x += sin(TIME * 0.6 + up * 2.0 + seed) * 0.12 * up * up;
	VERTEX.z += sin(TIME * 0.45 + up * 1.6 + seed * 2.0) * 0.18 * up * up;
}

void fragment() {
	vec2 p = UV * 2.0 - vec2(1.0, 0.0);
	float up = 1.0 - UV.y;
	// The fan's outline: a broad rounded fan on a short stem.
	float shape = length(vec2(p.x * 1.05, (UV.y - 0.95) * 1.1));
	if (shape > 0.98 || (UV.y > 0.86 && abs(p.x) > 0.07)) discard;
	// The branches: a cell lattice, stretched along the way they spread out from the stem.
	vec2 q = vec2(atan(p.x, 0.95 - UV.y) * 3.4, shape * 9.0);
	float edge = lattice(q);
	float stem = 1.0 - smoothstep(0.03, 0.06, abs(p.x)) * step(0.0, UV.y - 0.6);
	float w = mix(0.05, 0.12, smoothstep(0.9, 0.2, shape));
	float lace = max(1.0 - smoothstep(w, w + 0.04, edge), stem);
	if (lace < 0.5) discard;
	// Polyps: tiny pale stars dotted along the branches.
	float dots = step(0.94, hash(floor(UV * vec2(90.0, 80.0)) + seed));
	vec3 col = mix(branch * (0.75 + 0.25 * up), polyp, dots * 0.8);
	ALBEDO = col;
	ROUGHNESS = 0.75;
	EMISSION = polyp * dots * 0.25;
}
