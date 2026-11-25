shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D fog_texture : repeat_enable, filter_linear;
uniform vec2 fog_scroll_speed = vec2(0.006, 0.003);
uniform vec4 fog_color : source_color = vec4(0.031, 0.051, 0.059, 1.0); // Serene dark jade-slate #080D0F
uniform vec4 dao_mist_color : source_color = vec4(0.18, 0.28, 0.26, 0.65); // Gentle Dao mist

varying vec2 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec4 base_color = COLOR * tex;

	// Subtle organic rice paper ink grain
	float grain = fract(sin(dot(UV, vec2(12.9898, 78.233))) * 43758.5453);
	base_color.rgb += (grain - 0.5) * 0.02;

	// Fog of War: COLOR.a represents visibility
	// 1.0 = fully visible (no fog)
	// 0.55 = explored but out of line-of-sight (lingering Dao mist)
	// 0.20 = unexplored (gentle ethereal Dao mist overlay on organic mountain landscape)
	float fog_factor = clamp(1.0 - COLOR.a, 0.0, 1.0);
	if (fog_factor > 0.02) {
		// Dual-frequency scrolling world-space fog UVs for organic drifting mist
		vec2 uv1 = world_position * 0.04 + TIME * fog_scroll_speed;
		vec2 uv2 = world_position * 0.07 - TIME * fog_scroll_speed * 0.65;
		vec4 fog_sample1 = texture(fog_texture, uv1);
		vec4 fog_sample2 = texture(fog_texture, uv2);
		float mist_density = (fog_sample1.a + fog_sample2.a) * 0.5;

		// Gentle ethereal Dao mist overlay: blend soft mist rather than pitch black storm clouds
		vec3 mist_tint = mix(fog_color.rgb, dao_mist_color.rgb, mist_density * 0.5);
		// Smooth ramp across the fog boundary so there is no hard step discontinuity
		float mist_ramp = smoothstep(0.02, 0.14, fog_factor);
		float blend_amount = clamp(fog_factor * 0.92 * mist_ramp + mist_ramp * mist_density * 0.08, 0.0, 1.0);
		base_color.rgb = mix(base_color.rgb, mist_tint, blend_amount);
	}

	base_color.a = 1.0;
	COLOR = base_color;
}
