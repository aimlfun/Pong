namespace Pong.AI.FrameImageToXY;

/// <summary>
/// Turns a snapshot's pixel profiles into the ball's (x, y).
///
/// This used to be a neural network trained (until every one of ~65,000 images was within 1px) to do something that is
/// exactly computable: the ball is the lit pixels, so its position is the centre of the lit pixels in each profile.
/// The result is exact, instant, needs no training or saved file, and cannot fail to converge.
/// </summary>
internal class ImageFrameToXYBrain
{
    private const int c_heightOfCourt = Vision.CourtHeight;

    /// <summary>
    /// Input: horizontal profile (Vision.WidthOfEyes values) followed by vertical profile (262 values), 1 = ball pixel.
    /// Output: ball centre (x within the snapshot, y within the court), or null if no ball pixel is lit.
    /// </summary>
    internal double[]? AIMapImageIntoXY(double[] profiles)
    {
        int width = (int)Vision.WidthOfEyes;

        double x = CentreOfLitPixels(profiles, 0, width);
        double y = CentreOfLitPixels(profiles, width, c_heightOfCourt);

        if (double.IsNaN(x) || double.IsNaN(y)) return null;

        return [x, y];
    }

    /// <summary>
    /// Average index of the lit pixels, +0.5 so that it is a coordinate (centre of the pixel) rather than an index.
    /// A 4px ball drawn at pixels i..i+3 gives i+2, which is the ball's drawn centre.
    /// </summary>
    private static double CentreOfLitPixels(double[] profiles, int offset, int length)
    {
        double sum = 0;
        int count = 0;

        for (int i = 0; i < length; i++)
        {
            if (profiles[offset + i] == 0) continue;

            sum += i;
            ++count;
        }

        return count == 0 ? double.NaN : sum / count + 0.5;
    }
}
