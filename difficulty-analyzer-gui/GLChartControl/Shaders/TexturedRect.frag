#version 330 core

in vec4 fcolor; 
in vec2 ftexcoord;
in vec2 fcoord;

out vec4 fragColor;

uniform vec4 color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float width;
uniform float height;
uniform float radius = 0;
uniform sampler2D colortex;

void main()
{
    fragColor = texture(colortex, ftexcoord) * fcolor * color;

	vec2 m = vec2(min(fcoord.x, width - fcoord.x), min(fcoord.y, height - fcoord.y));
	if(m.x < radius && m.y < radius) 
		fragColor.a *= clamp(0, 1, radius - length(vec2(radius) - m));
	else 
		fragColor.a *= clamp(0, 1, min(m.x, m.y));
}