// The shape of her four gonads (her jelly's look, JellyNames.Symbols; HerColor publishes it): 0 horseshoes open toward
// the middle (Clementine's own), 1 rings, 2 clovers, 3 crescents, 4 stars. For gonad k (its middle c on the bell, its
// angle ka): the signed distance to its line, and how much of the line is drawn (open). A bell wearing a jelly's own
// look (BellView.Wear: the title's save slots) sets own_gonads instead.
global uniform int her_gonads;
uniform int own_gonads = -1;

float gonad_sd(vec2 d, vec2 c, float ka, float size, out float open) {
	open = 1.0;
	int shape = own_gonads >= 0 ? own_gonads : her_gonads;
	if (shape == 1) return length(d) - size;
	if (shape == 2) {
		float best = 1e3;
		for (int j = 0; j < 3; j++) {
			float la = ka + 3.1415927 + float(j) * 2.0943951;
			best = min(best, length(d - vec2(cos(la), sin(la)) * size * 0.55) - size * 0.5);
		}
		return best;
	}
	if (shape == 3) {
		open = 1.0 - smoothstep(-0.15, 0.3, dot(normalize(d + 1e-5), -normalize(c)));
		return length(d) - size;
	}
	if (shape == 4) {
		float aa = atan(d.y, d.x) - ka;
		return length(d) - size * (0.8 + 0.28 * cos(5.0 * aa));
	}
	open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-5), -normalize(c)));
	return length(d) - size;
}
