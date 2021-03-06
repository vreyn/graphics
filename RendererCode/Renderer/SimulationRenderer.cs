﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using Renderer.Base;
using SceneLib;

namespace Renderer
{
  public class SimulationRenderer : BaseRenderer
  {
    private readonly IPixelSampler _pixelSampler;
    private Random _randomizer = new Random();
    private readonly Dictionary<float, float> _randomValuesX = new Dictionary<float, float>();
    private readonly Dictionary<float, float> _randomValuesY = new Dictionary<float, float>();

    public SimulationRenderer(Scene scene, IPixelSampler pixelSampler)
      : base(scene)
    {
      if (pixelSampler == null) throw new ArgumentNullException("pixelSampler");
      _pixelSampler = pixelSampler;
      //ModelToWorldTransformation();
    }

    public override void Update(double time)
    {

    }

    public override void OnKeyPress(KeyPressEventArgs e)
    {

    }

    public override void Render()
    {

      if (RenderingParameters.Instance.EnableParallelism)
        _pixelSampler.Samples.ToList().AsParallel().ForAll(s => s.Color = GetSampleColor(s.X, s.Y, s.Time));
      else
        _pixelSampler.Samples.ToList().ForEach(s => s.Color = GetSampleColor(s.X, s.Y, s.Time));

      for (int i = 0; i < _scene.Width; i++)
      {
        for (int j = 0; j < _scene.Height; j++)
        {
          PaintPixel(i, j, _pixelSampler.Integrate(i, j));
        }

      }
    }

    private void ModelToWorldTransformation()
    {
      foreach (var renderObject in _scene.Objects)
      {
        if (renderObject is IHavePolygons)
        {
          var modelToWorld = ModelToWorld(renderObject);
          var inverseTranspose = modelToWorld.Inverse().Transpose();
          var polygonList = renderObject as IHavePolygons;
          foreach (var polygon in polygonList.Polygons)
          {
            foreach (var vertex in polygon.Vertices)
            {
              vertex.SimPosition = modelToWorld * vertex.Position;
              //vertex.Normal = inverseTranspose*vertex.Normal;
            }
          }
        }
      }
    }

    private Vector GetSampleColor(float screenX, float screenY, float time)
    {
      var eyeRay = CreateEyeRay(screenX, screenY, time);
      return RayTrace(eyeRay, 0, time, screenX, screenY).Clamp3();
    }

    private Ray CreateEyeRay(float screenX, float screenY, float time)
    {
      var sceneCamera = _scene.Camera;
      var pixelCoords = sceneCamera.PixelToCameraCoordinates(screenX, screenY, _scene.Width, _scene.Height);
      var coordinateBasis = sceneCamera.GetCameraCoordinatesBasis();
      var pixelWorldCoords = sceneCamera.Position + coordinateBasis[0] * (pixelCoords.X) + coordinateBasis[1] * (pixelCoords.Y) +
                             coordinateBasis[2] * (pixelCoords.Z);

      var eyeRay = SampleEyeRay(time, sceneCamera, pixelWorldCoords, coordinateBasis, screenX, screenY);
      return eyeRay;
    }

    private Ray SampleEyeRay(float time, Camera sceneCamera, Vector pixelWorldCoords, List<Vector> cameraCoordinateBasis, float screenX, float screenY)
    {
      var direction = pixelWorldCoords - sceneCamera.Position;
      direction = direction.Normalize3();
      var focalPlanePosition = sceneCamera.Position + RenderingParameters.Instance.FocalDistance * direction;

      var randomX = GetRandomValue(screenX, screenY);
      var randomY = GetRandomValue(screenX, screenY);
      var eyePosition = sceneCamera.Position + (RenderingParameters.Instance.LensSize / 2.0f) * ((float)(2 * randomX - 1)) * cameraCoordinateBasis[0] +
                        (RenderingParameters.Instance.LensSize / 2.0f) * ((float)(2 * randomY - 1)) * cameraCoordinateBasis[1];

      var eyeRay = new Ray(eyePosition, focalPlanePosition - eyePosition, time); // eye rays
      return eyeRay;
    }

    private float GetRandomValue(float x, float y)
    {
      _randomizer = new Random((int)(x * _scene.Width) + (int)(_scene.Height * y));
      return (float)_randomizer.NextDouble();
    }

    private Vector RayTrace(Ray ray, int recursion, float time, float screenX, float screenY)
    {
      IntersectObjects(ray);
      if (ray.IntersectedObject == null) return _scene.BackgroundColor;
      return Shade(ray, recursion, time, screenX, screenY);
    }

