﻿using System;
using System.Collections.Generic;
using System.Text;
using GeoAPI.Geometries;
using Sandwych.MapMatchingKit.Spatial;
using Sandwych.MapMatchingKit.Spatial.Index;

namespace Sandwych.MapMatchingKit.Tests.Spatial.Index
{
    public class RtreeIndexTest : AbstractSpatialIndexTest
    {
        protected override ISpatialIndex<ILineString> CreateSpatialIndex() =>
            new RtreeIndex<ILineString>(this.Geometries, this.Spatial, x => x, x => this.Spatial.Length(x));
    }
}
