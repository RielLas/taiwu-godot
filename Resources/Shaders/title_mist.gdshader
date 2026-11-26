shader_type canvas_item;
render_mode blend_mix;

uniform float mist_speed : hint_range(0.0, 0.2) = 0.018;
uniform float mist_opacity : hint_range(0.0, 0.5) = 0.16;
uniform float water_line_y : hint_range(0.5, 0.95) = 0.68;
uniform float shimmer_amp : hint_range(0.0, 0.01) = 0.0022;
uniform vec4 mist_tint : source_color = vec4(0.86, 0.89, 0.85, 1.0);

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
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

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * noise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void fragment() {
    vec2 uv = UV;
    // Gentle lower-water wave shimmer below water_line_y
    float water_mask = smoothstep(water_line_y, water_line_y + 0.12, uv.y);
    uv.x += sin(uv.y * 42.0 + TIME * 1.4) * shimmer_amp * water_mask;

    vec4 base = texture(TEXTURE, uv) * COLOR;

    // Drifting ink-wash mist ribbons across mid-peaks
    float n1 = fbm(vec2(UV.x * 3.5 + TIME * mist_speed, UV.y * 2.2));
    float n2 = fbm(vec2(UV.x * 5.2 - TIME * mist_speed * 0.6, UV.y * 3.4 + 1.7));
    float mist = smoothstep(0.45, 0.82, (n1 + n2) * 0.5);
    float band = smoothstep(0.12, 0.45, UV.y) * (1.0 - smoothstep(0.65, 0.92, UV.y));

    base.rgb = mix(base.rgb, mist_tint.rgb, mist * band * mist_opacity);
    COLOR = base;
}
