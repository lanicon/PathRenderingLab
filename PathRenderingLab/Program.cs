﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PathRenderingLab.PathCompiler;
using System.Xml;
using PathRenderingLab.Parsers;
using PathRenderingLab.SvgContents;

namespace PathRenderingLab
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        public struct PathDetails
        {
            public Path Path;

            public Color? FillColor;
            public Color? StrokeColor;
            public Color BackgroundColor;

            public FillRule FillRule;
            public double StrokeWidth;
            public StrokeLineCap StrokeLineCap;
            public StrokeLineJoin StrokeLineJoin;
            public double MiterLimit;
            public bool InvertY;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            string file;
            if (args.Length > 0) file = args[0];
            else
            {
                Console.Write("Enter path of SVG file: ");
                file = Console.ReadLine();
            }

            var svg = new Svg(file);
            var paths = new List<SvgPath>();

            void EnumeratePaths(SvgGroup g)
            {
                if (!g.Renderable) return;

                foreach (var node in g.Children)
                {
                    if (node is SvgGroup) EnumeratePaths(node as SvgGroup);
                    else if (node is SvgPath) paths.Add(node as SvgPath);
                }
            }

            EnumeratePaths(svg.Root);

            int pathId = 0, numPaths = 0;
            var triangleIndices = new List<int>();
            var curveVertices = new List<VertexPositionCurve>();
            var doubleCurveVertices = new List<VertexPositionDoubleCurve>();

            var triangleIndicesStartingIds = new List<int>() { 0 };
            var curveVerticesStartingIds = new List<int>() { 0 };
            var doubleCurveVerticesStartingIds = new List<int>() { 0 };

            var colors = new List<Color>();
            var transforms = new List<Matrix>();
            var vertexCache = new Dictionary<Vector2, int>();

            int curTriangleIndicesStartingId = 0;
            int curCurveVerticesStartingId = 0;
            int curDoubleCurveVerticesStartingId = 0;

            int IdForVertex(Vector2 v)
            {
                if (!vertexCache.ContainsKey(v))
                    vertexCache[v] = vertexCache.Count;
                return vertexCache[v];
            }

            foreach (var svgPath in paths)
            {
                var path = svgPath.Path;
                var ps = svgPath.PathStyle;

                Console.WriteLine($"Parsed path {pathId++}: {svgPath.Path}");
                Console.WriteLine();

                // Normalize the path
                var normalMatrix = path.Normalize();
                var matrix = (Matrix)(svgPath.Transform.ToMatrix() * normalMatrix);

                if (ps.FillColor.HasValue)
                {
                    AddDrawing(PathCompilerMethods.CompileFill(path, ps.FillRule));
                    colors.Add(ps.FillColor.Value);
                    transforms.Add(matrix);
                    numPaths++;
                }

                if (ps.StrokeColor.HasValue)
                {
                    AddDrawing(PathCompilerMethods.CompileStroke(path, ps.StrokeWidth / normalMatrix.A,
                        ps.StrokeLineCap, ps.StrokeLineJoin, ps.MiterLimit));
                    colors.Add(ps.StrokeColor.Value);
                    transforms.Add(matrix);
                    numPaths++;
                }

                void AddDrawing(CompiledDrawing drawing)
                {
                    var curTriangleIndices = new List<int>();
                    var curCurveVertices = new List<VertexPositionCurve>();
                    var curDoubleCurveVertices = new List<VertexPositionDoubleCurve>();

                    foreach (var tri in drawing.Triangles)
                    {
                        curTriangleIndices.Add(IdForVertex((Vector2)tri.A));
                        curTriangleIndices.Add(IdForVertex((Vector2)tri.B));
                        curTriangleIndices.Add(IdForVertex((Vector2)tri.C));
                    }

                    foreach (var tri in drawing.CurveTriangles)
                        curCurveVertices.AddRange(new[]
                        {
                            (VertexPositionCurve)tri.A,
                            (VertexPositionCurve)tri.B,
                            (VertexPositionCurve)tri.C
                        });

                    foreach (var tri in drawing.DoubleCurveTriangles)
                        curDoubleCurveVertices.AddRange(new[]
                        {
                            (VertexPositionDoubleCurve)tri.A,
                            (VertexPositionDoubleCurve)tri.B,
                            (VertexPositionDoubleCurve)tri.C
                        });

                    triangleIndices.AddRange(curTriangleIndices);
                    curveVertices.AddRange(curCurveVertices);
                    doubleCurveVertices.AddRange(curDoubleCurveVertices);

                    curTriangleIndicesStartingId += curTriangleIndices.Count;
                    triangleIndicesStartingIds.Add(curTriangleIndicesStartingId);

                    curCurveVerticesStartingId += curCurveVertices.Count;
                    curveVerticesStartingIds.Add(curCurveVerticesStartingId);

                    curDoubleCurveVerticesStartingId += curDoubleCurveVertices.Count;
                    doubleCurveVerticesStartingIds.Add(curDoubleCurveVerticesStartingId);
                }
            }

            int length = vertexCache.Count == 0 ? 0 : vertexCache.Max(p => p.Value) + 1;
            var allVertices = new Vector2[length];
            foreach (var kvp in vertexCache) allVertices[kvp.Value] = kvp.Key;

            Console.WriteLine("Statistics:");

            void WriteStats(string name, int numIndices, int numCurveVertices, int numDoubleCurveVertices)
            {
                Console.WriteLine($"{name}: {(numIndices + numCurveVertices + numDoubleCurveVertices) / 3} triangles " +
                    $"({numIndices / 3} filled, {numCurveVertices / 3} curves and {numDoubleCurveVertices / 3} double curves)");
            }

            for (int i = 0; i < numPaths; i++)
                WriteStats($"path {i}",
                    triangleIndicesStartingIds[i + 1] - triangleIndicesStartingIds[i],
                    curveVerticesStartingIds[i + 1] - curveVerticesStartingIds[i],
                    doubleCurveVerticesStartingIds[i + 1] - doubleCurveVerticesStartingIds[i]);

            using (var game = new PathRenderingLab())
            {
                game.BackgroundColor = Color.White;
                game.AllVertices = allVertices;
                game.DrawingColors = colors.ToArray();
                game.DrawingTransforms = transforms.ToArray();
                game.DrawingIndices = triangleIndices.ToArray();
                game.DrawingCurveVertices = curveVertices.ToArray();
                game.DrawingDoubleCurveVertices = doubleCurveVertices.ToArray();
                game.DrawingIndicesStartingIds = triangleIndicesStartingIds.ToArray();
                game.DrawingCurveVerticesStartingIds = curveVerticesStartingIds.ToArray();
                game.DrawingDoubleCurveVerticesStartingIds = doubleCurveVerticesStartingIds.ToArray();
                game.NumDrawings = numPaths;

                game.Run();
            }
        }
    }
#endif
}
