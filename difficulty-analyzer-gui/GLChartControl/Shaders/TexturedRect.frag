#version 330 core

in vec4 fcolor; 
in vec2 ftexcoord;
in vec2 fcoord;
in vec2 fsize;

out vec4 fragColor;

uniform vec4 color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float radius = 0;
uniform sampler2D colortex;

void main()
{
    fragColor = texture(colortex, ftexcoord) * fcolor * color;

	vec2 m = vec2(min(fcoord.x, fsize.x - fcoord.x), min(fcoord.y, fsize.y - fcoord.y));
	if(m.x < radius && m.y < radius)
		fragColor.a *= clamp(radius - length(vec2(radius) - m), 0, 1);
	else
		fragColor.a *= clamp(min(m.x, m.y), 0, 1);
}