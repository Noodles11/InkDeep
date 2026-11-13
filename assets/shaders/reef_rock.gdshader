// Free-standing reef rock in the reef's skin (reef_surface.gdshaderinc): arches, overhangs, cave roofs, boulders and
// the rubble of a secret's seal. Lumped along its normal so primitive shapes read as rock, and dissolved with an ordered dither while
// Clementine is underneath (THEME-BIBLE §6.1: canopy dissolves in a dithered cutout), and carved away round her where it
// stands between the camera and her (level_frame's lf_see_cut). Dithering keeps the rock opaque
// in the depth buffer, so the depth focus blurs it like any other high rock.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

/// 1 solid, 0 gone.
uniform float fade = 1.0;
/// How far the surface is lumped in or out along its normal (metres).
uniform float lumps = 0.0;
/// A colour wash.
uniform vec3 tint : source_color = vec3(1.0);

varying vec3 world;
varying vec3 world_normal;

void vertex() {
	vec3 wp = lf_pattern(lf_local((MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz));
	if (lumps > 0.0) VERTEX += NORMAL * (rs_fbm(wp * 0.45) - 0.5) * 2.0 * lumps;
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}


// An art direction under study (reef_art.gdshaderinc): the reef painted another way.
void art(inout ReefSkin s, vec3 local, float hw, float px) {
	if (reef_art <= 0) return;
	vec3 w = lf_pattern(local);
	s.albedo = art_paint(s.albedo, w, local, s.normal, reef_water_deep, reef_rock, reef_sand);
	s.albedo = mix(s.albedo, s.albedo * 0.4 + vec3(0.85, 0.82, 0.74) * 0.6, art_rim(local, s.normal, hw));
	vec2 ink = art_ink(w, local, s.normal, hw, px);
	s.albedo = mix(s.albedo, art_beyond(reef_water_deep), ink.y);
	s.albedo = mix(s.albedo, art_ink_colour(reef_water_deep), ink.x);
	s.caustic = art_caustic(s.caustic);
}

void fragment() {
	if (fade < 0.999 && lf_dither(FRAGCOORD.xy) >= fade) discard;
	if (lf_portal_cut(world, FRAGCOORD.xy) || !IN_SHADOW_PASS && lf_see_cut(world, CAMERA_POSITION_WORLD, FRAGCOORD.xy)) discard;
	vec3 local = lf_local(world);
	float px = length(fwidth(world.xz));
	ReefSkin s = reef_surface(local, normalize(world_normal), TIME, px);
	ReefSkin nat = s;
	if (reef_art == 7) s.normal = normalize(world_normal);
	art(s, local, fwidth(local.y), px);
	// Watercolour (look 7): the simple wash is the reef as her light shows it (nat): no fine relief, no glow of its
	// own. All that lies under the walls' line is the water's dark wash. Out of her light the reef is held near the
	// water's tone (s), so reef and water part only faintly until she lights them.
	if (reef_art == 7) {
		vec3 w7 = lf_pattern(local);
		float wk = art_water(local);
		vec3 wash = art_wash(w7, reef_water_deep, reef_murk);
		s.rough = 0.95;
		nat = s;
		nat.albedo = mix(s.albedo, wash * 0.3, wk);
		s.albedo = mix(mix(wash * 1.5, s.albedo, 0.16) * 0.4, wash * 0.3, wk);
		s.emission = mix(mix(wash * 1.5, nat.albedo, 0.16) * 0.55, wash * 0.8, wk);
		nat.emission = s.emission;
	}
	float show = reef_show_skin(s, nat, world, CAMERA_POSITION_WORLD);
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	// (And how much the showcase shows it as it is, in .y: lit plainly there, not in the drawn look's steps.)
	BACKLIGHT = vec3(s.caustic * 1.5, reef_art == 7 ? 0.0 : show, 0.0);
	vec3 c = s.albedo * tint;
	ALBEDO = c;
	EMISSION = s.emission + LF_SEE_RIM * lf_see_rim(world, CAMERA_POSITION_WORLD) * 0.35;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
}

void light() {
	float plain = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float ndl = mix(art_light(plain), plain, BACKLIGHT.y);
	// The sun also brings the caustics; other lights (her glow) light the reef plainly.
	float extra = LIGHT_IS_DIRECTIONAL ? BACKLIGHT.x : 0.0;
	DIFFUSE_LIGHT += (ndl + extra) * ATTENUATION * LIGHT_COLOR / PI;
	// Her light (and other near lights) leaves only a faint dull sheen on the matte rock and sand.
	if (!LIGHT_IS_DIRECTIONAL) {
		vec3 hv = normalize(LIGHT + VIEW);
		float spec = pow(clamp(dot(NORMAL, hv), 0.0, 1.0), 10.0) * (1.0 - ROUGHNESS) * ndl;
		SPECULAR_LIGHT += spec * ATTENUATION * LIGHT_COLOR * 0.4;
	}
}
