using Pong.GameArea;

namespace Pong.AI;

/// <summary>
/// Turns the three observed ball positions into network inputs, and maps network output to/from a bat Y.
/// Also provides a closed-form baseline (straight-line extrapolation + wall folding) to measure the network against.
/// </summary>
internal static class BallPredictor
{
    /// <summary>
    /// true  = raw positions + velocities + the baseline's guess (the net learns a correction; learns fast).
    /// false = raw positions only (6 inputs; the net must learn extrapolation and wall-folding itself).
    /// </summary>
    internal const bool UseEngineeredFeatures = true;

    /// <summary>
    /// Number of features.
    /// </summary>
    internal static int FeatureCount => UseEngineeredFeatures ? 9 : 6;

    /// <summary>
    /// Height of the court.
    /// </summary>
    private const double H = Vision.CourtHeight;

    /// <summary>
    /// Window x of the left bat's goal line (negative: it is off to the left of the eyes).
    /// </summary>
    private static double LeftLineWindowX => Bat.c_batDistanceFromEdgeX + 4 - Vision.WindowOriginX;

    /// <summary>
    /// Bat Y in pixels (0..H) -> network output range (-1..1, which is what tanh can produce).
    /// </summary>
    internal static double YToNN(double y) => (y / H) * 2 - 1;

    /// <summary>
    /// Network output (-1..1) -> bat Y in pixels (0..H).
    /// </summary>
    internal static double NNToY(double output) => Math.Clamp((output + 1) / 2 * H, 0, H);

    /// <summary>
    /// Reflect an unbounded Y into the court [0,H] as a ball bouncing off top and bottom would.
    /// </summary>
    internal static double Fold(double y)
    {
        double period = 2 * H;
        double t = y % period;
        if (t < 0) t += period;
        return t > H ? period - t : t;
    }

    /// <summary>
    /// Where a ball would arrive at the left line if it carried on in a straight line from frame 2 to 3 (wall-folded).
    /// Wrong when a wall bounce falls between frames 2 and 3, which is the case the network has to learn to correct.
    /// </summary>
    internal static double BaselineLandingY(double x2, double y2, double x3, double y3)
    {
        double frames = Vision.Frame3 - Vision.Frame2;
        double vx = (x3 - x2) / frames;
        double vy = (y3 - y2) / frames;

        if (vx > -0.05) return y3; // not travelling left; nothing sensible to extrapolate

        double framesToGo = (LeftLineWindowX - x3) / vx;

        return Fold(y3 + vy * framesToGo);
    }

    /// <summary>
    /// Network inputs, all roughly -1..1.
    /// </summary>
    internal static double[] Features(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        double ex = Vision.WidthOfEyes;

        if (!UseEngineeredFeatures)
        {
            return [x1 / ex * 2 - 1, YToNN(y1), x2 / ex * 2 - 1, YToNN(y2), x3 / ex * 2 - 1, YToNN(y3)];
        }

        double gap = Vision.Frame2 - Vision.Frame1;
        double vy1 = (y2 - y1) / gap; // px per frame, |vy| <= MaxBallSpeed
        double vy2 = (y3 - y2) / (Vision.Frame3 - Vision.Frame2);

        return
        [
            x1 / ex * 2 - 1, YToNN(y1),
            x2 / ex * 2 - 1, YToNN(y2),
            x3 / ex * 2 - 1, YToNN(y3),
            vy1 / Vision.MaxBallSpeed,
            vy2 / Vision.MaxBallSpeed,
            YToNN(BaselineLandingY(x2, y2, x3, y3))
        ];
    }
}
