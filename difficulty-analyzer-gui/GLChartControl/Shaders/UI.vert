#version 330 core

layout(location = 0) in vec3 vertexCoord;
layout(location = 1) in vec4 vertexColor;
layout(location = 2) in vec2 texcoord;

out vec4 fcolor;
out vec2 ftexcoord;
out vec2 fcoord;

out vec4 gcolor;
out vec2 gtexcoord;
out vec2 gcoord;

uniform int viewportWidth;
uniform int viewportHeight;
uniform float x;
uniform float y;
uniform float width;
uniform float height;

uniform float scaleX = 1;
uniform float scaleY = 1;

void main()
{
	mat4 transform = mat4(
		1.0f, 0.0f, 0.0f, 0.0f,
		0.0f, 1.0f, 0.0f, 0.0f,
		0.0f, 0.0f, 1.0f, 0.0f,
		0.0f, 0.0f, 0.0f, 1.0f
	);
	transform[0][0] = width / (float(viewportWidth) / scaleX);
	transform[1][1] = height / (float(viewportHeight) / scaleY);
	transform[1][3] = 1 - y / (float(viewportHeight) / scaleY) * 2;
	transform[0][3] = -1 + x / (float(viewportWidth) / scaleX) * 2;

	fcoord = vec2((vertexCoord.x + 1.0) / 2.0 * width, (1.0 - vertexCoord.y) / 2.0 * height);
	gl_Position = vec4(vertexCoord, 1.0) * transform;
	fcolor = vertexColor;
	ftexcoord = texcoord;

	gcolor = fcolor;
	gcoord = fcoord;
	gtexcoord = ftexcoord;
}