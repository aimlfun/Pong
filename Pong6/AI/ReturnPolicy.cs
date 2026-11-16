using Pong.GameArea;

namespace Pong.AI;

/// <summary>
/// Decides WHERE ON THE BAT to meet the ball. The network only predicts where the ball arrives; this is a separate,
/// deterministic placement step on top of that prediction.
///
/// Why it is needed: <see cref="Ball.BounceBallOffBat"/> sets the return angle purely from the hit zone (dy = zone/2),
/// ignoring the incoming angle. A bat centred under the ball (zone 0) returns it dead flat, which is trivial to answer.
/// So we deliberately put the bat centre to one side of the ball, to hit with an outer zone and send it off at an angle.
///
/// The cost is reach: offsetting by d leaves only (halfBat - d) px of margin for prediction error. Hence a cap
/// (<see cref="MaxOffsetFor"/>) that shrinks as the network's measured error grows.
/// </summary>
internal static class ReturnPolicy
{
    /// <summary>
    /// Never offset by less than this (zone 2 = 8px; below ~6px the return is nearly flat again).
    /// </summary>
    internal const int MinOffsetPx = 6;

    internal readonly record struct Aim(int BatY, int Zone, double ReturnLandingY);

    /// <summary>
    /// Largest offset (px between ball and bat centre) that leaves room for the network's error.
    /// expectedErrorPx is the mean absolute error; ~2.5x covers nearly all of it.
    /// </summary>
    internal static int MaxOffsetFor(double expectedErrorPx, double marginInErrors = 2.5)
    {
        return (int)Math.Floor(Bat.c_halfTheBatLength - marginInErrors * expectedErrorPx);
    }

    /// <summary>
    /// Pick a bat centre Y. We assume the opponent shadows the ball, so it will be standing about where we meet it, and
    /// choose the side/offset whose return lands furthest from there (the corners being the hardest, and out of reach in the dead zone).
    /// </summary>
    /// <param name="predictedY">Network's predicted arrival Y (px).</param>
    /// <param name="x2">Ball x in snapshot 2 and 3, from which the incoming speed is taken.</param>
    /// <param name="maxOffsetPx">See <see cref="MaxOffsetFor"/>. Below <see cref="MinOffsetPx"/> we simply centre on the ball.</param>
    internal static Aim Choose(double predictedY, double x2, double x3, int maxOffsetPx)
    {
        int ball = (int)Math.Round(predictedY);
        int lowest = Bat.c_deadZone + Bat.c_halfTheBatLength;
        int highest = Vision.CourtHeight - lowest;
        float incomingDx = (float)Math.Round((x3 - x2) / (Vision.Frame3 - Vision.Frame2), 1);

        Aim best = Centre(ball, incomingDx, lowest, highest);
        double bestScore = double.NegativeInfinity;
        bool found = false;

        for (int offset = MinOffsetPx; offset <= maxOffsetPx; offset++)
        {
            foreach (int side in new[] { -1, 1 })
            {
                int batY = Math.Clamp(ball - side * offset, lowest, highest);
                int dist = ball - batY; // the clamp may have shrunk (never grown) the offset

                if (Math.Abs(dist) < MinOffsetPx || Math.Abs(dist) > maxOffsetPx) continue;

                int zone = Bat.HitZone(dist);
                double landing = SimulateReturn(ball, incomingDx, zone);

                // Farther from where the opponent is standing is better; a return they cannot reach is better still.
                double score = Math.Abs(landing - ball) + (Unreachable(landing) ? 1000 : 0) - 0.01 * Math.Abs(dist);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Aim(batY, zone, landing);
                    found = true;
                }
            }
        }

        return found ? best : Centre(ball, incomingDx, lowest, highest);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ball"></param>
    /// <param name="incomingDx"></param>
    /// <param name="lowest"></param>
    /// <param name="highest"></param>
    /// <returns></returns>
    private static Aim Centre(int ball, float incomingDx, int lowest, int highest)
    {
        int batY = Math.Clamp(ball, lowest, highest);
        int zone = Bat.HitZone(ball - batY);

        return new Aim(batY, zone, zone == int.MaxValue ? ball : SimulateReturn(ball, incomingDx, zone));
    }

    /// <summary>
    /// The right-hand bat cannot centre on a ball this close to the top or bottom wall.
    /// </summary>
    internal static bool Unreachable(double y)
    {
        double lowest = Bat.c_deadZone + Bat.c_halfTheBatLength;

        return y - lowest <= -(Bat.c_halfTheBatLength + 1) || y - (Vision.CourtHeight - lowest) >= Bat.c_halfTheBatLength + 1;
    }

    /// <summary>
    /// Where the ball would arrive at the right bat's line if returned from zone <paramref name="zone"/>.
    /// Uses the real <see cref="Ball"/> (speed-up, rounding, wall reflection), so it cannot drift from the game.
    /// </summary>
    internal static double SimulateReturn(double ballY, float incomingDx, int zone)
    {
        Ball ball = new(Vision.CourtWidth, Vision.CourtHeight, BallDirection.RightToLeft)
        {
            X = Bat.c_batDistanceFromEdgeX + 4,
            Y = (float)ballY,
            dx = incomingDx
        };

        double landing = -1;
        ball.BallReachedBatRight += () => landing = ball.Y;
        ball.BallReachedBatLeft += () => landing = ballY; // cannot happen after a return; keeps the loop finite
        ball.BounceBallOffBat(zone);

        for (int i = 0; i < 5000 && landing < 0; i++) ball.Move();

        return landing < 0 ? ballY : landing;
    }
}
