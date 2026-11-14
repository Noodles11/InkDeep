// A bubble vault (PlaneVaults.cs): a fragile soap-film bubble, a thin film of rainbow sliding over it, a thin bright
// outline and a soft glow inside, all barely there from afar. Its skin wobbles as if a touch would break it. Her light
// filling it (charge 0 → 1) brightens its outline and its glow, and quickens its wobble, until it pops.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

/// How full of her light (0–1), and its own phase.
instance uniform float charge = 0.0;
instance uniform float seed = 0.0;
/// How far its skin wobbles (metres, at rest).
uniform float wobble = 0.035;

void vertex() {
	vec3 p = VERTEX;
	float t = TIME * (2.1 + 2.5 * charge) + seed;
	float w = sin(p.x * 6.3 + t * 1.7) * sin(p.y * 5.1 - t * 1.3) * sin(p.z * 7.2 + t * 1.1)
		+ 0.5 * sin(p.x * 11.0 - t * 2.3 + p.z * 4.0);
	VERTEX += NORMAL * w * wobble * (1.0 + 1.6 * charge);
}

void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float edge = 1.0 - ndv;
	// The film: thickest at its edge, its colours sliding round as it turns.
	float film = edge * 2.6 + TIME * 0.22 + seed * 0.37 + 0.15 * sin(TIME * 0.9 + seed);
	vec3 rainbow = 0.55 + 0.45 * cos(6.28318 * (film + vec3(0.0, 0.33, 0.67)));
	float sheen = pow(edge, 2.6);
	// The thin outline.
	float rim = pow(edge, 9.0);
	// The soft glow inside, from its middle.
	float glow = pow(ndv, 2.0) * (0.06 + 1.1 * charge * charge);
	vec3 colour = rainbow * sheen * (1.15 + 1.4 * charge) + vec3(1.0) * rim * (0.7 + 3.0 * charge) + vec3(1.0, 0.96, 0.86) * glow * 1.6;
	ALBEDO = colour;
	ALPHA = clamp(sheen * 0.5 + rim * (0.55 + 0.4 * charge) + glow * 0.55, 0.0, 1.0);
}
