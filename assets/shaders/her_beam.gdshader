// Her beam at her plane (BeamView.cs): only the thin glow where its lines are stopped, tracing the edge of what it falls
// on (the beam itself is the flashlight's cone, her_beam_volume.gdshader). Its mesh is the fan's lines, each out to what
// stopped it.
//   tint       her light's colour
//   strength   how bright (0: off; the view eases it in and out)
//   half_angle the cone's half-angle (radians)
//   reach      its full reach (metres): the glow is drawn so many metres deep, whatever the line's length
//   UV.x       how far along its own line (1 at its end); UV.y 1 where something stopped it
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec3 tint : source_color = vec3(1.0, 0.82, 0.45);
uniform float strength = 0.0;
uniform float half_angle = 0.38;
uniform float soft = 0.75;
uniform float reach = 16.0;

varying vec2 local;

void vertex() {
	local = VERTEX.xz;
}

void fragment() {
	float along = length(local);
	float angle = atan(local.y, local.x);
	float across = abs(angle) / max(half_angle, 1e-3);
	float edge = 1.0 - smoothstep(soft, 1.0, across);
	// Where a line is stopped, its end glows: the edge of what it touches, as bright however far the light came.
	float to_end = (1.0 - UV.x) * along / max(UV.x, 1e-3) * reach;
	float hit = (1.0 - smoothstep(0.03, 0.22, to_end)) * smoothstep(0.5, 0.9, UV.y);
	ALBEDO = mix(tint, vec3(1.0), 0.35);
	ALPHA = clamp(strength * edge * hit * 2.2, 0.0, 1.0);
}
