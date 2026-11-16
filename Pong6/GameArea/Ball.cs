using System.Security.Cryptography;
using Pong.Utils;

namespace Pong.GameArea;

/// <summary>
/// Direction ball is moving.
/// </summary>
internal enum BallDirection { LeftToRight, RightToLeft };

/// <summary>
/// Represents a "Ball" that can bounce off walls, that notifies owner if it reaches goal line.
/// </summary>
internal class Ball
{
    /// <summary>
    /// Brush used to draw ball.
    /// </summary>
    internal static readonly SolidBrush s_brushUsedToDrawBall = new(Color.FromArgb(150, 255, 255, 255));

    /// <summary>
    /// Delegate for notifying when ball reaches either bat.
    /// </summary>
    /// <param name="zone"></param>
    internal delegate void GoalLineNotify();  // delegate

    /// <summary>
    /// Event fired when ball reaches left goal area.
    /// </summary>
    internal event GoalLineNotify? BallReachedBatLeft;

    /// <summary>
    /// Event fired when ball reaches right goal area.
    /// </summary>
    internal event GoalLineNotify? BallReachedBatRight;

    /// <summary>
    /// X position (horizontal) of ball.
    /// </summary>
    internal float X;

    /// <summary>
    /// Y position (vertical) of ball.
    /// </summary>
    internal float Y;

    /// <summary>
    /// DeltaX ball is moving each frame.
    /// </summary>
    internal float dx;

    /// <summary>
    /// DeltaY ball is moving each frame.
    /// </summary>
    internal float dy;

    /// <summary>
    /// Acceleration to apply each time the ball makes contact with a bat.
    /// </summary>
    internal float accel = 1.05f;//5% faster

    /// <summary>
    /// Height of tennis court that the ball is bouncing in.
    /// </summary>
    private readonly int Height;

    /// <summary>
    /// Width of tennis court that the ball is bouncing in.
    /// </summary>
    private readonly int Width;

    /// <summary>
    /// Constructor. For a new ball.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="direction"></param>
    internal Ball(int width, int height, BallDirection direction)
    {
        Height = height;
        Width = width;

        X = width / 2f;
        Y = height / 2f;

        dy = (RandomNumberGenerator.GetInt32(300, 2500) / 100f) * (RandomNumberGenerator.GetInt32(1, 2) > 1 ? -1 : 1) / 10f;
        dx = (RandomNumberGenerator.GetInt32(900, 2500) / 100f) * (direction == BallDirection.LeftToRight ? 1 : -1) / 10f;

        dx = (float)Math.Round(dx, 1); // 2 digit accuracy causes issues
    }

    /// <summary>
    /// Moves the ball.
    /// </summary>
    internal void Move()
    {
        // move the ball
        X += dx;
        Y += dy;


        // bounce of top
        if (Y < 0)
        {
            dy = -dy;
            Y = -Y; // true reflection (was Y = dy, which shifted the ball inward by up to |dy| per bounce)
        }
        else
        {
            // bounce of bottom
            if (Y > Height)
            {
                dy = -dy;
                Y = 2 * Height - Y;
            }
        }

        // if the ball has reached the left goal-line, we fire the event
        if (X < Bat.c_batDistanceFromEdgeX + 4 + dx / 2)
        {
            X -= dx;
            Y -= dy;

            BallReachedBatLeft?.Invoke();
            return;
        }

        // if the ball has reached the right goal-line, we fire the event
        if (X > Width - Bat.c_batDistanceFromEdgeX - 4 - dx / 2)
        {
            X -= dx;
            Y -= dy;

            BallReachedBatRight?.Invoke();
        }
    }

    /// <summary>
    /// Draw the ball. Sure we could draw a round one, but that would not be "authentic".
    /// </summary>
    /// <param name="graphics"></param>
    internal void Draw(Graphics graphics)
    {
        graphics.FillRectangle(s_brushUsedToDrawBall, (int)Math.Round(X - 2), (int)Math.Round(Y - 2), 4, 4);
    }

    /// <summary>
    /// Ball bounces off bat.
    /// </summary>
    /// <param name="hit"></param>
    internal void BounceBallOffBat(int hit)
    {
        X -= dx; // move away from being inside the bat

        dx = -dx;  // switch direction (right->left, or left->right)
        dy = hit / 2f;

        // speed it up in x & y direction
        dx *= accel;
        dy *= accel;

        // prevent it moving too fast for the frame 2nd image to catch it.
        float maxDx = Pong.AI.Vision.MaxBallSpeed;

        // without this, it could cause it to exponentially speed up.
        dx = dx.Clamp(-maxDx, maxDx);
        dy = dy.Clamp(-maxDx, maxDx);

        // improve training by rounding. The ball position is rounded by virtue it is drawn at
        // a pixel location.
        dx = (float)Math.Round(dx, 1); 
        dy = (float)Math.Round(dy, 1); 
    }
}