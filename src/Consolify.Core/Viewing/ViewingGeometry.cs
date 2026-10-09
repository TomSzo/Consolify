// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Viewing;

/// <summary>How a display is used, which sets the default viewing distance.</summary>
public enum DisplayKind
{
    /// <summary>The device's own screen (laptop, handheld).</summary>
    Internal,

    /// <summary>A desk monitor.</summary>
    Monitor,

    /// <summary>A TV, typically viewed from a couch.</summary>
    Television,
}

/// <summary>The highest upscaler quality step that stays invisible at a given pixel density.</summary>
public enum UpscaleCeiling
{
    /// <summary>Low pixel density: render near native resolution (Quality mode or native).</summary>
    QualityOrNative,

    /// <summary>Medium pixel density: Balanced mode is the most aggressive invisible step.</summary>
    Balanced,

    /// <summary>High pixel density: even Performance mode is hard to tell apart from native.</summary>
    Performance,
}

/// <summary>
/// Converts screen size, resolution and viewing distance into pixels per degree (PPD): how many pixels
/// fall into one degree of the viewer's field of view. PPD decides how visible upscaling artefacts are.
/// </summary>
public static class ViewingGeometry
{
    /// <summary>
    /// Around 60 PPD an average eye no longer resolves individual pixels. Thresholds are starting
    /// points, to be refined with community measurements.
    /// </summary>
    public const double PerformanceThresholdPpd = 60;

    /// <summary>Below this density only near-native rendering looks console-clean.</summary>
    public const double BalancedThresholdPpd = 45;

    /// <summary>Default viewing distance in centimetres for each kind of display.</summary>
    public static double DefaultDistanceCm(DisplayKind kind, double? diagonalInches = null) => kind switch
    {
        // Handhelds are held at arm's length; laptops sit a little further away.
        DisplayKind.Internal => diagonalInches is < 10 ? 35 : 55,
        DisplayKind.Monitor => 65,
        DisplayKind.Television => 280,
        _ => 65,
    };

    /// <summary>Horizontal field of view, in degrees, that the screen covers.</summary>
    public static double HorizontalFovDegrees(double widthCm, double distanceCm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthCm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distanceCm);
        return 2 * Math.Atan(widthCm / 2 / distanceCm) * 180 / Math.PI;
    }

    /// <summary>Pixels per degree at the centre of the screen.</summary>
    public static double PixelsPerDegree(int horizontalPixels, double widthCm, double distanceCm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(horizontalPixels);
        return horizontalPixels / HorizontalFovDegrees(widthCm, distanceCm);
    }

    /// <summary>Screen width in centimetres from its diagonal and aspect ratio.</summary>
    public static double WidthFromDiagonalCm(double diagonalInches, int horizontalPixels, int verticalPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diagonalInches);
        double aspect = (double)horizontalPixels / verticalPixels;
        return diagonalInches * 2.54 * aspect / Math.Sqrt((aspect * aspect) + 1);
    }

    /// <summary>The most aggressive upscaler step that stays invisible at this density.</summary>
    public static UpscaleCeiling CeilingFor(double pixelsPerDegree) => pixelsPerDegree switch
    {
        >= PerformanceThresholdPpd => UpscaleCeiling.Performance,
        >= BalancedThresholdPpd => UpscaleCeiling.Balanced,
        _ => UpscaleCeiling.QualityOrNative,
    };
}
