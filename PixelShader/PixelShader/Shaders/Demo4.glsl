﻿#version 130
//Demo: 4. Circle as object

precision highp float;

in vec2 pixelCoords;
out vec4 pixelColor;

uniform float time;

struct Circle {
  vec2 position;
  float radius;
  vec4 color;
};
  
bool testCircle(Circle c, vec2 pixelCoords)
{
  return distance(c.position,pixelCoords) < c.radius;
}

void main(void)
{
  Circle c1 = Circle(vec2(0.5,0.5), 0.1 * sin(time), vec4(1,0,0,1))
 
  pixelColor = vec4(pixelCoords, 0.5 + 0.5*cos(time),1);  
  if(testCircle(c1, pixelCoords))
    pixelColor = (0.5 + 0.5*cos(time)) * c1.color;
}
