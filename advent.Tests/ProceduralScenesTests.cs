using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests
{
    public class ProceduralScenesTests
    {
        public static IEnumerable<object[]> SceneCases()
        {
            yield return new object[] { new GameOfLifeScene() };
            yield return new object[] { new StarfieldParallaxScene() };
            yield return new object[] { new PlasmaSdfScene() };
        }

        [Theory]
        [MemberData(nameof(SceneCases))]
        public void Scene_Activates_Draws_AndExpires(ISpecialScene scene)
        {
            scene.Activate();

            Assert.True(scene.IsActive);
            Assert.False(scene.RainbowSnow);
            Assert.False(scene.HidesTime);

            using var canvas = new Image<Rgba32>(64, 32);
            scene.Elapsed(TimeSpan.FromMilliseconds(200));
            scene.Draw(canvas);

            Assert.True(scene.IsActive);

            scene.Elapsed(TimeSpan.FromSeconds(30));

            Assert.False(scene.IsActive);
            Assert.False(scene.HidesTime);
        }
    }
}
