﻿#version 130
//Demo: 3. Simple 2D Circle

precision highp float;

in vec2 pixelCoords;
out vec4 pixelColor;


uniform vec2 resolution;
uniform vec2 mouse;
uniform float time;

vec2 center = vec2(0.5,0.5);
float radius = 0.1;
vec4 color = vec4(1,0,0,1);


void main(void)
{
  pixelColor = vec4(0,0,1,1);
  if(distance(center,pixelCoords) < radius)
    pixelColor = color;
 
}
