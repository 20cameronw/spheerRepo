using NUnit.Framework;

namespace Frontier.Tests
{
    public class MapGeometryTests
    {
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(12)]
        public void QueryBoundsPreloadOutsideScreenAndRefreshBeforeTheVisibleEdgeIsExhausted(double scale)
        {
            var viewport = new MapViewport();
            viewport.Resize(800, 600);
            viewport.ZoomAt(400, 300, scale / viewport.Scale);
            var bounds = new MapQueryBounds(viewport);
            Assert.AreEqual(-200, viewport.Project(bounds.min.x, bounds.min.y).x, 0.001);
            Assert.AreEqual(1000, viewport.Project(bounds.max.x, bounds.max.y).x, 0.001);
            Assert.IsTrue(bounds.Covers(viewport));
            viewport.Pan(80, 0);
            Assert.IsTrue(bounds.Covers(viewport), "Small pans reuse already prepared systems.");
            viewport.Pan(40, 0);
            Assert.IsFalse(bounds.Covers(viewport), "Refill starts while 80 pixels of loaded margin remain.");
            Assert.Less(viewport.Project(bounds.min.x, bounds.min.y).x, 0);
        }

        [Test]
        public void Viewport_RoundTripsFarCoordinates_AndKeepsZoomAnchor()
        {
            var v = new MapViewport();
            v.Resize(1080, 1920);
            v.Center(1e9, -1e9);
            MapPoint screen = v.Project(1e9 + 0.25, -1e9 + 1.5);
            MapPoint world = v.Unproject(screen.x, screen.y);
            Assert.AreEqual(1e9 + 0.25, world.x, 1e-6);
            Assert.AreEqual(-1e9 + 1.5, world.y, 1e-6);
            MapPoint anchor = v.Unproject(300, 400);
            v.ZoomAt(300, 400, 3);
            MapPoint after = v.Project(anchor.x, anchor.y);
            Assert.AreEqual(300, after.x, 1e-5);
            Assert.AreEqual(400, after.y, 1e-5);
            v.Pan(100, -50);
            after = v.Project(anchor.x, anchor.y);
            Assert.AreEqual(400, after.x, 1e-5);
            Assert.AreEqual(350, after.y, 1e-5);
        }

        [Test]
        public void Viewport_ZoomClamps_AndResizeDoesNotLoseCenter()
        {
            var v = new MapViewport();
            v.Resize(800, 600);
            v.Center(-15000, 70000);
            v.ZoomAt(400, 300, 1e10);
            Assert.AreEqual(MapViewport.MaxScale, v.Scale);
            v.ZoomAt(400, 300, 1e-20);
            Assert.AreEqual(MapViewport.MinScale, v.Scale);
            v.ZoomAt(400, 300, double.NaN);
            v.Resize(1000, 500);
            Assert.AreEqual(500, v.Project(-15000, 70000).x);
            Assert.AreEqual(250, v.Project(-15000, 70000).y);
        }

        [Test]
        public void OrbitalCurve_HasExactEndpoints_AndNoPiecewiseDiscontinuities()
        {
            var a = new MapPoint(-20, 30); var b = new MapPoint(130, -80);
            var curve = new RouteCurve(a, b, new MapPoint(0, 0), new MapPoint(100, -50));
            Assert.AreEqual(a.x, curve.At(-1).x);
            Assert.AreEqual(a.y, curve.At(0).y);
            Assert.AreEqual(b.x, curve.At(1).x);
            Assert.AreEqual(b.y, curve.At(2).y);
            for (int i = 1; i <= 1000; i++)
            {
                MapPoint delta = curve.At(i / 1000.0) - curve.At((i - 1) / 1000.0);
                Assert.Less(delta.Length, 1.0);
                Assert.AreEqual(1, curve.Direction(i / 1000.0).Length, 1e-9);
            }
        }

        [Test]
        public void Curve_CoincidentAndBareSystemEndpoints_AreFinite()
        {
            var a = new MapPoint(12, -8);
            var same = new RouteCurve(a, a, a, a);
            Assert.AreEqual(a.x, same.At(0.5).x);
            Assert.AreEqual(1, same.Direction(0).Length);
            var b = new MapPoint(100, 200);
            var straight = new RouteCurve(a, b, a, b);
            MapPoint middle = straight.At(0.5);
            Assert.AreEqual(56, middle.x, 1e-9);
            Assert.AreEqual(96, middle.y, 1e-9);
        }
    }
}
