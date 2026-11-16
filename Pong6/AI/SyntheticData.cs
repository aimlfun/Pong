using System.Security.Cryptography;
using Pong.GameArea;

namespace Pong.AI;

/// <summary>
/// Headless simulator. Plays the ball (using the real <see cref="Ball"/> class, so the physics cannot drift from the game)
/// from the right bat to the left line and records what the eyes would have seen, plus the true arrival Y.
/// Labels are free and unlimited, so there is no need to wait for rallies to collect training data.
/// </summary>
internal static class SyntheticData
{
    /// <summary>Creates one training item, or null if the ball was never fully visible (should not happen).</summary>
    internal static TrainingDataItem? Generate()
    {
        const int courtW = Vision.CourtWidth;
        const int courtH = Vision.CourtHeight;

        Ball ball = new(courtW, courtH, BallDirection.LeftToRight);

        double landing = -1;
        ball.BallReachedBatLeft += () => landing = ball.Y;
        ball.BallReachedBatRight += () => { };

        // The ball's speed depends on how many times it has been hit. Odd count => it ends up travelling left.
        int bounces = 1 + 2 * RandomNumberGenerator.GetInt32(0, 10);

        // Bat centre can only reach this range, so that is where the ball can be when hit.
        int minY = Bat.c_deadZone + Bat.c_halfTheBatLength;
        int maxY = courtH - Bat.c_deadZone - Bat.c_halfTheBatLength;
        double yHit = RandomNumberGenerator.GetInt32(minY * 10, maxY * 10) / 10.0;

        for (int i = 0; i < bounces; i++)
        {
            // where the form positions the ball, immediately before bouncing it off the right bat.
            ball.X = courtW - (Bat.c_batDistanceFromEdgeX + 7);
            ball.Y = (float)yHit;
            ball.BounceBallOffBat(RandomNumberGenerator.GetInt32(-4, 5)); // hit zone -4..+4
        }

        TrainingDataItem item = new() { round = bounces, xVelocityOfBallInDirectionOfLeftBat = ball.dx, yVelocityOfBallInDirectionOfLeftBat = ball.dy, YPositionOfTheBallWhenItArrivesOnRightBatLine = yHit };

        for (int frame = 1; frame < 5000 && landing < 0; frame++)
        {
            ball.Move();

            if (frame != Vision.Frame1 && frame != Vision.Frame2 && frame != Vision.Frame3) continue;

            // What the game draws is a 4x4 square at the rounded position; the vision code reports its centre.
            double left = Math.Round(ball.X - 2) - Vision.WindowOriginX;
            double centreX = left + 2;
            double centreY = Math.Round(ball.Y - 2) + 2;

            if (left < 0 || left + 4 > Vision.WidthOfEyes) return null; // not wholly visible

            if (frame == Vision.Frame1) { item.frame1X = centreX; item.frame1Y = centreY; }
            else if (frame == Vision.Frame2) { item.frame2X = centreX; item.frame2Y = centreY; }
            else { item.frame3X = centreX; item.frame3Y = centreY; }
        }

        if (landing < 0) return null;

        item.YPositionOfTheBallWhenItArrivesOnLeftBatLine = landing;
        item.FramesComplete = true;

        return item;
    }

    internal static List<TrainingDataItem> Generate(int count)
    {
        List<TrainingDataItem> items = new(count);

        while (items.Count < count)
        {
            TrainingDataItem? item = Generate();
            if (item is not null) items.Add(item);
        }

        return items;
    }
}
