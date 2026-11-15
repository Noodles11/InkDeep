// The title's opening (TitleMain.StepReveal, docs/TITLE-MENU.md): black over the menu, but for a hole in the gonad
// loader's shape (gonad_px its half-size on the screen) and a round opening (open_px) that both grow from the screen's
// middle until nothing is covered; a white glow (glow_px wide, glow its strength) rings the opening as it spreads.
shader_type canvas_item;

uniform sampler2D gonad : filter_linear, repeat_disable;
uniform vec2 centre = vec2(0.0);
uniform float gonad_px = 0.0;
uniform float open_px = 0.0;
uniform float glow_px = 40.0;
uniform float glow = 0.0;
// The opening still white (the loader's white, fading as the opening grows).
uniform float fill = 0.0;

varying vec2 at;

void vertex() {
	at = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 d = at - centre;
	float hole = 0.0;
	if (gonad_px > 0.0) {
		vec2 uv = d / (2.0 * gonad_px) + 0.5;
		if (uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0) hole = texture(gonad, uv).a;
	}
	float r = length(d);
	hole = max(hole, 1.0 - smoothstep(open_px - 2.0, open_px, r));
	// The glow: brightest at the opening's rim, fading out over the black (and a little into the opening).
	float out_ = max(r - open_px, 0.0);
	float halo = glow * exp(-out_ / max(glow_px, 1.0)) * (1.0 - 0.6 * hole);
	// Over the black: black lit by the glow; over the opening: the glow alone, white over the menu.
	float white_a = clamp(halo + hole * fill, 0.0, 1.0);
	float a = clamp((1.0 - hole) + white_a, 0.0, 1.0);
	COLOR = vec4(vec3(1.0, 0.98, 0.94) * white_a / max(a, 1e-3), a);
}
