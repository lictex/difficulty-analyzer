#version 330 core

layout (points) in;
layout (triangle_strip, max_vertices = 6) out;

in vec4 gcolor[];
in vec2 gtexcoord[];
in vec2 gcoord[];

out vec4 fcolor;
out vec2 ftexcoord;
out vec2 fcoord;

uniform int viewportWidth;
uniform int viewportHeight;
uniform float width;
uniform float height;
uniform float x;
uniform float y;
uniform float scaleX;
uniform float scaleY;

void main()
{
	gl_Position = vec4(gl_in[0].gl_Position.x - 4.0  * (scaleX / viewportWidth), gl_in[0].gl_Position.yzw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x - 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	gl_Position = vec4(gl_in[0].gl_Position.x - 4.0 * (scaleX / viewportWidth), 1 - (height / 2 + y) * scaleY / viewportHeight * 2, gl_in[0].gl_Position.zw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x - 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	gl_Position = vec4(gl_in[0].gl_Position.x + 4.0 * (scaleX / viewportWidth), 1 - (height / 2 + y) * scaleY / viewportHeight * 2, gl_in[0].gl_Position.zw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x + 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	EndPrimitive();
	gl_Position = vec4(gl_in[0].gl_Position.x - 4.0 * (scaleX / viewportWidth), gl_in[0].gl_Position.yzw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x - 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	gl_Position = vec4(gl_in[0].gl_Position.x + 4.0 * (scaleX / viewportWidth), 1 - (height / 2 + y) * scaleY / viewportHeight * 2, gl_in[0].gl_Position.zw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x + 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	gl_Position = vec4(gl_in[0].gl_Position.x + 4.0 * (scaleX / viewportWidth), gl_in[0].gl_Position.yzw);
	fcolor = gcolor[0];
	fcoord = vec2(gcoord[0].x + 2, height / 2);
	ftexcoord = gtexcoord[0];
	EmitVertex();
	EndPrimitive();
}