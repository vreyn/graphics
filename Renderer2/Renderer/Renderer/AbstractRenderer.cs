﻿using System;
using SceneLib;

namespace Renderer
{
  public abstract class AbstractRenderer : IRender
  {
    protected readonly Scene _scene;
    protected readonly IDisplay _display;
    protected bool _isParallel;

    public abstract string Name { get; }

    public bool IsParallel
    {
      get { return _isParallel; }
      set { _isParallel = value; }
    }

    protected AbstractRenderer(Scene scene, IDisplay display)
    {
      _scene = scene;
      _display = display;
    }
    
    public abstract void Render();

    protected Vector CalculateBlinnPhongIllumination(Vector viewDirection, Vector lightDirection, Vector lightColor, Vector normal, Material material)
    {
      var halfDirection = (viewDirection + lightDirection).Normalize3();
      var specular = material.Specular * lightColor *
          (float)Math.Pow(Math.Max(0, Vector.Dot3(normal, halfDirection)), material.Shininess);
      var diffuse = material.Diffuse * lightColor * Math.Max(0, Vector.Dot3(normal, lightDirection));
      return diffuse + specular;
    }
  }
}