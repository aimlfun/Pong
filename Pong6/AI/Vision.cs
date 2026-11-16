namespace Pong.AI;

/// <summary>
/// Geometry shared by the game, the "eyes" (snapshot window) and the headless simulator.
/// Kept free of WinForms types so the simulator can run without a form.
/// </summary>
internal static class Vision
{
    /// <summary>Court size in pixels. Must match pictureBoxDisplay (asserted by the form).</summary>
    internal const int CourtWidth = 429;
    internal const int CourtHeight = 262;

    /// <summary>Width of the snapshot window ("eyes") that sits just left of the right-hand bat.</summary>
    internal const double WidthOfEyes = 250;

    /// <summary>
    /// Snapshot window x = court x - WindowOriginX.
    /// (Equals the old expression -(width - batDistance - 15) + eyes - 13 for width 429.)
    /// </summary>
    internal const int WindowOriginX = CourtWidth - 282;

    /// <summary>Frames (counted from the ball leaving the right bat) at which the ball is photographed.</summary>
    internal const int Frame1 = 1;
    internal const int Frame2 = Frame1 + 50;
    internal const int Frame3 = Frame2 + 50;

    /// <summary>
    /// Fastest the ball may go in either axis.
    /// The ball leaves the right bat at about window x=245 and must still be wholly inside the window at Frame3:
    /// 245 - 101 * speed >= ~4  =>  speed <= 2.38. Rounded down to the 1 decimal place the ball uses.
    /// (The original cap, eyes/Frame3 = 2.475, rounds to 2.5 which puts frame 3 outside the window.)
    /// </summary>
    internal const float MaxBallSpeed = 2.3f;
}
