using System;
using System.Linq;
using Xunit;

namespace advent.Tests
{
    public class SceneSelectorTests
    {
        [Fact]
        public void Constructor_ThrowsForMonthOutsideCalendarRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SceneSelector(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SceneSelector(13));
        }

        [Fact]
        public void Constructor_UsesChristmasScenes_InDecember()
        {
            var sut = new SceneSelector(12);

            Assert.Equal(8, sut.AvailableSceneNames.Count);
            Assert.Equal("Santa", sut.AvailableSceneNames[0]);
            Assert.All(sut.AvailableSceneNames.Skip(1), name => Assert.Equal("Animated GIF", name));
        }

        [Fact]
        public void Constructor_UsesDefaultScenes_OutsideDecember()
        {
            var sut = new SceneSelector(11);

            var expected = new[]
            {
                "Cat",
                "Rainbow",
                "Game of Life",
                "Starfield Parallax",
                "Plasma SDF",
                "Error",
                "Space Invaders",
                "CTM Logo",
                "CTM Banner"
            };

            Assert.Equal(expected, sut.AvailableSceneNames);
        }

        [Fact]
        public void GetScene_ThrowsWhenIndexProviderReturnsOutOfRange()
        {
            var sut = new SceneSelector(11, _ => 99);

            Assert.Throws<InvalidOperationException>(() => sut.GetScene());
        }
    }
}
