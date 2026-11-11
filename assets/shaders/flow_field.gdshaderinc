// The water's motion round her (FlowField, docs/proposals/REEF-REDO-PROPOSAL.md §5): a small texture that follows the camera,
// holding how fast the water moves at each point of her plane (metres a second, x and z). Her swimming, her jets and
// her dashes stir it, and so do the creatures; it fades and spreads. Soft things read it and bend away.
//   flow_rect: the texture's corner (world x, z), its size (metres), and whether it is on (w > 0).
global uniform sampler2D flow_tex : filter_linear, repeat_disable;
global uniform vec4 flow_rect;

// The water's velocity at a world point of her plane (x, z), fading out toward the texture's edge.
vec2 flow_at(vec2 xz) {
	if (flow_rect.w <= 0.0) return vec2(0.0);
	vec2 uv = (xz - flow_rect.xy) / flow_rect.z;
	float edge = smoothstep(0.0, 0.08, min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));
	return texture(flow_tex, clamp(uv, 0.0, 1.0)).rg * edge;
}

// How far a soft thing bends in the water's motion (metres, x and z), its freedom w (0 base → 1 tip), at this depth
// under her plane (the stir fades away below her).
vec2 flow_bend(vec2 xz, float w, float below) {
	vec2 f = flow_at(xz);
	float k = 1.0 - smoothstep(1.0, 7.0, below);
	vec2 b = f * 0.22 * w * k;
	float l = length(b);
	return l > 0.9 ? b / l * 0.9 : b;
}
