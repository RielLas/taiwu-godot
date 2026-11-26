shader_type canvas_item;
render_mode blend_mix;

uniform vec4 backdrop_ink : source_color = vec4(0.094, 0.110, 0.098, 1.0); // #181c19
uniform vec4 mist_jade : source_color = vec4(0.20, 0.27, 0.24, 0.75);
uniform vec2 drift_speed = vec2(0.008, 0.004);
uniform float feather_softness : hint_range(0.05, 0.5) = 0.22;

varying vec2 world_pos;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
        u.y
    );
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    // Preserve tile sprite alpha cleanly!
    float sprite_alpha = tex.a;
    if (sprite_alpha < 0.01) {
        discard;
    }

    vec3 rgb = tex.rgb * COLOR.rgb;
    float fog_amount = clamp(1.0 - COLOR.a, 0.0, 1.0);
    if (fog_amount > 0.01) {
        float n1 = noise(world_pos * 0.005 + TIME * drift_speed);
        float n2 = noise(world_pos * 0.011 - TIME * drift_speed * 0.7);
        float cloud = (n1 + n2) * 0.5;
        float feathered = smoothstep(0.02, 0.02 + feather_softness, fog_amount + (cloud - 0.5) * 0.18);
        vec3 fog_rgb = mix(backdrop_ink.rgb, mist_jade.rgb, cloud * 0.45);
        rgb = mix(rgb, fog_rgb, feathered * 0.88);
    }

    COLOR = vec4(rgb, sprite_alpha);
}
