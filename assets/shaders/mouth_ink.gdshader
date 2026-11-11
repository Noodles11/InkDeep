// A dangerous cave mouth weeping the Leak's ink (PortalView, docs/DIVE.md §6): the more dangerous its cave, the
// more. Standing, a sheet over the mouth's face: dark violet runs dripping down the rock round its arch, slowly,
// thickest at the top. Lying, a stain on the floor before its threshold where the runs have pooled, ragged, fading out.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// 0..1: how much it weeps (its danger's share). Standing (1) or lying (0).
uniform float amount = 0.5;
uniform float standing = 1.0;
uniform float seed = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 uv = UV;
	float ink;
	if (standing > 0.5) {
		// Runs down the face: narrow columns, each its own length (more of them, and longer, the more it weeps), a bead at
		// each one's foot creeping down and back.
		float cols = 22.0;
		float x = uv.x * cols;
		float col = floor(x);
		float h = hash(vec2(col, seed));
		float run = (0.25 + 0.75 * h) * (0.35 + 0.65 * amount);
		float w = 1.0 - smoothstep(0.08, 0.32 + 0.12 * hash(vec2(col, 7.0)), abs(fract(x) - 0.5));
		float down = uv.y;
		float bead = run + 0.03 * sin(TIME * (0.4 + h) + h * 9.0);
		ink = w * step(down, bead) * step(hash(vec2(col, 3.0 + seed)), 0.35 + 0.6 * amount);
		ink = max(ink, (1.0 - smoothstep(0.0, 0.03, abs(down - bead))) * w * 1.4 * step(hash(vec2(col, 3.0 + seed)), 0.35 + 0.6 * amount));
		// A wash of it over the top of the face.
		ink = max(ink, (1.0 - smoothstep(0.0, 0.18, down)) * (0.5 + 0.5 * noise(vec2(uv.x * 9.0, seed))) * amount);
		// Fading out to the sides.
		ink *= 1.0 - smoothstep(0.38, 0.5, abs(uv.x - 0.5));
	} else {
		// A pool on the floor before the threshold (uv.y 0 at the face): ragged, thickest by the face.
		vec2 c = (uv - vec2(0.5, 0.0)) * vec2(2.0, 1.0);
		float r = length(c * vec2(1.0, 1.4));
		float edge = 0.55 + 0.45 * amount + 0.25 * (noise(uv * 7.0 + seed) - 0.5);
		ink = (1.0 - smoothstep(edge - 0.25, edge, r)) * (0.55 + 0.45 * noise(uv * 15.0 + seed * 2.0));
	}
	ALBEDO = mix(vec3(0.03, 0.01, 0.06), vec3(0.22, 0.08, 0.36), 0.25 + 0.2 * sin(TIME * 0.6 + uv.x * 5.0));
	ALPHA = clamp(ink, 0.0, 1.0) * (0.55 + 0.35 * amount);
}
