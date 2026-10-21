shader_type canvas_item;
render_mode unshaded;

// All effects are procedural and follow the artwork's aspect-preserving UVs.
uniform float ember_strength : hint_range(0.0, 2.0) = 0.75;
uniform float smoke_strength : hint_range(0.0, 2.0) = 0.65;
uniform float light_strength : hint_range(0.0, 2.0) = 0.75;
uniform vec4 ember_color : source_color = vec4(0.78, 0.48, 0.055, 1.0);
uniform vec4 smoke_color : source_color = vec4(0.39, 0.29, 0.12, 1.0);
// Negative uses live time; nonnegative freezes time for reproducible previews.
uniform float preview_time = -1.0;
varying vec4 vertex_tint;

void vertex() {
    vertex_tint = COLOR;
}

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}

float cloud_noise(vec2 p) {
    return noise(p) * 0.57 + noise(p * 2.03 + vec2(7.1)) * 0.28
        + noise(p * 4.07 + vec2(17.3)) * 0.15;
}

float ellipse(vec2 uv, vec2 center, vec2 radius) {
    vec2 d = (uv - center) / radius;
    return 1.0 - smoothstep(0.15, 1.0, dot(d, d));
}

void fragment() {
    vec4 art = texture(TEXTURE, UV);
    float t = preview_time >= 0.0 ? preview_time : TIME;

    // Keep drifting haze and embers away from the face, hand and torso.
    float left_air = 1.0 - smoothstep(0.27, 0.52, UV.x);
    float top_air = ellipse(UV, vec2(0.51, 0.12), vec2(0.09, 0.32));
    float edge_air = smoothstep(0.96, 1.0, UV.x)
        * (1.0 - smoothstep(0.25, 0.48, UV.y));
    float air = clamp(left_air + top_air + edge_air, 0.0, 1.0);

    // Slow upward flow; two scales keep the haze soft instead of a flat wash.
    vec2 drift = vec2(t * 0.022, t * 0.032);
    float cloud = cloud_noise(UV * vec2(4.5, 3.5) + drift);
    float wisps = cloud_noise(UV * vec2(9.0, 6.0) + drift * 1.4 + vec2(11.0));
    float smoke = smoothstep(0.42, 0.77, cloud * 0.75 + wisps * 0.25);
    smoke *= air * smoke_strength * 0.105;
    vec3 color = mix(art.rgb, smoke_color.rgb, smoke);

    // Warm illumination gently breathes only in existing yellow highlights.
    float gold = smoothstep(0.08, 0.28, art.r - art.b)
        * smoothstep(0.28, 0.72, art.r) * smoothstep(0.18, 0.42, art.g);
    float rim = max(ellipse(UV, vec2(0.49, 0.54), vec2(0.23, 0.6)),
        ellipse(UV, vec2(0.88, 0.18), vec2(0.25, 0.35)));
    float flicker = sin(t * 1.23 + UV.x * 5.0) * 0.55
        + sin(t * 2.17 + UV.y * 4.0) * 0.30 + sin(t * 3.71) * 0.15;
    color += vec3(0.11, 0.067, 0.009) * gold * rim * flicker * light_strength;

    // Six narrow streams, with random spacing. Each fragment only checks its
    // own stream and its neighbors instead of evaluating every visible spark.
    float embers = 0.0;
    float column = floor(UV.x / 0.09);
    for (int i = -1; i <= 1; i++) {
        float stream = column + float(i);
        if (stream < 0.0 || stream > 5.0) {
            continue;
        }
        float speed = mix(0.033, 0.055, hash(vec2(stream, 3.7)));
        float row = floor((UV.y + t * speed) * 4.0);
        float r = hash(vec2(stream, row));
        if (r < 0.38) {
            continue;
        }
        float x = (stream + mix(0.22, 0.78, r)) * 0.09;
        float y = (row + mix(0.22, 0.78, hash(vec2(row, stream + 7.8)))) * 0.25 - t * speed;
        x += sin(t * 0.7 + row * 2.7 + stream) * 0.009;
        vec2 d = (UV - vec2(x, y)) * vec2(1920.0, 1080.0);
        d.x += d.y * 0.22;
        // Most fragments are far from this spark; skip the expensive falloff.
        if (abs(d.x) > 20.0 || abs(d.y) > 20.0) {
            continue;
        }
        float size = mix(1.1, 2.5, r);
        float core = exp(-dot(d / vec2(size, size * 1.7), d / vec2(size, size * 1.7)));
        float halo = exp(-dot(d / vec2(size * 3.0), d / vec2(size * 3.0))) * 0.15;
        float fade = smoothstep(0.0, 0.12, y) * (1.0 - smoothstep(0.88, 1.0, y));
        embers += (core + halo) * fade * (0.8 + 0.2 * sin(t * 3.0 + row + stream));
    }
    color += ember_color.rgb * embers * air * ember_strength;
    COLOR = vec4(clamp(color, vec3(0.0), vec3(1.0)), art.a) * vertex_tint;
}
