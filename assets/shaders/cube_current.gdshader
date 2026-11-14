// A current in the cube world (docs/proposals/CUBE-WORLD-PLAN.md §8): faint streaks of brighter water running the way
// it flows, laid flat on her plane. The frontier drift is slow and faint; the wake current pouring out of a shut edge
// fast and plain. Lanes and streaks are fixed to the world, so the water runs past and the quad itself never shows.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

/// The flow (metres a second, on the plane: x east, y south) and how plainly it shows (0–1).
instance uniform vec2 flow = vec2(1.0, 0.0);
instance uniform float strength = 1.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

void fragment() {
	float speed = length(flow);
	vec2 d = speed > 1e-4 ? flow / speed : vec2(1.0, 0.0);
	vec2 side = vec2(-d.y, d.x);
	vec2 p = world_pos.xz;
	float across = dot(p, side) * 1.6;
	float lane = floor(across);
	// Each lane its own pace and phase; a streak a few metres long in each, running with the flow.
	float along = dot(p, d) * 0.22 - TIME * (0.25 + speed * 0.22) * (0.8 + 0.4 * hash(lane + 3.0)) + hash(lane);
	float streak = smoothstep(0.82, 0.97, fract(along)) * smoothstep(1.0, 0.97, fract(along));
	float thin = 1.0 - smoothstep(0.08, 0.32, abs(fract(across) - 0.5));
	// Soft at the quad's edges.
	float edge = smoothstep(0.0, 0.3, UV.x) * smoothstep(1.0, 0.7, UV.x) * smoothstep(0.0, 0.3, UV.y) * smoothstep(1.0, 0.7, UV.y);
	ALBEDO = vec3(0.55, 0.82, 1.0);
	ALPHA = clamp(streak * thin * edge * strength, 0.0, 1.0) * 0.5;
}
