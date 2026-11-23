// A title menu item dissolving as she chooses it (TitleMain.StepChoose): from the side her beam strikes it, outward
// along the beam (dir), a ragged front (front, in screen pixels along dir from origin) eats its text, burning bright at
// its edge (burn). Before the front starts (front far below) it is drawn as it is. Also its whitening (light): the item
// in her beam.
shader_type canvas_item;

uniform vec2 origin = vec2(0.0);
uniform vec2 dir = vec2(1.0, 0.0);
uniform float front = -100000.0;
uniform float ragged = 26.0;
uniform vec3 burn : source_color = vec3(1.0, 0.9, 0.65);

varying vec2 at;

void vertex() {
	at = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}

void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float along = dot(at - origin, dir) + (noise(at * 0.09) * 0.7 + noise(at * 0.31) * 0.3 - 0.5) * 2.0 * ragged;
	float past = front - along;
	if (past > 0.0) discard;
	float edge = 1.0 - smoothstep(0.0, 9.0, -past);
	c.rgb = mix(c.rgb, burn * 1.6, edge * step(-100000.0 + 1.0, front));
	COLOR = c;
}
