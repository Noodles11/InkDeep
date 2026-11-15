// A bubble vault (PlaneVaults.cs): a soap bubble. Its film shows the colours of a soap film all over (thin-film
// interference: magenta, gold, green and blue bands by the film's thickness), the film swirling slowly as it drains
// toward the bottom, where it is thicker; strongest at the glancing edge, faint across the face so the bubble stays
// clear. Two bright window highlights sit on it (the light from above and a smaller one opposite), coloured at their
// fringes; a thin bright outline and a soft glow inside. Its skin wobbles as if a touch would break it. Her light
// filling it (charge 0 → 1) brightens its outline, its colours and its glow, and quickens its wobble, until it pops.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

/// How full of her light (0–1), and its own phase.
instance uniform float charge = 0.0;
instance uniform float seed = 0.0;
/// How far its skin wobbles (metres, at rest).
uniform float wobble = 0.035;

varying vec3 local;

void vertex() {
	vec3 p = VERTEX;
	local = normalize(VERTEX);
	float t = TIME * (2.1 + 2.5 * charge) + seed;
	float w = sin(p.x * 6.3 + t * 1.7) * sin(p.y * 5.1 - t * 1.3) * sin(p.z * 7.2 + t * 1.1)
		+ 0.5 * sin(p.x * 11.0 - t * 2.3 + p.z * 4.0);
	VERTEX += NORMAL * w * wobble * (1.0 + 1.6 * charge);
}

// Smooth value noise in 3D.
float h3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float n3(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(h3(i), h3(i + vec3(1, 0, 0)), f.x), mix(h3(i + vec3(0, 1, 0)), h3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(h3(i + vec3(0, 0, 1)), h3(i + vec3(1, 0, 1)), f.x), mix(h3(i + vec3(0, 1, 1)), h3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

// A soap film's colour by its thickness (0–1 over a few orders of interference): the bands of magenta, gold, green and
// blue a film shows, paler where it is thick.
vec3 film_colour(float d) {
	vec3 c = 0.5 + 0.5 * cos(6.28318 * (d * vec3(2.6, 3.1, 3.7) + vec3(0.0, 0.18, 0.42)));
	return mix(c, vec3(0.8), smoothstep(0.7, 1.2, d) * 0.5);
}

void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float edge = 1.0 - ndv;
	float t = TIME * 0.18 + seed * 1.7;
	// The film's thickness over its surface: swirling slowly, and draining toward the bottom.
	vec3 q = local * 2.2;
	q += 0.6 * vec3(n3(q + vec3(t, 0.0, 0.0)), n3(q + vec3(0.0, t * 1.3, 4.1)), n3(q + vec3(2.7, 0.0, t)));
	float thick = 0.35 * n3(q * 1.3 + vec3(0.0, -t, 0.0)) + 0.18 * n3(q * 3.1 - vec3(t)) + 0.35 * (0.5 - 0.5 * local.y);
	// Seen at a slant, the light runs further through the film: its colours shift.
	thick += edge * 0.35;
	vec3 film = film_colour(thick);
	// How much the film shows: strong at its glancing edge, faint across its face.
	float sheen = 0.3 + 0.7 * pow(edge, 1.5);
	// The window highlights, as the eye sees them: one up on its left, a smaller one low on its right.
	vec3 key = normalize(vec3(-0.75, 0.75, 0.55));
	vec3 back = normalize(vec3(0.7, -0.65, 0.6));
	vec3 r = reflect(-VIEW, NORMAL);
	float spot = max(dot(r, key), 0.0), spot2 = max(dot(r, back), 0.0);
	float hi = smoothstep(0.9, 0.96, spot) + 0.6 * smoothstep(0.95, 0.985, spot2);
	// (Its fringe coloured by the film.)
	float fringe = smoothstep(0.8, 0.9, spot) * (1.0 - smoothstep(0.9, 0.96, spot));
	// The thin outline.
	float rim = pow(edge, 9.0);
	// The soft glow inside, from its middle.
	float glow = pow(ndv, 2.0) * (0.06 + 1.1 * charge * charge);
	// (Its colours full, as a soap film's are.)
	film = clamp(mix(vec3(dot(film, vec3(0.333))), film, 1.5), 0.0, 1.0);
	vec3 colour = film * sheen * (1.25 + 1.4 * charge) + film * fringe * 1.3 + vec3(1.0) * hi * 1.4
		+ vec3(1.0) * rim * (0.6 + 3.0 * charge) + vec3(1.0, 0.96, 0.86) * glow * 1.6;
	ALBEDO = colour;
	ALPHA = clamp(sheen * 0.5 + fringe * 0.4 + hi * 0.9 + rim * (0.55 + 0.4 * charge) + glow * 0.55, 0.0, 1.0);
}
