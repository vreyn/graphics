﻿using System;
using SceneLib;

namespace Renderer
{
  public class Mesh : MeshBase
  {
    private TriangleBase _currentIntersectedPolygon;

    public Mesh(string name, string filename, IMeshLoader meshLoader, IRenderObjectFactory renderObjectFactory)
      : base(name, filename, meshLoader, renderObjectFactory)
    {
    }

    public override bool Intersect(Ray ray)
    {
      foreach (var polygon in Triangles)
      {
        if (polygon.Intersect(ray))
        {
          _currentIntersectedPolygon = polygon;
          return true;
        }
      }
      return false;
    }

    public override Vector GetNormal(Vector point)
    {
      if (_currentIntersectedPolygon == null) throw new ApplicationException("Missing current polygon intersected. Either Intersect method wasnt called, or it didnt found an intersection with this mesh");
      return _currentIntersectedPolygon.GetNormal(point);
    }

    public override Material GetMaterial(Vector point)
    {
      if (_currentIntersectedPolygon == null) throw new ApplicationException("Missing current polygon intersected. Either Intersect method wasnt called, or it didnt found an intersection with this mesh");
      return _currentIntersectedPolygon.GetMaterial(point);
    }
  }
}
