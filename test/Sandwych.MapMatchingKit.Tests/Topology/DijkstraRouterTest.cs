﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandwych.MapMatchingKit.Topology;
using Xunit;

namespace Sandwych.MapMatchingKit.Tests.Topology
{
    public class DijkstraRouterTest : AbstractRouterTest
    {
        protected override IGraphRouter<Road, RoadPoint> CreateRouter() => new DijkstraRouter<Road, RoadPoint>();

      
    }

}
