﻿using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Sandwych.MapMatchingKit.Topology;
using Sandwych.MapMatchingKit.Spatial;
using Sandwych.MapMatchingKit.Spatial.Geometries;
using Sandwych.MapMatchingKit.Spatial.Index;

namespace Sandwych.MapMatchingKit.Roads
{
    /// <summary>
    /// <para>
    /// Implementation of a road map with (directed) roads, i.e. <see cref="Road"/> objects. It provides a road
    /// network for routing that is derived from <see cref="Topology.AbstractGraph{TEdge}"/> and spatial search of roads with a
    /// <see cref="Spatial.Index.ISpatialIndex{TItem}"/>.
    /// </para>
    /// <para>
    /// <b>Note:</b> Since <see cref="Road"/> objects are directed representations of <see cref="RoadInfo"/> objects,
    /// identifiers have a special mapping, see <see cref="Road"/>.
    /// </para>
    /// </summary>
    public sealed class RoadMap : AbstractGraph<Road>
    {
        public ISpatialIndex<RoadInfo> Index { get; }
        private readonly ISpatialOperation _spatial;

        public RoadMap(IEnumerable<Road> roads, ISpatialOperation spatial) : base(roads)
        {
            _spatial = spatial;

            // The original Barefoot is using Quad Tree for spatial indexing, however, in my experiment, NTS's STRtree is
            // much faster than NTS's Quadtree.
            //this.Index = new Spatial.Index.RBush.RBushSpatialIndex<RoadInfo>(roads.Select(x => x.RoadInfo), spatial, r => r.Geometry, r => r.Length);
            this.Index = new RtreeIndex<RoadInfo>(roads.Select(x => x.RoadInfo), spatial, r => r.Geometry, r => r.Length);
        }

        private IEnumerable<RoadPoint> Split(IEnumerable<(RoadInfo Item, double Fraction, Coordinate2D InterpolatedPoint)> points)
        {
            /*
             * This uses the road
             */
            foreach (var point in points)
            {
                yield return new RoadPoint(this.EdgeMap[point.Item.Id * 2], point.Fraction, point.InterpolatedPoint, _spatial);

                var backwardRoadId = point.Item.Id * 2 + 1;
                if (this.EdgeMap.TryGetValue(backwardRoadId, out var road))
                {
                    yield return new RoadPoint(road, 1.0 - point.Fraction, point.InterpolatedPoint, _spatial);
                }
            }
        }

        public IEnumerable<RoadPoint> Radius(in Coordinate2D c, double r, int k = -1) =>
            this.Split(this.Index.Radius(c, r, k));

    }
}
