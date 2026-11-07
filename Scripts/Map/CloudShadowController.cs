using System;
using Godot;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Simulates slow drifting sky cloud shadows across the isometric world.
    /// Creates a camera-following world-space quad with a procedurally generated
    /// multi-octave FBM cloud shadow shader at 15% opacity, scrolling UVs continuously.
    /// Obeys Constitutional Law 1 (Halal & Zero Scene Searching) and passes all mouse clicks through.
    /// </summary>
    public partial class CloudShadowController : Node2D
    {
        [Export] public Vector2 PanSpeed { get; set; } = new Vector2(0.015f, 0.008f);
        [Export] public float ShadowOpacity { get; set; } = 0.15f;
        [Export] public float CloudScale { get; set; } = 3.5f;

        private ColorRect? _shadowRect;
        private Node2D? _followTarget;

        public override void _Ready()
        {
            ZIndex = 8; // Above terrain (0), decals (1), landmarks (2), tokens (3); below HUD CanvasLayer
            CreateCloudShadowQuad();
        }

        private void CreateCloudShadowQuad()
        {
            // Large world-space quad (160x160 world units) centered over origin
            float halfSpan = 80f;
            _shadowRect = new ColorRect
            {
                Name = "DriftingCloudShadowsQuad",
                Position = new Vector2(-halfSpan, -halfSpan),
                Size = new Vector2(halfSpan * 2f, halfSpan * 2f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var shader = new Shader();
            shader.Code = @"
shader_type canvas_item;

uniform vec2 pan_speed = vec2(0.015, 0.008);
uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.15;
uniform float cloud_scale = 3.5;

varying vec2 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    v += noise(p) * 0.65;
    v += noise(p * 2.0 + vec2(5.2, 1.3)) * 0.35;
    return v;
}

void fragment() {
    // Model/World space coordinates so clouds drift consistently
    vec2 uv = world_pos * (cloud_scale * 0.04) + TIME * pan_speed;
    float n = fbm(uv);
    float cloud = smoothstep(0.28, 0.72, n);
    COLOR = vec4(0.01, 0.02, 0.02, cloud * shadow_opacity);
}
";

            var mat = new ShaderMaterial { Shader = shader };
            mat.SetShaderParameter("pan_speed", PanSpeed);
            mat.SetShaderParameter("shadow_opacity", ShadowOpacity);
            mat.SetShaderParameter("cloud_scale", CloudScale);

            _shadowRect.Material = mat;
            AddChild(_shadowRect);
        }

        public void SetFollowTarget(Node2D target)
        {
            _followTarget = target;
        }

        public override void _Process(double delta)
        {
            if (_followTarget != null && GodotObject.IsInstanceValid(_followTarget))
            {
                Position = _followTarget.Position;
            }
        }
    }
}