    private Vector Shade(Ray ray, int recursion, float time, float screenX, float screenY)
    {
      var intersectedDitance = ray.IntersectionDistance;
      var intersectedObject = ray.IntersectedObject;

      var intersectionPoint = ray.Position + (ray.Direction * intersectedDitance);
      var normal = intersectedObject.GetNormal(intersectionPoint, time);
      var material = intersectedObject.GetMaterial(intersectionPoint);

      var shadingColor = RenderingParameters.Instance.EnableAmbient ? _scene.AmbientLight * material.Diffuse : new Vector(0, 0, 0);
      if (RenderingParameters.Instance.EnableShading)
      {
        const float epsilon = 0.1f;
        foreach (var light in _scene.Lights)
        {
          var viewDirection = ray.Direction * -1;
          var lightDirection = (light.Position - intersectionPoint).Normalize3();
          var lightDistance = (light.Position - intersectionPoint).Magnitude3();

          var shadowRay = SampleShadowRay(time, intersectionPoint, light, epsilon, screenX, screenY);
          if (RenderingParameters.Instance.EnableShadows)
            IntersectObjects(shadowRay); // shadow rays

          if ((shadowRay.IntersectedObject == null || shadowRay.IntersectionDistance >= lightDistance))
          {
            shadingColor += CalculateBlinnPhongIllumination(viewDirection, lightDirection, light.Color, normal, material);
          }

          if (RenderingParameters.Instance.EnableReflections && material.ReflectivityAttenuation > 0 &&
            recursion < RenderingParameters.Instance.NumberOfRecursiveRays)
          {
            var reflectionDirection = GetReflectionDirection(ray, normal);
            var reflectedRay = new Ray(intersectionPoint + reflectionDirection * epsilon, reflectionDirection, time);
            shadingColor += material.ReflectivityAttenuation * RayTrace(reflectedRay, ++recursion, time, screenX, screenY);
          }
          if (RenderingParameters.Instance.EnableRefractions && material.RefractiveIndex > 0 &&
              recursion < RenderingParameters.Instance.NumberOfRecursiveRays)
          {
            var reflectionDirection = GetReflectionDirection(ray, normal);
            var reflectedRay = new Ray(intersectionPoint + reflectionDirection * epsilon, reflectionDirection, time);

            var dDotN = Vector.Dot3(ray.Direction, normal);
            var nt = material.RefractiveIndex;
            var refractionDirection = new Vector();
            var cosine = 0.0f;
            if (dDotN < 0)
            {
              refractionDirection = GerRefractionDirection(ray, normal, nt);
              cosine = -dDotN;
            }
            else
            {
              refractionDirection = GerRefractionDirection(ray, -1.0f * normal, 1.0f / nt);
              if (refractionDirection != null)
              {
                cosine = Vector.Dot3(refractionDirection, normal);
              }
            }
            if (refractionDirection == null)
            {
              shadingColor += material.RefractiveAttenuation * RayTrace(reflectedRay, ++recursion, time, screenX, screenY);
            }
            else
            {
              var r0 = ((nt - 1) * (nt - 1)) / ((nt + 1) * (nt + 1));
              var r = r0 + (1 - r0) * (float)Math.Pow(1 - cosine, 5);
              var refractedRay = new Ray(intersectionPoint + refractionDirection * epsilon, refractionDirection, time);
              shadingColor += material.RefractiveAttenuation * (r * RayTrace(reflectedRay, recursion + 1, time, screenX, screenY) + (1 - r) * RayTrace(refractedRay, ++recursion, time, screenX, screenY));
            }

          }
        }
      }
      else
      {
        shadingColor = material.Diffuse;
      }

      return shadingColor;
    }

    private static Vector GetReflectionDirection(Ray ray, Vector normal)
    {
      var reflectionDirection = (ray.Direction - normal * (Vector.Dot3(ray.Direction, normal) * 2)).Normalize3();
      return reflectionDirection;
    }

    private Vector GerRefractionDirection(Ray ray, Vector normal, float nt)
    {
      var dDotN = Vector.Dot3(ray.Direction, normal);
      var sqrtValue = 1 - (1 - dDotN * dDotN) / (nt * nt);
      if (sqrtValue < 0)
        return null;
      var d = (ray.Direction - normal * dDotN) / nt -
                            normal * (float)Math.Sqrt(sqrtValue);
      return d.Normalize3();
    }

    private Ray SampleShadowRay(float time, Vector intersectionPoint, Light light, float epsilon, float screenX, float screenY)
    {
      var randomX = GetRandomValue(screenX, screenY);
      var randomY = GetRandomValue(screenX, screenY);
      var randomLightPosition = light.Position + light.Width * (2 * randomX - 1) +
                        light.Height * (2 * randomY - 1);
      var lightDirection = (randomLightPosition - intersectionPoint).Normalize3();
      var shadowRay = new Ray(intersectionPoint + lightDirection * epsilon, lightDirection, time);
      return shadowRay;
    }

    private void IntersectObjects(Ray ray)
    {
      foreach (var sceneObject in _scene.Objects)
      {
        sceneObject.Intersect(ray);
      }
    }


  }
}