// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Viewing;

namespace Consolify.Core.Tests.Viewing;

public class ViewingGeometryTests
{
    [Theory]
    // diagonal ("), width px, height px, distance (cm), expected PPD, expected ceiling
    [InlineData(24, 1920, 1080, 60, 40, UpscaleCeiling.QualityOrNative)]
    [InlineData(27, 2560, 1440, 70, 55, UpscaleCeiling.Balanced)]
    [InlineData(7, 1920, 1080, 35, 77, UpscaleCeiling.Performance)]
    [InlineData(55, 1920, 1080, 300, 84, UpscaleCeiling.Performance)]
    [InlineData(65, 3840, 2160, 300, 142, UpscaleCeiling.Performance)]
    public void Computes_pixels_per_degree_and_ceiling(
        double diagonal, int width, int height, double distance, double expectedPpd, UpscaleCeiling expectedCeiling)
    {
        double widthCm = ViewingGeometry.WidthFromDiagonalCm(diagonal, width, height);

        double ppd = ViewingGeometry.PixelsPerDegree(width, widthCm, distance);

        Assert.Equal(expectedPpd, ppd, tolerance: 1.0);
        Assert.Equal(expectedCeiling, ViewingGeometry.CeilingFor(ppd));
    }

    [Fact]
    public void Handheld_screens_get_a_shorter_default_distance_than_laptops()
    {
        Assert.Equal(35, ViewingGeometry.DefaultDistanceCm(DisplayKind.Internal, diagonalInches: 7));
        Assert.Equal(55, ViewingGeometry.DefaultDistanceCm(DisplayKind.Internal, diagonalInches: 14));
        Assert.Equal(280, ViewingGeometry.DefaultDistanceCm(DisplayKind.Television));
    }
}
