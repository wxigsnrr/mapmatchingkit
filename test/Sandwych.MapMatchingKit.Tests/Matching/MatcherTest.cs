﻿using GeoAPI.Geometries;
using NetTopologySuite.IO;
using Sandwych.MapMatchingKit.Matching;
using Sandwych.MapMatchingKit.Roads;
using Sandwych.MapMatchingKit.Spatial;
using Sandwych.MapMatchingKit.Spatial.Geometries;
using Sandwych.MapMatchingKit.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Sandwych.MapMatchingKit.Tests.Matching
{
    public class MatcherTest : TestBase
    {
        private readonly ISpatialOperation _spatial = new GeographySpatialOperation();
        private readonly DijkstraRouter<Road, RoadPoint> _router = new DijkstraRouter<Road, RoadPoint>();
        private readonly RoadMap _map;
        private readonly Func<Road, double> _cost = new Func<Road, double>(Costs.TimeCost);

        class MockedRoadReader
        {
            private readonly List<RoadInfo> _roads = new List<RoadInfo>();
            private readonly (long, long, long, bool, string)[] _entries = new(long, long, long, bool, string)[]
            {
                (0L, 0L, 1L, false, "LINESTRING(11.000 48.000, 11.010 48.000)"),
                (1L, 1L, 2L, false, "LINESTRING(11.010 48.000, 11.020 48.000)"),
                (2L, 2L, 3L, false, "LINESTRING(11.020 48.000, 11.030 48.000)"),
                (3L, 1L, 4L, true, "LINESTRING(11.010 48.000, 11.011 47.999)"),
                (4L, 4L, 5L, true, "LINESTRING(11.011 47.999, 11.021 47.999)"),
                (5L, 5L, 6L, true, "LINESTRING(11.021 47.999, 11.021 48.010)")
            };
            private IEnumerator<RoadInfo> _enumerator;

            public IEnumerable<RoadInfo> Roads => _roads;

            public MockedRoadReader(ISpatialOperation spatial)
            {
                var wktRdr = new WKTReader();
                foreach (var e in _entries)
                {
                    var geom = wktRdr.Read("SRID=4326;" + e.Item5) as ILineString;
                    _roads.Add(new RoadInfo(e.Item1, e.Item2, e.Item3, e.Item1, e.Item4, (short)0, 1.0f, 100f, 100f, (float)spatial.Length(geom), geom));
                }
                _enumerator = _roads.GetEnumerator();
            }
        }

        public MatcherTest()
        {
            var reader = new MockedRoadReader(_spatial);
            var roadMapBuilder = new RoadMapBuilder();
            _map = roadMapBuilder.AddRoads(reader.Roads).Build();
        }

        private void AssertCandidate(in (MatcherCandidate, Double) candidate, Coordinate2D sample)
        {
            var polyline = _map.GetEdge(candidate.Item1.RoadPoint.Road.Id).Geometry;
            var f = _spatial.Intercept(polyline, sample);
            var i = _spatial.Interpolate(polyline, f);
            var l = _spatial.Distance(i, sample);
            var sig2 = Math.Pow(5d, 2);
            var sqrt_2pi_sig2 = Math.Sqrt(2d * Math.PI * sig2);
            var p = 1 / sqrt_2pi_sig2 * Math.Exp((-1) * l * l / (2 * sig2));

            AssertEquals(f, candidate.Item1.RoadPoint.Fraction, 10E-6);
            AssertEquals(p, candidate.Item2, 10E-6);
        }

        private void AssertTransition(in (MatcherTransition, Double) transition,
                in (MatcherCandidate, MatcherSample) source,
                in (MatcherCandidate, MatcherSample) target, double lambda)
        {
            var edges = _router.Route(source.Item1.RoadPoint, target.Item1.RoadPoint, _cost);
            Assert.NotNull(edges);

            var route = new Route(source.Item1.RoadPoint, target.Item1.RoadPoint, edges);

            AssertEquals(route.Length, transition.Item1.Route.Length, 10E-6);
            Assert.Equal(route.StartPoint.Road.Id, transition.Item1.Route.StartPoint.Road.Id);
            Assert.Equal(route.EndPoint.Road.Id, transition.Item1.Route.EndPoint.Road.Id);

            double beta = lambda == 0 ? (2.0 * (target.Item2.Time - source.Item2.Time) / 1000)
                    : 1 / lambda;
            double @base = 1.0 * _spatial.Distance(source.Item2.Coordinate, target.Item2.Coordinate) / 60;
            double p = (1 / beta)
                    * Math.Exp((-1.0) * Math.Max(0, route.Cost(Costs.TimePriorityCost) - @base) / beta);

            AssertEquals(transition.Item2, p, 10E-6);
        }

        private ISet<long> RefSet(Coordinate2D sample, double radius)
        {
            var refset = new HashSet<long>();
            foreach (var road in _map.Edges.Values)
            {
                double f = _spatial.Intercept(road.Geometry, sample);
                var i = _spatial.Interpolate(road.Geometry, f);
                double l = _spatial.Distance(i, sample);

                if (l <= radius)
                {
                    refset.Add(road.Id);
                }
            }
            return refset;
        }

        [Fact]
        public void TestCandidates()
        {
            var filter = new Matcher(_map, _router, _cost, _spatial);
            {
                filter.MaxRadius = 100D;
                var sample = new Coordinate2D(11.001, 48.001);

                var candidates = filter.Candidates(new MatcherCandidate[] { }, new MatcherSample(0, 0, sample));

                Assert.Empty(candidates);
            }
            void assertCandidate(double radius, Coordinate2D sample, IEnumerable<long> refsetIds)
            {
                filter.MaxRadius = radius;

                var candidates = filter.Candidates(new MatcherCandidate[] { }, new MatcherSample(0, 0, sample));

                var refset = new HashSet<long>(refsetIds);
                var set = new HashSet<long>();

                foreach (var candidate in candidates)
                {
                    Assert.Contains(candidate.Item1.RoadPoint.Road.Id, refset);
                    AssertCandidate(candidate, sample);
                    set.Add(candidate.Item1.RoadPoint.Road.Id);
                }

                Assert.Equal(refset, set);
            }
            assertCandidate(200D, new Coordinate2D(11.001, 48.001), new long[] { 0L, 1L });
            assertCandidate(200D, new Coordinate2D(11.010, 48.000), new long[] { 0L, 3L });
            assertCandidate(200D, new Coordinate2D(11.011, 48.001), new long[] { 0L, 2L, 3L });
            assertCandidate(300D, new Coordinate2D(11.011, 48.001), new long[] { 0L, 2L, 3L, 8L });
            assertCandidate(300D, new Coordinate2D(11.011, 48.001), new long[] { 0L, 2L, 3L, 8L });
            assertCandidate(200D, new Coordinate2D(11.019, 48.001), new long[] { 2L, 3L, 5L, 10L });
        }

    }
}
